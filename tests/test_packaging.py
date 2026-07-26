"""VCC 配布まわり（package.json / .meta / リスティング）の検証。"""

from __future__ import annotations

import json
import re
from pathlib import Path

import pytest

from harness.paths import PACKAGE_DIR, REPO_ROOT
from tools.build_listing import build_listing, collect_versions, semver_key
from tools.gen_meta import guid_for, iter_assets, run as gen_meta_run

PACKAGE_JSON = PACKAGE_DIR / "package.json"
LISTING_JSON = REPO_ROOT / "listing.json"

SEMVER = re.compile(r"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.\-]+)?$")


@pytest.fixture(scope="module")
def package() -> dict:
    return json.loads(PACKAGE_JSON.read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def listing_config() -> dict:
    return json.loads(LISTING_JSON.read_text(encoding="utf-8"))


# --- package.json -------------------------------------------------------------


def test_package_json_required_fields(package):
    for key in ("name", "displayName", "version", "unity", "description", "author"):
        assert package.get(key), f"package.json に {key} がありません"


def test_package_version_is_semver(package):
    assert SEMVER.match(package["version"]), f"semver ではありません: {package['version']}"


def test_package_depends_on_shadercore(package):
    dependencies = package.get("vpmDependencies", {})
    assert "jp.lilxyzw.shadercore" in dependencies, "Shader Core への依存が宣言されていません"


def test_package_name_matches_folder(package):
    assert package["name"] == PACKAGE_DIR.name


def test_changelog_mentions_current_version(package):
    changelog = (PACKAGE_DIR / "CHANGELOG.md").read_text(encoding="utf-8")
    assert f"[{package['version']}]" in changelog, "CHANGELOG に現在のバージョンの項目がありません"


# --- .meta --------------------------------------------------------------------


def test_no_missing_meta_files():
    missing = gen_meta_run(PACKAGE_DIR, check_only=True)
    assert not missing, (
        ".meta が不足しています。python tools/gen_meta.py を実行してコミットしてください: "
        f"{[str(p.relative_to(REPO_ROOT)) for p in missing]}"
    )


def test_no_orphan_meta_files():
    orphans = [
        path.relative_to(REPO_ROOT)
        for path in PACKAGE_DIR.rglob("*.meta")
        if not Path(str(path)[: -len(".meta")]).exists()
    ]
    assert not orphans, f"対応するファイルが無い .meta があります: {orphans}"


def test_meta_guids_are_unique():
    """GUID の重複はマテリアルの参照先が入れ替わる致命的なバグになる。"""
    guids = {}
    for asset in iter_assets(PACKAGE_DIR):
        meta_path = asset.with_name(asset.name + ".meta")
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta_path.read_text(encoding="utf-8"), re.MULTILINE)
        assert match, f"{meta_path} に guid がありません"

        guid = match.group(1)
        assert guid not in guids, f"GUID が重複しています: {meta_path} と {guids[guid]}"
        guids[guid] = meta_path


def test_guid_derivation_is_deterministic():
    """同じパスからは常に同じ GUID が出ること（新規ファイル用）。"""
    assert guid_for("Shaders/Illust2D/Illust2D.scshader") == guid_for("Shaders/Illust2D/Illust2D.scshader")
    assert guid_for("a") != guid_for("b")
    assert re.fullmatch(r"[0-9a-f]{32}", guid_for("Shaders/Illust2D/Illust2D.scshader"))


def test_scshader_meta_uses_scripted_importer():
    meta = (PACKAGE_DIR / "Shaders" / "Illust2D" / "Illust2D.scshader.meta").read_text(encoding="utf-8")
    assert "ScriptedImporter:" in meta
    assert "11c23ed6ad66fef4699c7e3c88c88784" in meta, "Shader Core のインポータを指していません"


# --- リスティング -------------------------------------------------------------


def test_listing_lists_this_package(listing_config, package):
    assert package["name"] in listing_config["packages"]


def test_listing_url_points_at_pages(listing_config):
    owner, repo = listing_config["repository"].split("/")
    assert listing_config["url"] == f"https://{owner}.github.io/{repo}/index.json"


def test_semver_key_orders_versions():
    versions = ["0.1.0", "0.10.0", "0.2.0", "1.0.0-beta.1", "1.0.0"]
    assert sorted(versions, key=semver_key) == ["0.1.0", "0.2.0", "0.10.0", "1.0.0-beta.1", "1.0.0"]


def test_build_listing_from_releases(listing_config, package):
    releases = [
        {
            "draft": False,
            "manifest": {**package, "version": "0.1.0"},
            "zipUrl": "https://example.invalid/jp.sabas0ba.sabashader-0.1.0.zip",
            "zipSHA256": "a" * 64,
        },
        {
            "draft": False,
            "manifest": {**package, "version": "0.2.0"},
            "zipUrl": "https://example.invalid/jp.sabas0ba.sabashader-0.2.0.zip",
            "zipSHA256": "b" * 64,
        },
        {"draft": True, "manifest": {**package, "version": "9.9.9"}, "zipUrl": "https://example.invalid/x.zip"},
    ]

    versions = collect_versions(releases, listing_config["packages"], token=None, fetch=False)
    listing = build_listing(listing_config, versions)

    entries = listing["packages"][package["name"]]["versions"]
    assert list(entries) == ["0.2.0", "0.1.0"], "新しいバージョンが先頭に来ていません"
    assert "9.9.9" not in entries, "ドラフトのリリースが混ざっています"
    assert entries["0.2.0"]["url"].endswith("0.2.0.zip")
    assert entries["0.2.0"]["zipSHA256"] == "b" * 64
    assert entries["0.1.0"]["vpmDependencies"] == package["vpmDependencies"]
    assert listing["url"] == listing_config["url"]
