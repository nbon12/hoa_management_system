"""Run-time switches the pytest plugin sets from the command line."""

from __future__ import annotations

from dataclasses import dataclass, field

from .config import Config, config


@dataclass
class Runtime:
    #: Whether ``docs-test=run`` blocks are actually executed. ``--docs-no-exec``
    #: turns this off for a fast, side-effect-free pass.
    execute: bool = True
    config: Config = field(default_factory=config)


_RUNTIME = Runtime()


def runtime() -> Runtime:
    return _RUNTIME


def configure(*, execute: bool | None = None, cfg: Config | None = None) -> Runtime:
    if execute is not None:
        _RUNTIME.execute = execute
    if cfg is not None:
        _RUNTIME.config = cfg
    return _RUNTIME
