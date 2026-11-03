"""Links and backticked repository paths."""

from __future__ import annotations

from docs_test import references
from docs_test.config import Config, config


def test_slug_matches_github() -> None:
    assert references.slug("What is checked") == "what-is-checked"
    assert references.slug("`docs-test=run` blocks") == "docs-testrun-blocks"
    assert references.slug("Why *this* matters?") == "why-this-matters"


def test_anchors_include_repeated_headings(tmp_path) -> None:
    document = tmp_path / "doc.md"
    document.write_text("# Title\n\n## Notes\n\n## Notes\n\n<a id=\"manual\"></a>\n", encoding="utf-8")

    assert references.anchors(document) == frozenset({"title", "notes", "notes-1", "manual"})


def test_headings_inside_code_blocks_are_not_anchors(tmp_path) -> None:
    document = tmp_path / "doc.md"
    document.write_text("# Real\n\n```markdown\n# Not real\n```\n", encoding="utf-8")

    assert references.anchors(document) == frozenset({"real"})


def test_the_fixtures_report_what_they_are_meant_to(fixture_config: Config) -> None:
    findings = references.check("docs-tests/tests/fixtures/failing.md", fixture_config)

    assert len(findings) == 3
    assert any("no-such-document.md" in finding for finding in findings)
    assert any("#no-such-heading" in finding for finding in findings)
    assert any("specs/999-not-a-feature/tasks.md" in finding for finding in findings)
    assert all(finding.startswith("docs-tests/tests/fixtures/failing.md:") for finding in findings)


def test_a_passing_document_reports_nothing(fixture_config: Config) -> None:
    assert references.check("docs-tests/tests/fixtures/passing.md", fixture_config) == []


def test_point_in_time_documents_keep_their_history(fixture_config: Config) -> None:
    """Their path claims were true when written; section 11 does not ask for more."""
    assert references.check("docs-tests/tests/fixtures/point-in-time.md", fixture_config) == []

    as_current = Config(
        include=fixture_config.include,
        exclude=fixture_config.exclude,
        point_in_time=(),
        ignore_paths=fixture_config.ignore_paths,
    )
    findings = references.check("docs-tests/tests/fixtures/point-in-time.md", as_current)
    assert any("LongGoneService.cs" in finding for finding in findings)


def test_real_documents_are_clean() -> None:
    """Under the repository's own configuration, not a bare default one."""
    assert references.check("README.md", config()) == []
    assert references.check("docs-tests/README.md", config()) == []
