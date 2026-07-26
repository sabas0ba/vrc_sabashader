from __future__ import annotations

import sys
from pathlib import Path

import pytest

_TESTS_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(_TESTS_DIR))
sys.path.insert(0, str(_TESTS_DIR.parent))  # tools パッケージを import できるように


def pytest_addoption(parser: pytest.Parser) -> None:
    parser.addoption(
        "--update-goldens",
        action="store_true",
        default=False,
        help="比較する代わりにゴールデン画像を書き出す",
    )


@pytest.fixture(scope="session")
def update_goldens(pytestconfig: pytest.Config) -> bool:
    return bool(pytestconfig.getoption("--update-goldens"))
