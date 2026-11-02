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

#: Check kinds an evaluator exists for. A ``[languages]`` table may only map onto
#: these.
KINDS: frozenset[str] = frozenset({"shell", "json", "yaml"})

#: Commands a ``docs-test=run`` block may never contain, however it is annotated:
#: a runnable example has to be safe on any checkout, on a laptop as much as in
#: CI. Matched case-insensitively against the block's source. In code rather than
#: in docs-test.toml so that editing the config cannot make the gate destructive.
#:
#: This is a backstop against a careless example, not a sandbox: it cannot stop a
#: determined one. Execution safety rests on the gate running without secrets and
#: on review of what gets annotated; the denylist catches the obvious mistakes.
DEFAULT_DENYLIST: tuple[str, ...] = (
    # Deleting things
    r"\brm\s+(-[a-z]*[rf]|--(recursive|force))",
    r"\bfind\s+[^\n]*(-delete|-exec\s+rm)",
    r"\b(shutdown|reboot|mkfs\S*|dd\s+if=)",
    # Rewriting or publishing history
    r"\bgit\s+(push|apply|am|reset\s+--hard|clean\s+-[a-z]*f|branch\s+-D|tag\s+-d|"
    r"checkout\s+--|restore\s)",
    r"\b(npm|yarn|pnpm)\s+(un)?publish",
    r"\bdotnet\s+nuget\s+push",
    r"\bgh\s+(release\s+(create|delete)|repo\s+delete|pr\s+(merge|close)|issue\s+close)",
    # Changing infrastructure or clusters
    r"\b(tofu|terraform)\s+(apply|destroy|import|state\s+(rm|push))",
    r"\bkubectl\s+(apply|delete|drain|patch|scale|replace)",
    r"\b(gcloud|aws|az)\s+[a-z0-9-]+\s+[a-z0-9-]*\s*(delete|destroy|remove|rm)\b",
    r"\bdocker\s+(compose\s+down[^\n]*-v|rm\b|rmi\b|volume\s+rm|system\s+prune)",
    # Dropping data
    r"\bdotnet\s+ef\s+database\s+(drop|update)",
    r"\b(drop|truncate)\s+(table|database|schema)\b",
    # Running something the network just handed us
    r"\b(curl|wget)[^\n|]*\|\s*(ba|z|k)?sh",
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
    """Read ``docs-test.toml``.

    With no argument the repository's own config is read, and the dataclass
    defaults stand in when it is absent. A path passed explicitly — ``pytest
    --docs-config`` — has to exist: falling back to the defaults there would
    change the scope of the run while still reporting green.
    """
    if path is None:
        path = DEFAULT_CONFIG_PATH
        if not path.exists():
            return Config()
    elif not path.exists():
        raise FileNotFoundError(f"docs-test config not found: {path}")

    raw = tomllib.loads(path.read_text(encoding="utf-8"))
    corpus = raw.get("corpus", {})
    references = raw.get("references", {})
    shell = raw.get("shell", {})

    # A `[languages]` table adds to the defaults; replacing them wholesale would
    # quietly stop checking most of the corpus.
    languages = dict(DEFAULT_LANGUAGES)
    for kind, names in raw.get("languages", {}).items():
        if kind not in KINDS:
            raise ValueError(
                f"{path}: [languages] maps onto {kind!r}, which nothing checks; "
                f"it has to be one of {', '.join(sorted(KINDS))}"
            )
        for name in names:
            languages[name.lower()] = kind

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
