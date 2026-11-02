"""Checks for what a document's prose claims.

Two kinds of claim:

* **Links** — ``[the quickstart](specs/003-dotnet-api-backend/quickstart.md)`` has to
  resolve, and if it carries an anchor the heading has to be there.
* **Paths** — a backticked ``neko-hoa/src/app/app.config.ts`` has to be a file in the
  repository.

Path claims are not checked in point-in-time documents (``tasks.md``, handoffs,
patch notes), which record what was true when they were written. Constitution
section 11 puts the freshness obligation on ``spec.md`` and the like, not on those.
"""

from __future__ import annotations

import re
from functools import lru_cache
from pathlib import Path
from urllib.parse import unquote

from . import claims
from .config import Config, repo_root

FENCE = re.compile(
    r"^(?P<indent>[ \t]*)(?P<fence>```+|~~~+)[^\n]*\n.*?^(?P=indent)(?P=fence)[ \t]*$",
    re.DOTALL | re.MULTILINE,
)
INLINE_CODE = re.compile(r"`([^`\n]+)`")
INLINE_LINK = re.compile(r"\[(?P<text>[^\]]*)\]\((?P<target>[^)\s]+)(?:\s+\"[^\"]*\")?\)")
REFERENCE_LINK = re.compile(r"^\[(?P<label>[^\]]+)\]:[ \t]+(?P<target>\S+)", re.MULTILINE)
HEADING = re.compile(r"^#{1,6}[ \t]+(?P<text>.+?)[ \t]*#*$", re.MULTILINE)
HTML_ANCHOR = re.compile(r"<a[^>]+(?:id|name)=\"(?P<anchor>[^\"]+)\"")
EXTERNAL = ("http://", "https://", "mailto:", "tel:", "ftp://")


def _fence_spans(text: str) -> list[tuple[int, int]]:
    return [(match.start(), match.end()) for match in FENCE.finditer(text)]


def _outside(spans: list[tuple[int, int]], position: int) -> bool:
    return not any(start <= position < end for start, end in spans)


def slug(heading: str) -> str:
    """GitHub's heading anchor for ``heading``."""
    text = re.sub(r"`([^`]*)`", r"\1", heading)
    text = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", text)
    text = re.sub(r"[*_~]+", "", text)
    text = text.strip().lower()
    text = re.sub(r"[^\w\- ]+", "", text, flags=re.UNICODE)
    return text.replace(" ", "-")


@lru_cache(maxsize=512)
def anchors(path: Path) -> frozenset[str]:
    """Every anchor a reader can link to in ``path``."""
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return frozenset()
    spans = _fence_spans(text)
    found: dict[str, int] = {}
    result: set[str] = set()
    for match in HEADING.finditer(text):
        if not _outside(spans, match.start()):
            continue
        base = slug(match.group("text"))
        if not base:
            continue
        count = found.get(base, 0)
        found[base] = count + 1
        result.add(base if count == 0 else f"{base}-{count}")
    result.update(match.group("anchor") for match in HTML_ANCHOR.finditer(text))
    return frozenset(result)


def _line_of(text: str, position: int) -> int:
    return text.count("\n", 0, position) + 1


def check(relative_path: str, config: Config) -> list[str]:
    """Findings for one document, each prefixed with ``path:line``."""
    root = repo_root()
    document = root / relative_path
    text = document.read_text(encoding="utf-8", errors="replace")
    spans = _fence_spans(text)
    findings: list[str] = []

    def report(position: int, message: str) -> None:
        findings.append(f"{relative_path}:{_line_of(text, position)}: {message}")

    targets = [
        (match.start(), match.group("target"))
        for match in INLINE_LINK.finditer(text)
        if _outside(spans, match.start())
    ]
    targets += [
        (match.start(), match.group("target"))
        for match in REFERENCE_LINK.finditer(text)
        if _outside(spans, match.start())
    ]

    for position, target in targets:
        target = target.strip("<>")
        if target.startswith(EXTERNAL) or target.startswith("{"):
            continue  # external links are the sweep's job; {placeholders} are templates

        path_part, _, anchor = target.partition("#")
        path_part = unquote(path_part)

        if not path_part:
            if anchor and config.check_anchors and anchor not in anchors(document):
                report(position, f"link to #{anchor}, but this document has no such heading")
            continue

        resolved = (document.parent / path_part).resolve()
        try:
            claim = resolved.relative_to(root).as_posix()
        except ValueError:
            report(position, f"link to {target} points outside the repository")
            continue

        if config.path_claim_ignored(claim):
            continue
        if not claims.path_exists(claim):
            report(position, f"link to {target} resolves to {claim}, which is not in the repository")
            continue
        if anchor and config.check_anchors and resolved.suffix == ".md":
            if anchor not in anchors(resolved):
                report(position, f"link to {target}, but {claim} has no #{anchor} heading")

    if not config.is_point_in_time(relative_path):
        for match in INLINE_CODE.finditer(text):
            if not _outside(spans, match.start()):
                continue
            claim = claims.is_path_claim(match.group(1))
            if claim is None or config.path_claim_ignored(claim):
                continue
            if not claims.path_exists(claim):
                report(
                    match.start(),
                    f"`{match.group(1)}` refers to {claim}, which is not in the repository",
                )

    return findings
