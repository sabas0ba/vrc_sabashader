#!/usr/bin/env python3
"""Unity でのコンパイル検証用プロジェクトを組み立てる。

`.ci/UnityProject` の雛形に、このリポジトリのパッケージと Shader Core を
埋め込みパッケージとして配置する。CI から使うが、Unity を持っている人が
手元で同じ検証をするのにも使える。

    python tools/setup_unity_project.py
    # 出来た .ci/UnityProject を Unity で開く（または下記を実行）
    # <Unity> -batchmode -quit -projectPath .ci/UnityProject \\
    #   -executeMethod SabaShader.CI.ShaderCompileCheck.Run
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PROJECT_DIR = REPO_ROOT / ".ci" / "UnityProject"
PACKAGE_DIR = REPO_ROOT / "Packages" / "jp.sabas0ba.sabashader"

SHADERCORE_URL = "https://github.com/lilxyzw/Shader-Core.git"
# tests/harness/paths.py と同じコミットに固定する
SHADERCORE_COMMIT = "0a0b2fef78fc3b0438b58f443a2e75210db83ec4"


def clone_shadercore(destination: Path) -> None:
    if (destination / "package.json").is_file():
        print(f"Shader Core は配置済み: {destination}")
        return

    if destination.exists():
        shutil.rmtree(destination)
    destination.mkdir(parents=True)

    commands = [
        ["git", "init", "--quiet", str(destination)],
        ["git", "-C", str(destination), "remote", "add", "origin", SHADERCORE_URL],
        ["git", "-C", str(destination), "fetch", "--quiet", "--depth", "1", "origin", SHADERCORE_COMMIT],
        ["git", "-C", str(destination), "checkout", "--quiet", "FETCH_HEAD"],
    ]
    for command in commands:
        subprocess.run(command, check=True, timeout=300)

    shutil.rmtree(destination / ".git", ignore_errors=True)
    print(f"Shader Core を配置しました: {destination}")


def copy_package(destination: Path) -> None:
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(PACKAGE_DIR, destination)
    print(f"パッケージを配置しました: {destination}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=PROJECT_DIR)
    args = parser.parse_args()

    packages = args.project / "Packages"
    if not (packages / "manifest.json").is_file():
        print(f"雛形が見つかりません: {packages / 'manifest.json'}", file=sys.stderr)
        return 1

    copy_package(packages / PACKAGE_DIR.name)
    clone_shadercore(packages / "jp.lilxyzw.shadercore")

    print(f"\n準備完了: {args.project}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
