"""Run-time switches the pytest plugin sets from the command line."""

from __future__ import annotations

from dataclasses import dataclass

from .config import Config, config


@dataclass
class Runtime:
    #: Whether ``docs-test=run`` blocks are actually executed. ``--docs-no-exec``
    #: turns this off for a fast, side-effect-free pass.
    execute: bool = True
    #: Resolved on first use rather than at import, so that a malformed
    #: docs-test.toml surfaces as a readable error from the plugin instead of
    #: breaking the import and taking the whole plugin with it.
    _config: Config | None = None

    @property
    def config(self) -> Config:
        if self._config is None:
            self._config = config()
        return self._config


_RUNTIME = Runtime()


def runtime() -> Runtime:
    return _RUNTIME


def configure(*, execute: bool | None = None, cfg: Config | None = None) -> Runtime:
    if execute is not None:
        _RUNTIME.execute = execute
    if cfg is not None:
        _RUNTIME._config = cfg
    return _RUNTIME
