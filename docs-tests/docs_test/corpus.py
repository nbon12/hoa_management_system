"""Which documents the suite tests.

The corpus is the set of tracked Markdown files that ``docs-test.toml`` includes.
``python -m docs_test.corpus`` prints it, which is how ``scripts/docs-test.sh``
hands the targets to pytest.
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

from .config import Config, config, repo_root


def _git(*args: str) -> str:
    result = subprocess.run(
        ("git", *args),
        cwd=repo_root(),
        capture_output=True,
        text=True,
        check=True,
    )
    return result.stdout


def relative(path: Path | str) -> str:
    """``path`` as a repo-relative POSIX path."""
    resolved = Path(path).resolve()
    try:
        return resolved.relative_to(repo_root()).as_posix()
    except ValueError:
        return resolved.as_posix()


PATTERNS = ("*.md", "*.markdown")


def _names(listing: str) -> set[str]:
    return {path for path in listing.split("\0") if path}


def tracked_docs(cfg: Config | None = None) -> list[str]:
    """Every in-scope Markdown document the repository has, repo-relative and sorted.

    Committed and new-but-not-ignored files both count, so a document passes the
    suite locally before it is committed as well as after.
    """
    cfg = cfg or config()
    listing = _git(
        "-c", "core.excludesFile=/dev/null",
        "ls-files", "-z", "--cached", "--others", "--exclude-standard", *PATTERNS,
    )
    return sorted(path for path in _names(listing) if cfg.in_scope(path))


def changed_docs(base_ref: str, cfg: Config | None = None) -> list[str]:
    """In-scope documents that differ from ``base_ref``.

    Uncommitted and untracked documents count too, so a local run before committing
    sees the same set the pull request gate will see after the push. Deletions are
    dropped — there is nothing left to test.
    """
    cfg = cfg or config()
    merge_base = _git("merge-base", base_ref, "HEAD").strip() or base_ref
    candidates = (
        _names(_git("diff", "--name-only", "--diff-filter=d", "-z", merge_base, "--", *PATTERNS))
        | _names(_git("diff", "--name-only", "--diff-filter=d", "-z", "HEAD", "--", *PATTERNS))
        | _names(_git("ls-files", "--others", "--exclude-standard", "-z", *PATTERNS))
    )
    root = repo_root()
    return sorted(path for path in candidates if cfg.in_scope(path) and (root / path).is_file())


def in_scope(path: Path | str, cfg: Config | None = None) -> bool:
    cfg = cfg or config()
    return cfg.in_scope(relative(path))


def is_point_in_time(path: Path | str, cfg: Config | None = None) -> bool:
    cfg = cfg or config()
    return cfg.is_point_in_time(relative(path))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Print the documentation test corpus.")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--all", action="store_true", help="every in-scope tracked document")
    group.add_argument("--changed", metavar="BASE_REF", help="documents changed against BASE_REF")
    args = parser.parse_args(argv)

    paths = tracked_docs() if args.all else changed_docs(args.changed)
    for path in paths:
        print(path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
