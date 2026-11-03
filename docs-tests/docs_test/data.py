"""Checks for data examples: a documented payload has to be parseable.

A block that is deliberately a fragment says so::

    ```json docs-test=skip reason="response fields, not a whole payload"
    ```
"""

from __future__ import annotations

import json
import re

import yaml

from .fences import Block

#: ``//`` and ``/* */`` comments, which are legal in the ``jsonc`` dialect.
LINE_COMMENT = re.compile(r"(^|\s)//[^\n]*")
BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.DOTALL)
TRAILING_COMMA = re.compile(r",(\s*[}\]])")


def _strip_jsonc(source: str) -> str:
    source = BLOCK_COMMENT.sub("", source)
    source = LINE_COMMENT.sub(r"\1", source)
    return TRAILING_COMMA.sub(r"\1", source)


def _hint(block: Block, kind: str, detail: str) -> str:
    return (
        f"{block.where()}: this ```{block.language} example is not valid {kind}\n\n"
        f"{detail}\n\n"
        "If it is a fragment rather than a whole document, annotate it "
        f'```{block.language} docs-test=skip reason="fragment"'
    )


def evaluate_json(example) -> str | None:
    block: Block = example.parsed
    source = block.source
    if block.language == "jsonc":
        source = _strip_jsonc(source)
    if not source.strip():
        return _hint(block, "JSON", "the block is empty")
    try:
        json.loads(source)
    except json.JSONDecodeError as error:
        if _is_json_lines(source):
            return None
        return _hint(block, "JSON", f"{error.msg} at line {error.lineno}, column {error.colno}")
    return None


def _is_json_lines(source: str) -> bool:
    """Is every line its own JSON document?

    Contract documents list alternative request bodies one per line ("exactly one
    of userId or email"), which is a legitimate example, not a broken payload.
    """
    lines = [line for line in source.splitlines() if line.strip()]
    if len(lines) < 2:
        return False
    for line in lines:
        try:
            json.loads(line)
        except json.JSONDecodeError:
            return False
    return True


def evaluate_yaml(example) -> str | None:
    block: Block = example.parsed
    if not block.source.strip():
        return _hint(block, "YAML", "the block is empty")
    try:
        # Documented YAML is often a multi-document snippet; accept either shape.
        list(yaml.safe_load_all(block.source))
    except yaml.YAMLError as error:
        return _hint(block, "YAML", str(error))
    return None
