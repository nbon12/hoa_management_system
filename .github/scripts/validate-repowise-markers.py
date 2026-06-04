#!/usr/bin/env python3
"""Ensure REPOWISE marker regions are well-formed and non-empty."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SKIP_DIRS = {
    ".git",
    ".github",
    "node_modules",
    "bin",
    "obj",
    "dist",
    "test-results",
    "playwright-report",
    ".repowise",
    ".venv-repowise",
}

START_RE = re.compile(r"<!--\s*REPOWISE:START\b.*?-->")
END_RE = re.compile(r"<!--\s*REPOWISE:END\s*-->")


def iter_source_files() -> list[Path]:
    files: list[Path] = []
    for path in ROOT.rglob("*"):
        if not path.is_file():
            continue
        if any(part in SKIP_DIRS for part in path.relative_to(ROOT).parts):
            continue
        if path.suffix.lower() not in {".md", ".cs", ".ts", ".tsx", ".js", ".jsx", ".py"}:
            continue
        files.append(path)
    return files


def validate_file(path: Path) -> list[str]:
    text = path.read_text(encoding="utf-8")
    errors: list[str] = []
    rel = path.relative_to(ROOT)

    for start in START_RE.finditer(text):
        end = END_RE.search(text, start.end())
        if end is None:
            errors.append(f"{rel}: missing REPOWISE:END after {start.group()}")
            continue

        body = text[start.end() : end.start()]
        if not body.strip():
            errors.append(f"{rel}: empty content in {start.group()}")

    if END_RE.search(text) and not START_RE.search(text):
        errors.append(f"{rel}: contains REPOWISE:END without a matching START")

    return errors


def main() -> int:
    marker_files = [
        p for p in iter_source_files() if "REPOWISE:START" in p.read_text(encoding="utf-8")
    ]

    if not marker_files:
        print("No REPOWISE marker regions found.", file=sys.stderr)
        return 1

    errors: list[str] = []
    for path in marker_files:
        errors.extend(validate_file(path))

    if errors:
        print("Repowise marker validation failed:", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(f"Validated {len(marker_files)} file(s) with REPOWISE marker regions.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
