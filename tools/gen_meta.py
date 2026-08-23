#!/usr/bin/env python3
"""Unity の .meta ファイルを決定的に生成する。

Unity を持っていない環境でもパッケージを完成させられるようにするためのツール。
GUID はパッケージ内の相対パスから uuid5 で導出するので、誰がいつ実行しても同じ値になる。
GUID が安定していないと、利用者のマテリアルがシェーダーを見失うので重要。

    python tools/gen_meta.py            # 足りない .meta を作る
    python tools/gen_meta.py --check    # 生成せず、足りないものがあれば終了コード 1
"""

from __future__ import annotations

import argparse
import sys
import uuid
from pathlib import Path
from typing import Iterable, List

REPO_ROOT = Path(__file__).resolve().parent.parent
PACKAGE_DIR = REPO_ROOT / "Packages" / "io.github.sabas0ba.sabashader"

# GUID 導出用の名前空間。変更すると全 GUID が変わるので絶対に触らないこと。
GUID_NAMESPACE = uuid.UUID("6f5c9d6e-6a1e-5f0c-9f4d-1d0f8b2a7c31")

IGNORED_NAMES = {".DS_Store", "Thumbs.db"}

# Shader Core の .meta に合わせた拡張子ごとのインポータ
IMPORTER_BY_SUFFIX = {
    ".hlsl": "ShaderIncludeImporter",
    ".cginc": "ShaderIncludeImporter",
    ".shader": "ShaderImporter",
    ".cs": "MonoImporter",
    ".asmdef": "AssemblyDefinitionImporter",
    ".asmref": "AssemblyDefinitionReferenceImporter",
    ".json": "TextScriptImporter",
    ".txt": "TextScriptImporter",
    ".po": "LocalizationImporter",
}

# Shader Core の SCShaderImporter.cs (tag 0.1.9) の GUID
SCSHADER_IMPORTER_GUID = "11c23ed6ad66fef4699c7e3c88c88784"

TRAILING = "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def guid_for(relative_path: str) -> str:
    return uuid.uuid5(GUID_NAMESPACE, relative_path).hex


def meta_body(path: Path, relative_path: str) -> str:
    guid = guid_for(relative_path)

    if path.is_dir():
        return f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n{TRAILING}"

    if path.suffix == ".scshader":
        return (
            f"fileFormatVersion: 2\nguid: {guid}\nScriptedImporter:\n"
            "  internalIDToNameTable: []\n"
            "  externalObjects: {}\n"
            "  serializedVersion: 2\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
            f"  script: {{fileID: 11500000, guid: {SCSHADER_IMPORTER_GUID}, type: 3}}\n"
        )

    if path.suffix == ".cs":
        return (
            f"fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n"
            "  externalObjects: {}\n"
            "  serializedVersion: 2\n"
            "  defaultReferences: []\n"
            "  executionOrder: 0\n"
            "  icon: {instanceID: 0}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )

    if path.suffix == ".shader":
        return (
            f"fileFormatVersion: 2\nguid: {guid}\nShaderImporter:\n"
            "  externalObjects: {}\n"
            "  defaultTextures: []\n"
            "  nonModifiableTextures: []\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )

    importer = IMPORTER_BY_SUFFIX.get(path.suffix, "DefaultImporter")
    return f"fileFormatVersion: 2\nguid: {guid}\n{importer}:\n{TRAILING}"


def iter_assets(root: Path) -> Iterable[Path]:
    for path in sorted(root.rglob("*")):
        if path.suffix == ".meta" or path.name in IGNORED_NAMES:
            continue
        if any(part.startswith(".") for part in path.relative_to(root).parts):
            continue
        yield path


def run(package_dir: Path, check_only: bool) -> List[Path]:
    """不足している .meta を作る（または列挙する）。既存ファイルは書き換えない。"""
    changed: List[Path] = []
    for asset in iter_assets(package_dir):
        meta_path = asset.with_name(asset.name + ".meta")
        if meta_path.exists():
            continue
        changed.append(meta_path)
        if not check_only:
            relative = asset.relative_to(package_dir).as_posix()
            meta_path.write_text(meta_body(asset, relative), encoding="utf-8", newline="\n")
    return changed


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, default=PACKAGE_DIR)
    parser.add_argument("--check", action="store_true", help="生成せず不足を報告するだけ")
    args = parser.parse_args()

    changed = run(args.package, args.check)
    for path in changed:
        print(("missing: " if args.check else "created: ") + str(path.relative_to(REPO_ROOT)))

    if args.check and changed:
        print(f"\n{len(changed)} 件の .meta が不足しています。python tools/gen_meta.py を実行してください。")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
