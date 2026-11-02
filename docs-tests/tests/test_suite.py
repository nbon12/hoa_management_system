"""The suite end to end: run pytest over the fixture documents and count outcomes.

This is the test that proves the plugin is wired up — collection, per-example
items, the document-wide reference item, skips, and execution — rather than just
the checks in isolation.
"""

from __future__ import annotations

import os
import subprocess
import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

import pytest

from docs_test.config import repo_root

#: Recount these when the fixture documents change.
#: passing.md  — 11 checked examples, 3 skipped (2 annotated, 1 Sybil directive),
#:               1 skipped for a missing tool, 1 references item
#: point-in-time.md — 1 example, 1 references item
#: failing.md  — 13 failing examples, 1 failing references item
EXPECTED_PASSED = 14
EXPECTED_FAILED = 14
EXPECTED_SKIPPED = 4

@pytest.fixture(scope="module")
def outcomes(tmp_path_factory) -> dict[str, int]:
    """Run the suite over the fixtures and read the JUnit report it writes."""
    root = repo_root()
    fixtures = root / "docs-tests" / "tests" / "fixtures"
    report = tmp_path_factory.mktemp("docs-test") / "report.xml"
    result = subprocess.run(
        (
            sys.executable,
            "-m",
            "pytest",
            "-c",
            "docs-tests/pytest.ini",
            "--rootdir",
            str(root),
            "--docs-config",
            str(fixtures / "docs-test.toml"),
            "-p",
            "no:cacheprovider",
            "--tb=no",
            f"--junit-xml={report}",
            str(fixtures),
        ),
        cwd=root,
        capture_output=True,
        text=True,
        env={**os.environ, "PYTHONPATH": str(root / "docs-tests")},
    )
    if not report.exists():
        pytest.fail(f"the suite did not run:\n{result.stdout}\n{result.stderr}")

    suite = ElementTree.parse(report).getroot().find("testsuite")
    total, failures, errors, skipped = (
        int(suite.get(name, 0)) for name in ("tests", "failures", "errors", "skipped")
    )
    assert errors == 0, f"collection errors:\n{result.stdout}"
    return {
        "passed": total - failures - errors - skipped,
        "failed": failures,
        "skipped": skipped,
    }


def test_the_failing_fixture_fails_once_per_example(outcomes: dict[str, int]) -> None:
    assert outcomes.get("failed") == EXPECTED_FAILED


def test_the_passing_fixture_passes(outcomes: dict[str, int]) -> None:
    assert outcomes.get("passed") == EXPECTED_PASSED


def test_skips_are_skips_not_silent_passes(outcomes: dict[str, int]) -> None:
    assert outcomes.get("skipped") == EXPECTED_SKIPPED


def test_fixtures_are_outside_the_real_corpus() -> None:
    """Otherwise the gate would fail on documents that are meant to fail."""
    from docs_test import corpus

    assert not any(path.startswith("docs-tests/tests/fixtures/") for path in corpus.tracked_docs())
