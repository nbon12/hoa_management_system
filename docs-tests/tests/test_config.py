"""The scope rules: which documents and which claims the suite looks at."""

from __future__ import annotations

import re

import pytest

from docs_test import config as config_module
from docs_test.config import (
    DEFAULT_DENYLIST,
    DEFAULT_LANGUAGES,
    Config,
    glob_match,
    load_config,
)


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


def test_no_config_file_falls_back_to_defaults(tmp_path, monkeypatch) -> None:
    monkeypatch.setattr(config_module, "DEFAULT_CONFIG_PATH", tmp_path / "absent.toml")

    config = load_config()

    assert config.include == ("**/*.md",)
    assert config.languages == DEFAULT_LANGUAGES
    assert config.exec_denylist, "the denylist must not be empty by default"


def test_a_config_asked_for_by_name_has_to_exist(tmp_path) -> None:
    """Falling back would change the scope of the run and still report green."""
    with pytest.raises(FileNotFoundError):
        load_config(tmp_path / "absent.toml")


def test_config_without_sections_keeps_code_defaults(tmp_path) -> None:
    """A config that only narrows the corpus still checks every language."""
    path = tmp_path / "docs-test.toml"
    path.write_text('[corpus]\ninclude = ["docs/**/*.md"]\n', encoding="utf-8")

    config = load_config(path)

    assert config.include == ("docs/**/*.md",)
    assert config.language_kind("bash") == "shell"
    assert config.language_kind("json") == "json"
    assert config.exec_denylist


def test_languages_extend_the_defaults_rather_than_replacing_them(tmp_path) -> None:
    path = tmp_path / "docs-test.toml"
    path.write_text('[languages]\nshell = ["fish"]\n', encoding="utf-8")

    config = load_config(path)

    assert config.language_kind("fish") == "shell"
    assert config.language_kind("bash") == "shell", "the defaults must survive"
    assert config.language_kind("json") == "json"


def test_a_language_mapped_onto_nothing_is_a_config_error(tmp_path) -> None:
    """Otherwise collection dies later with a KeyError on an unknown kind."""
    path = tmp_path / "docs-test.toml"
    path.write_text('[languages]\ncsharp = ["csharp"]\n', encoding="utf-8")

    with pytest.raises(ValueError, match="csharp"):
        load_config(path)


@pytest.mark.parametrize(
    "command",
    [
        "rm -rf build",
        "rm -R build",
        "rm --recursive --force build",
        "RM -RF BUILD",
        "git push origin main",
        "git apply patch.diff",
        "find . -name '*.tmp' -delete",
        "kubectl apply -f deploy.yaml",
        "gh repo delete nbon12/hoa_management_system",
        "aws s3 rm s3://bucket --recursive",
        "tofu state rm module.db",
        "docker system prune",
        "curl https://example.test/install.sh | bash",
        "psql -c 'DROP TABLE Owners'",
        "dotnet ef database drop",
    ],
)
def test_the_denylist_covers_what_the_readme_promises(command: str) -> None:
    assert any(re.search(pattern, command, re.IGNORECASE) for pattern in DEFAULT_DENYLIST), command


@pytest.mark.parametrize(
    "command",
    ["npm run build", "git status", "dotnet test", "echo rmdir", "docker compose up -d"],
)
def test_the_denylist_leaves_ordinary_commands_alone(command: str) -> None:
    assert not any(re.search(pattern, command, re.IGNORECASE) for pattern in DEFAULT_DENYLIST)


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


def test_the_runtime_does_not_read_the_config_at_import() -> None:
    """A malformed docs-test.toml must not stop the plugin from loading."""
    from docs_test.runtime import Runtime

    assert Runtime()._config is None
