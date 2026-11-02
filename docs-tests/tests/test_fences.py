"""Fence annotations and which blocks become test cases."""

from __future__ import annotations

import pytest
from sybil import Document

from docs_test.config import Config
from docs_test.fences import (
    MAX_TIMEOUT,
    AnnotationError,
    Block,
    FenceParser,
    parse_attributes,
)


def test_no_annotations() -> None:
    attributes = parse_attributes("")
    assert attributes.mode == "check"
    assert not attributes.annotated
    assert not attributes.runnable


def test_run_with_options() -> None:
    attributes = parse_attributes('docs-test=run cwd=neko-hoa needs="npm, node" timeout=120 exit=1')
    assert attributes.runnable
    assert attributes.cwd == "neko-hoa"
    assert attributes.needs == ("npm", "node")
    assert attributes.timeout == 120
    assert attributes.exit_code == 1
    assert attributes.annotated


def test_skip_with_reason() -> None:
    attributes = parse_attributes('docs-test=skip reason="a fragment"')
    assert attributes.skipped
    assert attributes.reason == "a fragment"


def test_unrelated_metadata_is_left_alone() -> None:
    """Another tool's info-line metadata is not ours to fail over."""
    assert not parse_attributes("title=Example highlight=3").annotated


@pytest.mark.parametrize(
    "text",
    [
        "docs-test=run timeuot=30",
        "docs-test=sprint",
        "docs-test=run timeout=soon",
        "docs-test=run exit=later",
        'docs-test=run reason="unbalanced',
        # A duration has to be one, and an exit code has to be one.
        "docs-test=run timeout=0",
        "docs-test=run timeout=-5",
        "docs-test=run exit=-1",
        "docs-test=run exit=999",
        "docs-test=run timeout=301",
    ],
)
def test_broken_annotations_are_errors(text: str) -> None:
    with pytest.raises(AnnotationError):
        parse_attributes(text)


@pytest.mark.parametrize("text", ["docs_test=run", "DOCS-TEST=run", "docs-tests=run"])
def test_a_misspelled_gateway_key_is_a_typo_not_metadata(text: str) -> None:
    """On its own, with nothing correctly spelled beside it to give it away."""
    with pytest.raises(AnnotationError, match="did you mean"):
        parse_attributes(text)


@pytest.mark.parametrize("text", ["expected=4", "exec=true", "timout=30", "name=setup"])
def test_another_tool_s_metadata_is_not_our_typo(text: str) -> None:
    """Fuzzy-matching every key would make `expected=` a misspelled `expect`."""
    assert not parse_attributes(text).annotated


def test_a_misspelled_key_beside_one_of_ours_is_still_an_error() -> None:
    with pytest.raises(AnnotationError, match="timout"):
        parse_attributes("docs-test=run timout=30")


def test_the_longest_allowed_timeout_is_accepted() -> None:
    assert parse_attributes(f"docs-test=run timeout={MAX_TIMEOUT}").timeout == MAX_TIMEOUT


def _parse(text: str, config: Config | None = None):
    parser = FenceParser(
        config=config or Config(),
        evaluators={kind: (lambda example: None) for kind in ("shell", "json", "yaml")},
    )
    document = Document(text, "/repo/doc.md")
    return list(parser(document))


def test_only_checked_languages_become_examples() -> None:
    regions = _parse(
        "```bash\ntrue\n```\n\n```csharp\nvar x = 1;\n```\n\n```json\n{}\n```\n"
    )
    assert [region.parsed.language for region in regions] == ["bash", "json"]


def test_block_records_where_it_came_from() -> None:
    (region,) = _parse("intro\n\nmore\n\n```yaml\na: 1\n```\n")
    block = region.parsed
    assert isinstance(block, Block)
    assert block.line == 5
    assert block.source == "a: 1\n"


def test_annotation_on_an_unchecked_language_is_reported() -> None:
    (region,) = _parse("```text docs-test=run\nnot shell\n```\n")
    assert "are not checked" in region.evaluator(None)


def test_run_on_a_data_block_is_reported() -> None:
    (region,) = _parse("```json docs-test=run\n{}\n```\n")
    assert "only applies to shell blocks" in region.evaluator(None)


def test_broken_annotation_is_reported_in_place() -> None:
    (region,) = _parse("```bash docs-test=run timeuot=30\ntrue\n```\n")
    message = region.evaluator(None)
    assert "doc.md:1" in message
    assert "timeuot" in message


def test_nested_fences_do_not_confuse_the_lexer() -> None:
    text = "````markdown\n```bash\ntrue\n```\n````\n\n```json\n{}\n```\n"
    assert [region.parsed.language for region in _parse(text)] == ["bash", "json"]
