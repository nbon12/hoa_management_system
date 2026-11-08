"""Checks for shell examples.

Three things happen to every shell block, in order:

1. **Syntax.** ``bash -n`` parses it, with ``<placeholders>`` substituted so an
   example that asks the reader to fill in a value still parses.
2. **Claims.** Commands that name something in the repository — an npm script, a
   .NET project, a compose service, a workflow, a path — have to name something
   that is really there. This is where documentation drift is caught, and it needs
   no execution at all.
3. **Execution**, for blocks annotated ``docs-test=run``.
"""

from __future__ import annotations

import os
import re
import shutil
import subprocess
from pathlib import Path

from . import claims
from .config import repo_root
from .fences import Block
from .runtime import runtime

#: ``tofu apply -var bucket_name=<name>`` is a documentation convention, not a
#: redirection. Substituted before ``bash -n`` so the example still parses.
PLACEHOLDER = re.compile(r"<[A-Za-z][A-Za-z0-9_.:/+ -]{0,60}>")

NPM_RUN = re.compile(r"\bnpm (?:run|run-script) ([A-Za-z0-9:_-]+)")
DOTNET_PROJECT = re.compile(r"\bdotnet\s+(?:[a-z-]+\s+)*--project[= ]\s*([^\s\\&;|'\"]+)")
DOTNET_TARGET = re.compile(
    r"\bdotnet\s+(?:run|test|build|publish|watch|pack)\s+(?P<target>"
    r"[\w./-]+\.(?:csproj|sln)"  # an explicit project or solution file
    r"|[A-Z][\w-]*(?:\.[A-Z][\w-]*)+"  # a project directory, PascalCase.Dotted
    r")\b"
)
COMPOSE = re.compile(r"\bdocker\s+compose\s+(?:-[^\s]+\s+)*(up|down|run|exec|logs|restart|stop|start|rm|build|pull)((?:\s+-[^\s]+)*)\s*([A-Za-z][\w.-]*)?")
WORKFLOW_RUN = re.compile(r"\bgh\s+workflow\s+(?:run|view|disable|enable)\s+([\w.-]+\.ya?ml)")
#: A word that could be a repository path: used to find path claims inside commands.
WORD = re.compile(r"[^\s\"'`=,;|&()<>]+")

MAX_OUTPUT_LINES = 40


def normalise(source: str) -> str:
    """``source`` with documentation placeholders replaced by a literal word."""
    return PLACEHOLDER.sub("PLACEHOLDER", source)


def check_syntax(block: Block) -> str | None:
    result = subprocess.run(
        ("bash", "-n"),
        input=normalise(block.source),
        capture_output=True,
        text=True,
    )
    if result.returncode == 0:
        return None
    return (
        f"{block.where()}: this shell example is not valid bash\n\n"
        f"{result.stderr.strip()}\n\n"
        "If the block is deliberately not runnable shell, fence it as ```text, or "
        'annotate it ```bash docs-test=skip reason="..."'
    )


def check_claims(block: Block) -> list[str]:
    """Everything this block says about the repository that is not true."""
    source = block.source
    findings: list[str] = []
    # A block annotated cwd=neko-hoa documents paths relative to it; plenty of
    # blocks name repo-root paths as well, so both bases get the benefit of the doubt.
    bases = [block.attributes.cwd] if block.attributes.cwd else []
    bases.append(".")

    for match in NPM_RUN.finditer(source):
        script = match.group(1)
        if claims.npm_scripts() and script not in claims.npm_scripts():
            findings.append(
                f"`npm run {script}` is documented but neko-hoa/package.json has no "
                f"{script!r} script (it has: {', '.join(sorted(claims.npm_scripts()))})"
            )

    for pattern in (DOTNET_PROJECT, DOTNET_TARGET):
        for match in pattern.finditer(source):
            target = match.group(1)
            if "PLACEHOLDER" in normalise(target) or target.startswith("$"):
                continue
            # `dotnet --project X` says X is a path, so it does not have to look like
            # one the way a bare word in a command does.
            claim, satisfied = _resolve(target, bases, rooted=False)
            if claim and not satisfied:
                findings.append(f"`dotnet … {target}` points at {claim}, which is not in the repository")

    for match in COMPOSE.finditer(source):
        service = match.group(3)
        known = claims.compose_services()
        if service and known and service not in known:
            findings.append(
                f"`docker compose {match.group(1)} {service}` names a service that "
                f"docker-compose.yaml does not define (it defines: {', '.join(sorted(known))})"
            )

    for match in WORKFLOW_RUN.finditer(source):
        workflow = match.group(1)
        if claims.workflows() and workflow not in claims.workflows():
            findings.append(f"`gh workflow … {workflow}` names a workflow that .github/workflows does not contain")

    for word in WORD.findall(source):
        claim, satisfied = _resolve(word, bases)
        if claim and not satisfied and not runtime().config.path_claim_ignored(claim):
            findings.append(f"`{word}` refers to {claim}, which is not in the repository")

    return findings


def _resolve(token: str, bases: list[str], rooted: bool = True) -> tuple[str | None, bool]:
    """``token`` as a path claim, and whether any base makes it true.

    With ``rooted``, the claim has to start at one of :data:`claims.PATH_ROOTS` —
    the rule that keeps prose with slashes in it out of the checker. Commands that
    take a path argument pass ``rooted=False``, because the command already said
    the argument is a path.
    """
    cleaned = claims.looks_like_path(token) if rooted else claims.clean_path(token)
    if cleaned is None:
        return None, False
    first: str | None = None
    for base in bases:
        candidate = _join(base, cleaned)
        claim = claims.is_path_claim(candidate) if rooted else claims.clean_path(candidate)
        if claim is None:
            continue
        first = first or claim
        if claims.path_exists(claim):
            return claim, True
    return first, False


def _join(base: str, target: str) -> str:
    if base in {"", "."} or target.startswith("/"):
        return target
    return (Path(base) / target).as_posix()


def _tail(text: str) -> str:
    lines = text.strip().splitlines()
    if len(lines) <= MAX_OUTPUT_LINES:
        return "\n".join(lines)
    return "\n".join(["…"] + lines[-MAX_OUTPUT_LINES:])


def denied_command(block: Block) -> str | None:
    """Is this runnable block asking for something no example may do?

    Checked for every ``docs-test=run`` block, whether or not it is about to run:
    a destructive example is a problem in the document, and finding it only on the
    machine that happens to have the tool installed would defeat the point.
    """
    for pattern in runtime().config.exec_denylist:
        if re.search(pattern, block.source, re.IGNORECASE):
            return (
                f"{block.where()}: this block is annotated docs-test=run but matches the "
                f"denied command pattern {pattern!r}. Runnable examples have to be safe to "
                "run on any checkout; leave destructive commands unannotated."
            )
    return None


def execute(block: Block) -> str | None:
    """Run a ``docs-test=run`` block and check its exit code and output."""
    import pytest

    attributes = block.attributes
    config = runtime().config

    denied = denied_command(block)
    if denied:
        return denied

    missing = [tool for tool in attributes.needs if shutil.which(tool) is None]
    if missing:
        pytest.skip(f"needs {', '.join(missing)}, which is not installed here")

    cwd = repo_root() / (attributes.cwd or ".")
    if not cwd.is_dir():
        return f"{block.where()}: cwd={attributes.cwd} does not exist"

    timeout = attributes.timeout or config.exec_default_timeout
    environment = {**os.environ, "DOCS_TEST": "1", "NO_COLOR": "1"}
    try:
        result = subprocess.run(
            ("bash", "-e", "-o", "pipefail", "-c", block.source),
            cwd=cwd,
            capture_output=True,
            text=True,
            timeout=timeout,
            env=environment,
        )
    except subprocess.TimeoutExpired:
        return (
            f"{block.where()}: still running after {timeout}s. Raise it with "
            f"timeout=<seconds> if the example is meant to take that long."
        )

    output = f"{result.stdout}\n{result.stderr}"
    if result.returncode != attributes.exit_code:
        return (
            f"{block.where()}: exited {result.returncode}, expected {attributes.exit_code}\n\n"
            f"$ {block.source.strip()}\n\n{_tail(output)}"
        )
    if attributes.expect and attributes.expect not in output:
        return (
            f"{block.where()}: output does not contain {attributes.expect!r}\n\n"
            f"$ {block.source.strip()}\n\n{_tail(output)}"
        )
    return None


def evaluate(example) -> str | None:
    """Sybil evaluator for shell blocks."""
    block: Block = example.parsed

    syntax = check_syntax(block)
    if syntax:
        return syntax

    findings = check_claims(block)
    if findings:
        joined = "\n".join(f"  - {finding}" for finding in findings)
        return (
            f"{block.where()}: this shell example describes things that are not in the "
            f"repository:\n{joined}\n\n"
            "Update the example, or the code it describes. If the reference is "
            'deliberately hypothetical, add it to references.ignore_paths in '
            "docs-tests/docs-test.toml."
        )

    if block.attributes.runnable:
        # Policy first: a denied command fails whether or not this run executes
        # anything, and whether or not `needs=` would have skipped the block.
        denied = denied_command(block)
        if denied:
            return denied
        if not runtime().execute:
            import pytest

            pytest.skip("execution disabled (--docs-no-exec)")
        return execute(block)
    return None
