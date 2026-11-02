"""Turning fenced Markdown blocks into test cases.

A fence's info line carries the annotations::

    ```bash docs-test=run cwd=neko-hoa needs=npm timeout=120
    ```

GitHub only reads the first word of an info line as the language, so the
annotations are invisible in rendered documentation.
"""

from __future__ import annotations

import re
import shlex
from dataclasses import dataclass, field
from typing import Callable, Iterable

from sybil import Document, Region
from sybil.parsers.markdown.lexers import RawFencedCodeBlockLexer

from .config import Config
from .runtime import runtime

#: The info line: a language, then whitespace-separated ``key=value`` annotations.
INFO_PATTERN = re.compile(r"(?P<language>[A-Za-z0-9_+#.-]*)[ \t]*(?P<attributes>[^\n]*)$\n", re.MULTILINE)

MODES = {"run", "check", "skip"}
KEYS = {"docs-test", "reason", "cwd", "needs", "timeout", "exit", "expect"}


class AnnotationError(Exception):
    """An annotation the author clearly meant, written in a way we cannot honour."""


@dataclass(frozen=True)
class Attributes:
    """Annotations from one fence info line."""

    mode: str = "check"
    reason: str | None = None
    cwd: str | None = None
    needs: tuple[str, ...] = ()
    timeout: int | None = None
    exit_code: int = 0
    expect: str | None = None
    #: True when the author wrote any recognised annotation on this block.
    annotated: bool = False

    @property
    def runnable(self) -> bool:
        return self.mode == "run"

    @property
    def skipped(self) -> bool:
        return self.mode == "skip"


def parse_attributes(text: str) -> Attributes:
    """Parse the annotations trailing a fence language.

    An info line with no recognised key is left alone — other tooling is welcome to
    put its own metadata there. Once one recognised key is present the whole line is
    the harness's, and anything unrecognised in it is a mistake worth failing over.
    """
    text = text.strip()
    if not text:
        return Attributes()

    try:
        tokens = shlex.split(text)
    except ValueError as error:  # unbalanced quotes
        raise AnnotationError(f"cannot parse annotations {text!r}: {error}") from error

    pairs: list[tuple[str, str]] = []
    unknown: list[str] = []
    for token in tokens:
        key, _, value = token.partition("=")
        if key in KEYS:
            pairs.append((key, value))
        else:
            unknown.append(token)

    if not pairs:
        return Attributes()
    if unknown:
        raise AnnotationError(
            "unrecognised annotation(s) "
            + ", ".join(repr(token) for token in unknown)
            + f"; supported keys are {', '.join(sorted(KEYS))}"
        )

    values = dict(pairs)
    mode = values.get("docs-test", "check")
    if mode not in MODES:
        raise AnnotationError(f"docs-test={mode!r} is not one of {', '.join(sorted(MODES))}")

    timeout = values.get("timeout")
    exit_code = values.get("exit", "0")
    for name, number in (("timeout", timeout), ("exit", exit_code)):
        if number is not None and not re.fullmatch(r"-?\d+", number):
            raise AnnotationError(f"{name}={number!r} is not an integer")

    return Attributes(
        mode=mode,
        reason=values.get("reason"),
        cwd=values.get("cwd"),
        needs=tuple(part for part in re.split(r"[,\s]+", values.get("needs", "")) if part),
        timeout=int(timeout) if timeout is not None else None,
        exit_code=int(exit_code),
        expect=values.get("expect"),
        annotated=True,
    )


@dataclass(frozen=True)
class Block:
    """One fenced example, ready to be checked."""

    path: str
    line: int
    language: str
    source: str
    attributes: Attributes

    def where(self) -> str:
        return f"{self.path}:{self.line}"


Evaluator = Callable[["object"], str | None]


@dataclass
class FenceParser:
    """A Sybil parser that yields one region per checkable fenced block."""

    config: Config
    evaluators: dict[str, Evaluator] = field(default_factory=dict)

    def __post_init__(self) -> None:
        self._lexer = RawFencedCodeBlockLexer(INFO_PATTERN)

    def __call__(self, document: Document) -> Iterable[Region]:
        from . import corpus  # local import: avoids a cycle through config

        path = corpus.relative(document.path)
        for region in self._lexer(document):
            language = (region.lexemes.get("language") or "").strip().lower()
            raw_attributes = region.lexemes.get("attributes") or ""
            line = document.text.count("\n", 0, region.start) + 1

            try:
                attributes = parse_attributes(raw_attributes)
            except AnnotationError as error:
                region.parsed = raw_attributes
                region.evaluator = _failing(f"{path}:{line}: {error}")
                yield region
                continue

            kind = self.config.language_kind(language)
            if attributes.skipped:
                if not attributes.annotated:
                    continue
                region.parsed = attributes.reason
                region.evaluator = _skipping(attributes.reason or "docs-test=skip")
                yield region
                continue

            if kind is None:
                if attributes.annotated:
                    region.parsed = raw_attributes
                    region.evaluator = _failing(
                        f"{path}:{line}: ```{language or '(none)'} blocks are not checked, "
                        "so the annotation on this one would never run"
                    )
                    yield region
                continue

            if attributes.runnable and kind != "shell":
                region.parsed = raw_attributes
                region.evaluator = _failing(
                    f"{path}:{line}: docs-test=run only applies to shell blocks, not ```{language}"
                )
                yield region
                continue

            region.parsed = Block(
                path=path,
                line=line,
                language=language,
                source=str(region.lexemes["source"]),
                attributes=attributes,
            )
            region.evaluator = self.evaluators[kind]
            yield region


def _failing(message: str) -> Evaluator:
    def evaluate(example: object) -> str:
        return message

    return evaluate


def _skipping(reason: str) -> Evaluator:
    def evaluate(example: object) -> None:
        import pytest

        pytest.skip(reason)

    return evaluate


def default_parser(config: Config | None = None) -> FenceParser:
    """The parser used by the pytest plugin."""
    from . import data, shell

    config = config or runtime().config
    return FenceParser(
        config=config,
        evaluators={
            "shell": shell.evaluate,
            "json": data.evaluate_json,
            "yaml": data.evaluate_yaml,
        },
    )
