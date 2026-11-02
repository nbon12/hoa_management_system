"""The scope rules: which documents and which claims the suite looks at."""

from __future__ import annotations

import pytest

from docs_test.config import DEFAULT_LANGUAGES, Config, glob_match, load_config


@pytest.mark.parametrize(
    "path,pattern,expected",
    [
        ("README.md", "**/*.md", True),
        ("specs/009/spec.md", "**/*.md", True),
        ("specs/009/tasks.md", "specs/**/tasks.md", True),
        ("specs/009/sub/tasks.md", "specs/**/tasks.md", True),
        ("specs/tasks.md", "specs/**/tasks.md", True),
        ("docs/tasks.md", "specs/**/tasks.md", False),
        (".cursor/commands/x.md", ".cursor/**", True),
        ("cursor/commands/x.md", ".cursor/**", False),
        ("a/b/c.md", "*/*.md", False),
        ("neko-hoa/.angular/cache/x", "neko-hoa/.angular/**", True),
    ],
)
def test_glob_match(path: str, pattern: str, expected: bool) -> None:
    assert glob_match(path, pattern) is expected


def test_in_scope_applies_exclude_after_include() -> None:
    config = Config(include=("**/*.md",), exclude=("wireframes/**",))
    assert config.in_scope("specs/009/spec.md")
    assert not config.in_scope("wireframes/bundle/spec.md")
    assert not config.in_scope("README.rst")


def test_missing_config_file_falls_back_to_defaults(tmp_path) -> None:
    config = load_config(tmp_path / "absent.toml")
    assert config.include == ("**/*.md",)
    assert config.languages == DEFAULT_LANGUAGES
    assert config.exec_denylist, "the denylist must not be empty by default"


def test_config_without_sections_keeps_code_defaults(tmp_path) -> None:
    """A config that only narrows the corpus still checks every language."""
    path = tmp_path / "docs-test.toml"
    path.write_text('[corpus]\ninclude = ["docs/**/*.md"]\n', encoding="utf-8")

    config = load_config(path)

    assert config.include == ("docs/**/*.md",)
    assert config.language_kind("bash") == "shell"
    assert config.language_kind("json") == "json"
    assert config.exec_denylist


def test_extra_denylist_adds_to_the_baseline(tmp_path) -> None:
    path = tmp_path / "docs-test.toml"
    path.write_text('[shell]\nextra_denylist = ["kubectl +delete"]\n', encoding="utf-8")

    config = load_config(path)

    assert "kubectl +delete" in config.exec_denylist
    assert len(config.exec_denylist) > 1


def test_fixture_config_is_the_one_the_fixtures_expect(fixture_config: Config) -> None:
    assert fixture_config.in_scope("docs-tests/tests/fixtures/passing.md")
    assert not fixture_config.in_scope("README.md")
    assert fixture_config.is_point_in_time("docs-tests/tests/fixtures/point-in-time.md")
    assert fixture_config.path_claim_ignored("docs/deliberately-hypothetical.md")
