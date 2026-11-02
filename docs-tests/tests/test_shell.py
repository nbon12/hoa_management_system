"""Shell examples: syntax, claims, and execution."""

from __future__ import annotations

from types import SimpleNamespace

import pytest

from docs_test import claims, shell
from docs_test.fences import Attributes, Block
from docs_test.runtime import configure, runtime


def block(source: str, **attributes) -> Block:
    return Block(
        path="docs/example.md",
        line=1,
        language="bash",
        source=source,
        attributes=Attributes(**attributes),
    )


def example(source: str, **attributes) -> SimpleNamespace:
    return SimpleNamespace(parsed=block(source, **attributes))


# ── Syntax ────────────────────────────────────────────────────────────────────


def test_valid_shell_passes() -> None:
    assert shell.check_syntax(block('for f in a b; do echo "$f"; done\n')) is None


def test_broken_shell_is_reported() -> None:
    message = shell.check_syntax(block("if [ -f foo ]\necho nope\n"))
    assert message is not None
    assert "not valid bash" in message
    assert "docs/example.md:1" in message


def test_placeholders_are_not_redirections() -> None:
    assert shell.normalise("tofu apply -var name=<your-name>") == "tofu apply -var name=PLACEHOLDER"
    assert shell.check_syntax(block("tofu plan -var bucket=<bucket-name>\n")) is None


def test_heredocs_survive_normalisation() -> None:
    assert shell.check_syntax(block("cat <<'EOF' > /tmp/x\nhello\nEOF\n")) is None


# ── Claims ────────────────────────────────────────────────────────────────────


def test_real_claims_hold() -> None:
    assert shell.check_claims(block("npm run test:ci\n")) == []
    assert shell.check_claims(block("dotnet test HOAManagementCompany.Tests\n")) == []
    assert shell.check_claims(block("docker compose up -d db\n")) == []
    assert shell.check_claims(block("bash scripts/docs-test.sh --all\n")) == []


def test_missing_npm_script_is_reported() -> None:
    (finding,) = shell.check_claims(block("npm run lint\n"))
    assert "no 'lint' script" in finding


def test_missing_dotnet_project_is_reported() -> None:
    (finding,) = shell.check_claims(block("dotnet run --project NoSuchApp\n"))
    assert "NoSuchApp" in finding


def test_unknown_compose_service_is_reported() -> None:
    (finding,) = shell.check_claims(block("docker compose exec nosuchservice psql\n"))
    assert "docker-compose.yaml does not define" in finding


def test_compose_flags_are_not_services() -> None:
    assert shell.check_claims(block("docker compose up -d --wait\n")) == []


def test_unknown_workflow_is_reported() -> None:
    (finding,) = shell.check_claims(block("gh workflow run nope.yml\n"))
    assert "nope.yml" in finding


def test_missing_path_is_reported() -> None:
    (finding,) = shell.check_claims(block("cat specs/999-nothing/spec.md\n"))
    assert "specs/999-nothing/spec.md" in finding


def test_elided_paths_are_shapes_not_claims() -> None:
    assert shell.check_claims(block("cat specs/…/tasks.md\n")) == []
    assert shell.check_claims(block("cat specs/.../tasks.md\n")) == []


def test_commands_are_not_mistaken_for_paths() -> None:
    assert shell.check_claims(block('echo "FAIL: unstamped" && grep -q __API_ORIGIN__ out\n')) == []


def test_cwd_is_a_base_for_relative_claims() -> None:
    assert shell.check_claims(block("node scripts/stamp-headers.mjs\n")) != []
    assert shell.check_claims(block("node scripts/stamp-headers.mjs\n", cwd="neko-hoa")) == []


def test_repo_root_claims_still_resolve_under_a_cwd() -> None:
    assert shell.check_claims(block("cat docs-tests/README.md\n", cwd="neko-hoa")) == []


def test_generated_paths_are_not_claims() -> None:
    """git ignores them, so documentation may name them and we cannot verify them."""
    assert claims.path_exists("coverage/report.xml")
    assert claims.path_exists("HOAManagementCompany/appsettings.Secrets.json")
    assert not claims.path_exists("HOAManagementCompany/NoSuchFile.cs")


# ── Execution ─────────────────────────────────────────────────────────────────


def test_a_runnable_block_runs() -> None:
    assert shell.evaluate(example("test -f docs-tests/docs-test.toml\n", mode="run")) is None


def test_the_expected_exit_code_is_honoured() -> None:
    assert shell.evaluate(example("exit 3\n", mode="run", exit_code=3)) is None
    message = shell.evaluate(example("exit 3\n", mode="run"))
    assert "exited 3, expected 0" in message


def test_expected_output_is_checked() -> None:
    assert shell.evaluate(example('echo "hello docs"\n', mode="run", expect="hello docs")) is None
    assert "does not contain" in shell.evaluate(
        example("echo goodbye\n", mode="run", expect="hello docs")
    )


def test_a_slow_block_times_out() -> None:
    message = shell.evaluate(example("sleep 5\n", mode="run", timeout=1))
    assert "still running after 1s" in message


def test_denied_commands_are_never_run() -> None:
    message = shell.evaluate(example("rm -rf /tmp/docs-test-should-not-happen\n", mode="run"))
    assert "denied command pattern" in message


def test_missing_tools_skip_rather_than_fail() -> None:
    with pytest.raises(pytest.skip.Exception):
        shell.evaluate(example("nope --version\n", mode="run", needs=("definitely-not-a-tool",)))


def test_no_exec_skips_execution() -> None:
    configure(execute=False)
    try:
        with pytest.raises(pytest.skip.Exception):
            shell.evaluate(example('echo "would have run"\n', mode="run"))
    finally:
        configure(execute=True)
    assert runtime().execute
