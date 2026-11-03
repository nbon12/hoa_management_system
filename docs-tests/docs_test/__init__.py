"""Executable documentation testing for this repository.

Every fenced example in a tracked Markdown document, and every repository path the
prose claims exists, is a test case. Examples are collected with `Sybil
<https://sybil.readthedocs.io/>`__, which turns each one into an individual pytest
item identified by its file and line, so a failure points at the exact block that
has drifted from the code.

See ``docs-tests/README.md`` for the annotation contract.
"""

from __future__ import annotations

__all__ = ["config", "corpus", "data", "fences", "references", "shell"]
