#!/usr/bin/env python3
"""
Marker sync for Repowise-maintained regions.

Full LLM regeneration requires a provider API key and is not run automatically
in CI until secrets are configured. This script validates markers and reports
regions that would need a local `repowise init` + manual/MCP update.
"""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROMPT = ROOT / "repowise" / "generation-prompt.md"
VALIDATOR = ROOT / ".github" / "scripts" / "validate-repowise-markers.py"


def main() -> int:
    if not PROMPT.is_file():
        print(f"Missing {PROMPT}", file=sys.stderr)
        return 1

    has_key = bool(
        os.environ.get("ANTHROPIC_API_KEY")
        or os.environ.get("OPENAI_API_KEY")
        or os.environ.get("GEMINI_API_KEY")
    )
    if not has_key:
        print(
            "Repowise marker LLM sync skipped: no provider API key in environment. "
            "Set ANTHROPIC_API_KEY (or OPENAI_API_KEY / GEMINI_API_KEY) for the "
            "repowise-docs job, or update markers locally using repowise/generation-prompt.md."
        )
    else:
        print(
            "Provider API key present; automated per-region marker rewrite is not "
            "implemented yet. Run `repowise update` locally and edit marker regions "
            "using repowise/generation-prompt.md, or extend this script."
        )

    if VALIDATOR.is_file():
        return subprocess.call([sys.executable, str(VALIDATOR)], cwd=ROOT)
    print(f"Validator not found: {VALIDATOR}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
