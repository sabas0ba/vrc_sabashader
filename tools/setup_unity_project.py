#!/usr/bin/env python3
"""Unity でのコンパイル検証用プロジェクトを組み立てる。

`.ci/UnityProject` の雛形に、このリポジトリのパッケージと Shader Core を
埋め込みパッケージとして配置する。CI から使うが、Unity を持っている人が
手元で同じ検証をするのにも使える。

    python tools/setup_unity_project.py
    # 出来た .ci/UnityProject を Unity で開く（または下記を実行）
    # <Unity> -batchmode -quit -projectPath .ci/UnityProject \\
    #   -executeMethod SabaShader.CI.ShaderCompileChecker.RunBatch
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PROJECT_DIR = REPO_ROOT / ".ci" / "UnityProject"
PACKAGE_DIR = REPO_ROOT / "Packages" / "io.github.sabas0ba.sabashader"

SHADERCORE_URL = "https://github.com/lilxyzw/Shader-Core.git"
# tests/harness/paths.py と同じコミットに固定する
SHADERCORE_COMMIT = "0a0b2fef78fc3b0438b58f443a2e75210db83ec4"
NONTOON_URL = "https://github.com/lilxyzw/NonToon.git"
# release 0.1.3。tests/harness/paths.py と同じコミットに固定する
NONTOON_COMMIT = "130bea3e6be5183b4fceb60df0062d38ef98067c"
TRANSFORMATION_BANK_ID = "io.github.sabas0ba.transformationbank"


def clone_pinned_package(destination: Path, url: str, commit: str, display_name: str) -> None:
    marker = destination / ".saba-source-commit"
    if (destination / "package.json").is_file() and marker.is_file() and marker.read_text(
        encoding="utf-8"
    ).strip() == commit:
        print(f"{display_name} は配置済み: {destination}")
        return

    if destination.exists():
        shutil.rmtree(destination)
    destination.mkdir(parents=True)

    # Windows から Podman の bind mount を使う場合、コンテナ内の Git からは
    # 初期化直後の作業ツリーが別所有者に見える。global 設定は変更せず、取得中の
    # リポジトリだけを safe.directory としてコマンド単位で許可する。
    git_at_destination = ["git", "-c", f"safe.directory={destination}", "-C", str(destination)]
    commands = [
        ["git", "init", "--quiet", str(destination)],
        [*git_at_destination, "remote", "add", "origin", url],
        [*git_at_destination, "fetch", "--quiet", "--depth", "1", "origin", commit],
        [*git_at_destination, "checkout", "--quiet", "FETCH_HEAD"],
    ]
    for command in commands:
        subprocess.run(command, check=True, timeout=300)

    shutil.rmtree(destination / ".git", ignore_errors=True)
    marker.write_text(commit + "\n", encoding="utf-8")
    print(f"{display_name} を配置しました: {destination}")


def clone_shadercore(destination: Path) -> None:
    clone_pinned_package(destination, SHADERCORE_URL, SHADERCORE_COMMIT, "Shader Core")


def clone_nontoon(destination: Path) -> None:
    clone_pinned_package(destination, NONTOON_URL, NONTOON_COMMIT, "NonToon")


def copy_package(destination: Path) -> None:
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(PACKAGE_DIR, destination)
    print(f"パッケージを配置しました: {destination}")


def copy_samples(project: Path) -> None:
    """全 UPM sample を Package Manager と同じ配置へ展開する。"""
    package = json.loads((PACKAGE_DIR / "package.json").read_text(encoding="utf-8"))
    sample_root = project / "Assets" / "Samples" / package["displayName"]
    if sample_root.exists():
        shutil.rmtree(sample_root)

    for sample in package.get("samples", []):
        source = PACKAGE_DIR / sample["path"]
        destination = (
            sample_root
            / package["version"]
            / sample["displayName"]
        )

        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(source, destination)
        print(f"{sample['displayName']} を配置しました: {destination}")


def enable_modules(project: Path) -> None:
    """パッケージ内のモジュールを全シェーダーで有効にする。

    Shader Core はシェーダーごとに有効なモジュールを ProjectSettings に持ち、
    既定値は「シェーダーと同じディレクトリにあるもの」だけ。モジュールを
    別ディレクトリに置いている本パッケージでは、明示的に有効化しないと
    Unity 側の検証がモジュールを一切通らない（気付けないまま緑になる）。
    """
    import json
    import re

    saba_modules = sorted(
        json.loads(path.read_text(encoding="utf-8"))["uniqueID"]
        for path in (PACKAGE_DIR / "Modules").rglob("*.scmodule")
    )
    saba_shaders = sorted(
        re.search(r'^\s*Shader\s+"([^"]+)"', path.read_text(encoding="utf-8"), re.MULTILINE).group(1)
        for path in (PACKAGE_DIR / "Shaders").rglob("*.scshader")
    )
    nontoon = project / "Packages" / "jp.lilxyzw.nontoon"
    nontoon_modules = sorted(
        json.loads(path.read_text(encoding="utf-8"))["uniqueID"]
        for path in (nontoon / "Shaders" / "Modules").rglob("*.scmodule")
    )
    nontoon_shaders = sorted(
        re.search(r'^\s*Shader\s+"([^"]+)"', path.read_text(encoding="utf-8"), re.MULTILINE).group(1)
        for path in (nontoon / "Shaders").glob("*.scshader")
    )
    if not saba_modules or not saba_shaders:
        return

    meta = project / "Packages" / "jp.lilxyzw.shadercore" / "Editor" / "ProjectSettings.cs.meta"
    guid_match = re.search(r"^guid:\s*(\w+)", meta.read_text(encoding="utf-8"), re.MULTILINE)
    if guid_match is None:
        raise SystemExit(f"Shader Core の ProjectSettings の GUID を読めません: {meta}")

    shader_modules = [(shader, saba_modules) for shader in saba_shaders]
    for shader in nontoon_shaders:
        modules = list(nontoon_modules)
        if shader == "NonToon":
            modules.append(TRANSFORMATION_BANK_ID)
        shader_modules.append((shader, sorted(set(modules))))

    entries = "\n".join(
        f"  - shadername: {shader}\n"
        + "    modules:\n"
        + "\n".join(f"    - {module}" for module in modules)
        + "\n    multiModules: []"
        for shader, modules in shader_modules
    )

    body = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_ObjectHideFlags: 53
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid_match.group(1)}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  shaderSettings:
{entries}
"""

    target = project / "ProjectSettings" / "jp.lilxyzw.shadercore.asset"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(body, encoding="utf-8")
    print("SabaShader と NonToon の検証用モジュール設定を作成しました")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=PROJECT_DIR)
    args = parser.parse_args()

    packages = args.project / "Packages"
    if not (packages / "manifest.json").is_file():
        print(f"雛形が見つかりません: {packages / 'manifest.json'}", file=sys.stderr)
        return 1

    copy_package(packages / PACKAGE_DIR.name)
    copy_samples(args.project)
    clone_shadercore(packages / "jp.lilxyzw.shadercore")
    clone_nontoon(packages / "jp.lilxyzw.nontoon")
    enable_modules(args.project)

    print(f"\n準備完了: {args.project}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
