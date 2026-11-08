"""Fixtures for the harness's own tests."""

from __future__ import annotations

from pathlib import Path

import pytest

from docs_test.config import load_config

FIXTURES = Path(__file__).parent / "fixtures"


@pytest.fixture(scope="session")
def fixture_config():
    """The configuration the fixture documents are checked under."""
    return load_config(FIXTURES / "docs-test.toml")


@pytest.fixture(scope="session")
def fixtures_dir() -> Path:
    return FIXTURES
