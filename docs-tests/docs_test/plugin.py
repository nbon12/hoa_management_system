"""The pytest plugin: collects documents and reports one test per claim.

Loaded through ``addopts = -p docs_test.plugin`` in ``docs-tests/pytest.ini``, so
every Markdown file pytest is pointed at becomes a test module whose items are its
fenced examples (via Sybil) plus one item for the document's links and paths.
"""

from __future__ import annotations

from pathlib import Path

import pytest
from sybil import Sybil
from sybil.integration.pytest import SybilFile
from sybil.parsers.markdown import SkipParser

from . import corpus, references
from .config import load_config
from .fences import default_parser
from .runtime import configure, runtime

MARKDOWN_SUFFIXES = {".md", ".markdown"}


def pytest_addoption(parser: pytest.Parser) -> None:
    group = parser.getgroup("docs-test", "executable documentation testing")
    group.addoption(
        "--docs-no-exec",
        action="store_true",
        default=False,
        help="check docs-test=run blocks statically instead of running them",
    )
    group.addoption(
        "--docs-config",
        default=None,
        metavar="PATH",
        help="docs-test.toml to use (default: docs-tests/docs-test.toml)",
    )


def pytest_configure(config: pytest.Config) -> None:
    override = config.getoption("--docs-config")
    configure(
        execute=not config.getoption("--docs-no-exec"),
        cfg=load_config(Path(override).resolve()) if override else None,
    )
    config._docs_sybil = Sybil(  # type: ignore[attr-defined]
        parsers=[default_parser(runtime().config), SkipParser()],
        patterns=["*.md", "*.markdown"],
        name="docs",
    )


def pytest_collect_file(file_path: Path, parent: pytest.Collector) -> pytest.Collector | None:
    if file_path.suffix.lower() not in MARKDOWN_SUFFIXES:
        return None
    if not runtime().config.in_scope(corpus.relative(file_path)):
        return None
    sybil = getattr(parent.config, "_docs_sybil", None)
    if sybil is None:
        return None
    return DocumentFile.from_parent(parent, path=file_path, sybils=[sybil])


class ReferencesItem(pytest.Item):
    """The document's links and backticked repository paths, as one test."""

    def runtest(self) -> None:
        relative = corpus.relative(self.path)
        findings = references.check(relative, runtime().config)
        if findings:
            listed = "\n".join(f"  - {finding}" for finding in findings)
            pytest.fail(
                f"{relative} refers to things that are not in the repository:\n{listed}\n\n"
                "Fix the reference, or add a deliberately hypothetical one to "
                "references.ignore_paths in docs-tests/docs-test.toml.",
                pytrace=False,
            )

    def reportinfo(self) -> tuple[Path, int, str]:
        return self.path, 0, f"{corpus.relative(self.path)} references"


class DocumentFile(SybilFile):
    """Sybil's collector, plus the document-wide reference check."""

    def collect(self):
        yield from super().collect()
        yield ReferencesItem.from_parent(self, name="references")
