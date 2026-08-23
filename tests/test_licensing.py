"""第三者素材のライセンス表記が抜けていないかを見る。

アバターデモは Unity Technologies Japan のユニティちゃんを使うため、
ユニティちゃんライセンス条項（UCL）が適用される。UCL は二次創作物を公開・頒布
する際に UCL ロゴかライセンス表記のいずれかの表示を求めている。

レンダリング結果をリポジトリに置く可能性があるので、
- 表記の文面がドキュメントに残っていること
- アバターの実体を追跡しない設定が外れていないこと
をテストで固定しておく。
"""

from __future__ import annotations

from pathlib import Path

import pytest

from harness.paths import REPO_ROOT

README = REPO_ROOT / "README.md"
DEMO_DOC = REPO_ROOT / "docs" / "avatar-demo.md"
GITIGNORE = REPO_ROOT / ".gitignore"

# 03Indication of License_JP_UCL2.0.pdf に載っている表記そのもの
UCL_NOTICE_LINES = (
    "この作品はユニティちゃんライセンス条項の元に提供されています",
    "© Unity Technologies Japan/UCL",
)

# デモが取得するアバターの実体が置かれる場所。ここを追跡すると
# アセットデータの再配布になり、ライセンス関連ファイル一式の同梱が必要になる。
DEMO_DIR_NAME = ".demo"

# 追跡してはいけない拡張子。デモの生成物が紛れ込んでいないかの保険。
MODEL_SUFFIXES = (".fbx", ".blend", ".vrm", ".unitypackage")


@pytest.fixture(scope="module")
def readme() -> str:
    return README.read_text(encoding="utf-8")


@pytest.fixture(scope="module")
def demo_doc() -> str:
    return DEMO_DOC.read_text(encoding="utf-8")


@pytest.mark.parametrize("line", UCL_NOTICE_LINES)
def test_readme_carries_ucl_notice(readme: str, line: str) -> None:
    assert line in readme, (
        f"README.md に UCL のライセンス表記がありません: {line!r}\n"
        "デモのレンダリング結果を掲載する場合に必要です。"
    )


@pytest.mark.parametrize("line", UCL_NOTICE_LINES)
def test_demo_doc_carries_ucl_notice(demo_doc: str, line: str) -> None:
    assert line in demo_doc, f"docs/avatar-demo.md に UCL のライセンス表記がありません: {line!r}"


def test_demo_directory_is_ignored() -> None:
    entries = {
        line.strip()
        for line in GITIGNORE.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.strip().startswith("#")
    }
    assert f"{DEMO_DIR_NAME}/" in entries, (
        f".gitignore に {DEMO_DIR_NAME}/ がありません。"
        "アバターの実体が追跡対象になると、アセットデータの再配布になります。"
    )


def test_no_model_data_is_tracked() -> None:
    """モデルデータがリポジトリに紛れ込んでいないことを見る。"""
    tracked = [
        path
        for path in REPO_ROOT.rglob("*")
        if path.is_file()
        and path.suffix.lower() in MODEL_SUFFIXES
        and ".git" not in path.parts
        and DEMO_DIR_NAME not in path.parts
        and ".cache" not in path.parts
    ]
    assert not tracked, "モデルデータがリポジトリ内にあります: " + ", ".join(
        str(p.relative_to(REPO_ROOT)) for p in tracked
    )
