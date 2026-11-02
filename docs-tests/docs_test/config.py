"""Configuration for the documentation test suite.

Defaults live in ``docs-tests/docs-test.toml`` so doc authors can change scope
without touching the harness.
"""

from __future__ import annotations

import re
import tomllib
from dataclasses import dataclass, field
from functools import lru_cache
from pathlib import Path


def repo_root() -> Path:
    """The repository root, derived from this file's location."""
    return Path(__file__).resolve().parents[2]


DEFAULT_CONFIG_PATH = repo_root() / "docs-tests" / "docs-test.toml"


def _glob_to_regex(pattern: str) -> re.Pattern[str]:
    """Translate a forward-slash glob into a regex, with ``**`` crossing directories.

    ``fnmatch`` treats ``*`` as matching ``/`` too, which makes ``specs/*/spec.md``
    useless here, so the translation is done by hand.
    """
    out = ["^"]
    i = 0
    while i < len(pattern):
        char = pattern[i]
        if pattern.startswith("**/", i):
            out.append("(?:[^/]+/)*")
            i += 3
        elif pattern.startswith("**", i):
            out.append(".*")
            i += 2
        elif char == "*":
            out.append("[^/]*")
            i += 1
        elif char == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(char))
            i += 1
    out.append("$")
    return re.compile("".join(out))


@lru_cache(maxsize=None)
def _compiled(pattern: str) -> re.Pattern[str]:
    return _glob_to_regex(pattern)


def glob_match(relative_path: str, pattern: str) -> bool:
    """Does ``relative_path`` (repo-relative, forward slashes) match ``pattern``?"""
    return _compiled(pattern).match(relative_path) is not None


def matches_any(relative_path: str, patterns: tuple[str, ...]) -> bool:
    return any(glob_match(relative_path, pattern) for pattern in patterns)


#: Fence language → check kind. A language that is not here is not checked.
DEFAULT_LANGUAGES: dict[str, str] = {
    "bash": "shell",
    "sh": "shell",
    "shell": "shell",
    "zsh": "shell",
    "console": "shell",
    "json": "json",
    "jsonc": "json",
    "yaml": "yaml",
    "yml": "yaml",
}

#: Commands a ``docs-test=run`` block may never contain, however it is annotated:
#: a runnable example has to be safe on any checkout. In code rather than in
#: docs-test.toml so that editing the config cannot make the gate destructive.
DEFAULT_DENYLIST: tuple[str, ...] = (
    r"rm +-[a-zA-Z]*[rf]",
    r"git +(push|reset +--hard|clean +-[a-zA-Z]*f)",
    r"(tofu|terraform) +(apply|destroy)",
    r"gcloud +[a-z-]+ +(delete|remove)",
    r"docker +compose +down +[^\n]*-v",
    r"curl[^\n|]*\| *(ba)?sh",
    r"(^|\s)(shutdown|reboot|mkfs|dd +if=)",
    r"npm +publish",
    r"dotnet +ef +database +drop",
)


@dataclass(frozen=True)
class Config:
    """Resolved documentation test configuration."""

    include: tuple[str, ...] = ("**/*.md",)
    exclude: tuple[str, ...] = ()
    #: Documents that record a moment in time (``tasks.md``, handoffs, patch notes).
    #: Constitution section 11 exempts them from staying current, so their path
    #: claims are not checked. Their examples still are.
    point_in_time: tuple[str, ...] = ()
    #: Path claims that are deliberately hypothetical (``infra/insecure.tf`` in a
    #: "here is what a finding looks like" walkthrough, for example).
    ignore_paths: tuple[str, ...] = ()
    check_anchors: bool = True
    exec_denylist: tuple[str, ...] = DEFAULT_DENYLIST
    exec_default_timeout: int = 60
    languages: dict[str, str] = field(default_factory=lambda: dict(DEFAULT_LANGUAGES))

    def in_scope(self, relative_path: str) -> bool:
        return matches_any(relative_path, self.include) and not matches_any(
            relative_path, self.exclude
        )

    def is_point_in_time(self, relative_path: str) -> bool:
        return matches_any(relative_path, self.point_in_time)

    def path_claim_ignored(self, claim: str) -> bool:
        return matches_any(claim, self.ignore_paths)

    def language_kind(self, language: str) -> str | None:
        """Map a fence language onto a check kind (``shell``, ``json``, ``yaml``)."""
        return self.languages.get(language.lower())


def load_config(path: Path | None = None) -> Config:
    """Read ``docs-test.toml``. A missing file yields the dataclass defaults."""
    path = path or DEFAULT_CONFIG_PATH
    if not path.exists():
        return Config()

    raw = tomllib.loads(path.read_text(encoding="utf-8"))
    corpus = raw.get("corpus", {})
    references = raw.get("references", {})
    shell = raw.get("shell", {})
    languages = dict(DEFAULT_LANGUAGES)
    if "languages" in raw:
        languages = {
            name.lower(): kind for kind, names in raw["languages"].items() for name in names
        }

    defaults = Config()
    return Config(
        include=tuple(corpus.get("include", defaults.include)),
        exclude=tuple(corpus.get("exclude", ())),
        point_in_time=tuple(corpus.get("point_in_time", ())),
        ignore_paths=tuple(references.get("ignore_paths", ())),
        check_anchors=bool(references.get("check_anchors", defaults.check_anchors)),
        exec_denylist=DEFAULT_DENYLIST + tuple(shell.get("extra_denylist", ())),
        exec_default_timeout=int(shell.get("default_timeout", defaults.exec_default_timeout)),
        languages=languages,
    )


@lru_cache(maxsize=1)
def config() -> Config:
    """The process-wide configuration."""
    return load_config()
