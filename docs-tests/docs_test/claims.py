"""Repository facts the documentation is allowed to claim.

Every claim is resolved against **tracked** files rather than the working tree, so
a check behaves the same on a developer's machine, where ``bin/`` and ``coverage/``
exist, as on a fresh CI checkout, where they do not.
"""

from __future__ import annotations

import json
import subprocess
from functools import lru_cache
from pathlib import Path

import yaml

from .config import repo_root

#: Top-level directories that can start a repository path claim. Anything else with
#: a slash in it — ``api/v1/properties``, ``and/or``, ``CRITICAL,HIGH`` — is prose.
PATH_ROOTS: tuple[str, ...] = (
    ".github",
    ".specify",
    ".claude",
    "docs",
    "docs-tests",
    "infra",
    "neko-hoa",
    "repowise",
    "scripts",
    "specs",
    "wireframes",
    "HOAManagementCompany",
    "HOAManagementCompany.Tests",
)


@lru_cache(maxsize=1)
def tracked_paths() -> frozenset[str]:
    """Every file the repository has, plus every directory that contains one.

    Tracked files and new, not-yet-committed ones both count, so documentation
    written alongside the code it describes passes before the commit as well as
    after it. Ignored files never do — see :func:`is_git_ignored`.
    """
    listing = subprocess.run(
        (
            "git",
            "-c",
            "core.excludesFile=/dev/null",
            "ls-files",
            "-z",
            "--cached",
            "--others",
            "--exclude-standard",
        ),
        cwd=repo_root(),
        capture_output=True,
        text=True,
        check=True,
    ).stdout
    paths: set[str] = set()
    for entry in listing.split("\0"):
        if not entry:
            continue
        paths.add(entry)
        parent = Path(entry).parent
        while parent != Path("."):
            paths.add(parent.as_posix())
            parent = parent.parent
    return frozenset(paths)


@lru_cache(maxsize=4096)
def is_git_ignored(claim: str) -> bool:
    """Does .gitignore cover ``claim``?

    Generated and developer-local paths — ``neko-hoa/dist``, ``coverage/``,
    ``appsettings.Secrets.json`` — are legitimate things for documentation to name
    even though they are never committed, so a claim git ignores is not a finding.
    ``core.excludesFile`` is neutralised so the answer is the repository's, not
    whatever the developer has in their home directory.
    """
    result = subprocess.run(
        ("git", "-c", "core.excludesFile=/dev/null", "check-ignore", "--no-index", "-q", "--", claim),
        cwd=repo_root(),
        capture_output=True,
    )
    return result.returncode == 0


def path_exists(claim: str) -> bool:
    """Is ``claim`` a path the repository really has (or deliberately ignores)?"""
    return claim in tracked_paths() or is_git_ignored(claim)


def clean_path(token: str) -> str | None:
    """Normalise ``token`` as a path, or return ``None`` if it cannot be one."""
    token = token.strip().strip("`").rstrip(".,;:!?)")
    if not token or any(c in token for c in ' \t*?[]{}|<>"\'()$'):
        return None
    if token.startswith(("http://", "https://", "//", "-", "~", "@", "#")):
        return None
    token = token.split("#", 1)[0]
    # file.cs:42 and file.md:L42 point at a line inside a file.
    if ":" in token:
        head, _, tail = token.rpartition(":")
        token = head if head and (tail.isdigit() or tail.startswith("L")) else token
    if ":" in token:
        return None
    if token.startswith("./"):
        token = token[2:]
    token = token.rstrip("/")
    return token or None


def looks_like_path(token: str) -> str | None:
    """:func:`clean_path`, but only for tokens that have a slash in them.

    A bare word is a command or a sentence fragment, and guessing otherwise is how
    a checker starts crying wolf over ``npm`` and ``echo``.
    """
    if "/" not in token:
        return None
    return clean_path(token)


def is_path_claim(token: str) -> str | None:
    """``token`` as a repository path claim, or ``None``.

    Conservative on purpose: a claim has to be rooted at one of :data:`PATH_ROOTS`
    so that prose with slashes in it — ``api/v1/properties``, ``and/or`` — is never
    mistaken for a path.
    """
    cleaned = looks_like_path(token)
    if cleaned is None or cleaned.split("/", 1)[0] not in PATH_ROOTS:
        return None
    return cleaned


@lru_cache(maxsize=1)
def npm_scripts() -> frozenset[str]:
    package = repo_root() / "neko-hoa" / "package.json"
    if not package.exists():
        return frozenset()
    return frozenset(json.loads(package.read_text(encoding="utf-8")).get("scripts", {}))


@lru_cache(maxsize=1)
def compose_services() -> frozenset[str]:
    compose = repo_root() / "docker-compose.yaml"
    if not compose.exists():
        return frozenset()
    document = yaml.safe_load(compose.read_text(encoding="utf-8")) or {}
    return frozenset(document.get("services", {}))


@lru_cache(maxsize=1)
def workflows() -> frozenset[str]:
    directory = repo_root() / ".github" / "workflows"
    if not directory.is_dir():
        return frozenset()
    return frozenset(path.name for path in directory.iterdir() if path.suffix in {".yml", ".yaml"})
