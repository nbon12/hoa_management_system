"""JSON and YAML examples."""

from __future__ import annotations

from types import SimpleNamespace

from docs_test import data
from docs_test.fences import Attributes, Block


def example(source: str, language: str = "json") -> SimpleNamespace:
    return SimpleNamespace(
        parsed=Block(
            path="docs/example.md", line=1, language=language, source=source, attributes=Attributes()
        )
    )


def test_valid_json() -> None:
    assert data.evaluate_json(example('{"a": 1}')) is None


def test_invalid_json_is_reported() -> None:
    message = data.evaluate_json(example('{"a": 1'))
    assert "not valid JSON" in message
    assert "docs/example.md:1" in message


def test_a_fragment_is_reported_with_the_annotation_to_use() -> None:
    message = data.evaluate_json(example('"Storage": {"BucketName": "hoa"}'))
    assert "docs-test=skip" in message


def test_alternative_payloads_one_per_line() -> None:
    assert data.evaluate_json(example('{"userId": "1"}\n{"email": "a@b.c"}\n')) is None


def test_json_lines_still_have_to_parse() -> None:
    assert data.evaluate_json(example('{"userId": "1"}\n{"email": \n')) is not None


def test_jsonc_comments_and_trailing_commas() -> None:
    source = '// which environment\n{\n  "enabled": true, /* inline */\n}\n'
    assert data.evaluate_json(example(source, language="jsonc")) is None


def test_empty_block() -> None:
    assert "empty" in data.evaluate_json(example("\n"))


def test_valid_yaml() -> None:
    assert data.evaluate_yaml(example("services:\n  db:\n    image: postgres:17\n", "yaml")) is None


def test_multi_document_yaml() -> None:
    assert data.evaluate_yaml(example("a: 1\n---\nb: 2\n", "yaml")) is None


def test_invalid_yaml_is_reported() -> None:
    message = data.evaluate_yaml(example("a:\n b: 1\n  c: 2\n", "yaml"))
    assert "not valid YAML" in message
