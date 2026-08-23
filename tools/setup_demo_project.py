#!/usr/bin/env python3
"""アバターデモ用の Unity プロジェクトを .demo/ に組み立てる。

第三者のアバターとその派生データはリポジトリに含めない。このスクリプトは
取得元を commit SHA で固定して .demo/ の下に展開するだけで、生成される
マテリアル・シーン・レンダリング結果も含めて .demo/ は .gitignore 済み。

    python tools/setup_demo_project.py
    # 出来た .demo/UnityProject を Unity で開く（または下記を実行）
    # <Unity> -batchmode -projectPath .demo/UnityProject \\
    #   -executeMethod SabaShader.Demo.AvatarDemoScene.BuildBatch -logFile -

取得するアバターは Unity Technologies Japan が公開している
ユニティちゃんで、ユニティちゃんライセンス（UCL）が適用される。
条項全文とライセンスロゴはモデルと同じ subtree に同梱されており、
Assets/Avatar/UnityChanSD/License/ に展開される。

UCL はライセンスロゴの表示を求めている。.demo/ はリポジトリ管理外なので
再配布は発生しないが、**レンダリング結果を公開する場合はロゴ表示が必要**。
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from tools.setup_unity_project import clone_shadercore, PACKAGE_DIR, REPO_ROOT  # noqa: E402

DEMO_DIR = REPO_ROOT / ".demo"
PROJECT_DIR = DEMO_DIR / "UnityProject"
STAGING_DIR = DEMO_DIR / "_src"
TEMPLATE_DIR = REPO_ROOT / ".ci" / "UnityProject"
EDITOR_SOURCE_DIR = REPO_ROOT / "tools" / "demo" / "Editor"


@dataclass(frozen=True)
class AvatarSource:
    """取得元。commit を固定して sparse checkout する。"""

    name: str
    url: str
    commit: str
    # sparse-checkout に渡すパターン（--no-cone）
    sparse: tuple[str, ...]
    # (取得元の相対パス, Assets/Avatar/<name> 以下の配置先) の並び。
    # ディレクトリでもファイルでもよい。.meta は自動で一緒に運ぶ。
    copy: tuple[tuple[str, str], ...]
    license_note: str = ""
    # copy したあとに取り除くもの。このプロジェクトでコンパイルできない
    # 第三者シェーダーなどを持ち込まないために使う。
    prune: tuple[str, ...] = field(default_factory=tuple)


AVATARS: tuple[AvatarSource, ...] = (
    AvatarSource(
        name="UnityChanSD",
        url="https://github.com/unity3d-jp/UnityChanToonShaderVer2_Project.git",
        commit="add3a5afb97f7cc62393b6d63f91ce2525d3ebbb",
        sparse=(
            "Assets/UnityChan/SD_unitychan/Models/*",
            "Assets/Toon/Textures/utc_all2_light.*",
            "Assets/UnityChan/License/*",
        ),
        copy=(
            ("Assets/UnityChan/SD_unitychan/Models", "SD_unitychan/Models"),
            # FBX に付いてくる def_mat.mat はテクスチャ未設定なので、
            # ベースカラーのアトラスを別途持ってくる。
            # フォルダ内に色テクスチャが 1 枚だけになるようにしておくと、
            # AvatarDemoScene 側のフォールバックが一意に決まる。
            ("Assets/Toon/Textures/utc_all2_light.png", "SD_unitychan/utc_all2_light.png"),
            ("Assets/UnityChan/License", "License"),
        ),
        license_note="ユニティちゃんライセンス 2.0（条項とロゴは License/ に同梱）",
    ),
    AvatarSource(
        name="UnityChanCRS",
        url="https://github.com/unity3d-jp/unitychan-crs.git",
        commit="149acd484bae86c706da05d758a4d64bf817bb34",
        sparse=("Assets/UnityChan/CandyRockStar/*",),
        copy=(("Assets/UnityChan/CandyRockStar", "CandyRockStar"),),
        # 同梱シェーダーは Unity 2022.3 で通らない可能性があり、
        # 通ってもデモの比較対象にはしないので持ち込まない。
        # マテリアルはシェーダーを見失うので Standard に差し替えられる。
        prune=("CandyRockStar/Shader",),
        license_note="ユニティちゃんライセンス（このリポジトリには条項が同梱されていない）",
    ),
)


def run(command: list[str]) -> None:
    subprocess.run(command, check=True, timeout=1800)


def sparse_checkout(source: AvatarSource, destination: Path) -> None:
    """commit を固定した sparse checkout。blob はフィルタして必要な分だけ取る。"""
    marker = destination / ".checkout-commit"
    if marker.is_file() and marker.read_text(encoding="utf-8").strip() == source.commit:
        print(f"{source.name}: 取得済み ({source.commit[:12]})")
        return

    if destination.exists():
        shutil.rmtree(destination, onexc=_force_remove)
    destination.mkdir(parents=True)

    git = ["git", "-C", str(destination)]
    run(["git", "init", "--quiet", str(destination)])
    run(git + ["remote", "add", "origin", source.url])
    run(git + ["sparse-checkout", "init", "--no-cone"])
    run(git + ["sparse-checkout", "set", *source.sparse])
    run(git + ["fetch", "--quiet", "--depth", "1", "--filter=blob:none", "origin", source.commit])
    run(git + ["checkout", "--quiet", "FETCH_HEAD"])

    marker.write_text(source.commit + "\n", encoding="utf-8")
    print(f"{source.name}: 取得しました ({source.commit[:12]}) <- {source.url}")


def _force_remove(func, path, _exc):
    """git が作る読み取り専用ファイルを消せるようにする。"""
    Path(path).chmod(0o700)
    func(path)


def copy_with_meta(source: Path, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)

    if source.is_dir():
        if destination.exists():
            shutil.rmtree(destination)
        shutil.copytree(source, destination)
    else:
        shutil.copy2(source, destination)

    meta = source.with_name(source.name + ".meta")
    if meta.is_file():
        shutil.copy2(meta, destination.with_name(destination.name + ".meta"))


def place_avatar(source: AvatarSource, staging: Path, avatar_root: Path) -> None:
    destination = avatar_root / source.name
    if destination.exists():
        shutil.rmtree(destination)

    for relative, target in source.copy:
        origin = staging / relative
        if not origin.exists():
            print(f"  警告: 取得元に見つかりません: {relative}", file=sys.stderr)
            continue
        copy_with_meta(origin, destination / target)

    for relative in source.prune:
        for path in (destination / relative, destination / (relative + ".meta")):
            if path.is_dir():
                shutil.rmtree(path)
            elif path.is_file():
                path.unlink()

    print(f"  配置しました: {destination}")
    if source.license_note:
        print(f"  ライセンス: {source.license_note}")


def copy_template(project: Path) -> None:
    """Unity のバージョンと UPM の依存は .ci の検証プロジェクトに合わせる。"""
    for relative in ("Packages/manifest.json", "ProjectSettings/ProjectVersion.txt"):
        origin = TEMPLATE_DIR / relative
        if not origin.is_file():
            raise SystemExit(f"雛形が見つかりません: {origin}")
        target = project / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(origin, target)


def copy_editor_scripts(project: Path) -> None:
    destination = project / "Assets" / "Editor"
    if destination.exists():
        shutil.rmtree(destination)
    destination.mkdir(parents=True)

    for script in sorted(EDITOR_SOURCE_DIR.glob("*.cs")):
        shutil.copy2(script, destination / script.name)
        print(f"  Editor スクリプト: {script.name}")


def copy_package(destination: Path) -> None:
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(PACKAGE_DIR, destination)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=PROJECT_DIR)
    parser.add_argument(
        "--only",
        action="append",
        choices=[a.name for a in AVATARS],
        help="取得するアバターを絞る（既定は全部）",
    )
    args = parser.parse_args()

    project = args.project
    sources = [a for a in AVATARS if not args.only or a.name in args.only]

    print(f"雛形を用意します: {project}")
    copy_template(project)
    copy_editor_scripts(project)

    packages = project / "Packages"
    copy_package(packages / PACKAGE_DIR.name)
    clone_shadercore(packages / "jp.lilxyzw.shadercore")

    avatar_root = project / "Assets" / "Avatar"
    avatar_root.mkdir(parents=True, exist_ok=True)

    for source in sources:
        print(f"\n{source.name} を取得します")
        staging = STAGING_DIR / source.name
        sparse_checkout(source, staging)
        place_avatar(source, staging, avatar_root)

    print(
        "\n準備完了: "
        f"{project}\n"
        "ユニティちゃんライセンスの条項は "
        f"{avatar_root / 'UnityChanSD' / 'License'} にあります。\n"
        "レンダリング結果を公開する場合はライセンスロゴの表示が必要です。"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
