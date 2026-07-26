#!/usr/bin/env python3
"""GitHub Releases から VCC 用の VPM リスティング (index.json) を組み立てる。

VCC は「リスティング URL」を 1 本購読し、その中の各バージョンの zip を取得する。
リリースを作るたびにこのスクリプトを回して GitHub Pages に置けばよい。

    python tools/build_listing.py --output _site/index.json

`--releases <file>` を渡すとネットワークを使わずに JSON から読む（テスト用）。
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import urllib.request
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_CONFIG = REPO_ROOT / "listing.json"

GITHUB_API = "https://api.github.com"


def semver_key(version: str) -> Tuple:
    """プレリリース付きの semver をざっくり比較できるキーにする。"""
    core, _, pre = version.partition("-")
    numbers = []
    for part in core.split("."):
        numbers.append(int(part) if part.isdigit() else 0)
    while len(numbers) < 3:
        numbers.append(0)
    # プレリリースは同じ数値より前に来る
    return (numbers[0], numbers[1], numbers[2], 0 if pre else 1, pre)


def _request(url: str, token: Optional[str], accept: str = "application/vnd.github+json") -> bytes:
    request = urllib.request.Request(url, headers={"Accept": accept, "User-Agent": "sabashader-listing"})
    if token:
        request.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def fetch_releases(repository: str, token: Optional[str]) -> List[dict]:
    releases: List[dict] = []
    page = 1
    while True:
        payload = _request(f"{GITHUB_API}/repos/{repository}/releases?per_page=100&page={page}", token)
        batch = json.loads(payload)
        if not batch:
            break
        releases.extend(batch)
        page += 1
    return releases


def _asset_url(release: dict, suffix: str) -> Optional[str]:
    for asset in release.get("assets", []):
        if asset.get("name", "").endswith(suffix):
            return asset.get("browser_download_url")
    return None


def collect_versions(
    releases: Iterable[dict],
    package_names: Iterable[str],
    token: Optional[str],
    *,
    fetch: bool = True,
) -> Dict[str, Dict[str, dict]]:
    """リリースを {パッケージ名: {バージョン: package.json 相当}} に畳む。"""
    wanted = set(package_names)
    result: Dict[str, Dict[str, dict]] = {name: {} for name in wanted}

    for release in releases:
        if release.get("draft"):
            continue

        manifest = release.get("manifest")
        if manifest is None:
            url = _asset_url(release, "package.json")
            if url is None or not fetch:
                continue
            manifest = json.loads(_request(url, token, accept="application/octet-stream"))

        name = manifest.get("name")
        version = manifest.get("version")
        if name not in wanted or not version:
            continue

        zip_url = release.get("zipUrl") or _asset_url(release, ".zip")
        if not zip_url:
            continue

        entry = dict(manifest)
        entry["url"] = zip_url

        digest = release.get("zipSHA256")
        if digest is None and fetch:
            digest = hashlib.sha256(_request(zip_url, token, accept="application/octet-stream")).hexdigest()
        if digest:
            entry["zipSHA256"] = digest

        result[name][version] = entry

    return result


def build_listing(config: dict, versions: Dict[str, Dict[str, dict]]) -> dict:
    packages = {}
    for name in config["packages"]:
        found = versions.get(name, {})
        if not found:
            continue
        ordered = sorted(found, key=semver_key, reverse=True)
        packages[name] = {"versions": {version: found[version] for version in ordered}}

    listing = {
        "name": config["name"],
        "id": config["id"],
        "url": config["url"],
        "author": config["author"],
        "packages": packages,
    }
    for optional in ("description", "infoLink", "bannerUrl"):
        if config.get(optional):
            listing[optional] = config[optional]
    return listing


def render_page(listing: dict) -> str:
    """GitHub Pages に置く案内ページ。VCC にリスティングを登録するリンクを出す。"""
    add_link = f"vcc://vpm/addRepo?url={listing['url']}"

    rows = []
    for name, package in listing["packages"].items():
        versions = list(package["versions"])
        latest = package["versions"][versions[0]] if versions else {}
        rows.append(
            "<tr>"
            f"<td><code>{name}</code></td>"
            f"<td>{latest.get('displayName', '')}</td>"
            f"<td>{versions[0] if versions else '-'}</td>"
            f"<td>{len(versions)}</td>"
            "</tr>"
        )

    table = "\n".join(rows) or '<tr><td colspan="4">まだリリースがありません</td></tr>'
    description = listing.get("description", "")

    return f"""<!DOCTYPE html>
<html lang="ja">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{listing['name']} VPM Listing</title>
<style>
  body {{ font-family: system-ui, sans-serif; max-width: 46rem; margin: 3rem auto; padding: 0 1rem; line-height: 1.7; }}
  code {{ background: #f0f0f3; padding: 0.1em 0.35em; border-radius: 4px; }}
  table {{ border-collapse: collapse; width: 100%; margin: 1.5rem 0; }}
  th, td {{ border-bottom: 1px solid #ddd; padding: 0.5rem; text-align: left; }}
  .cta {{ display: inline-block; background: #1c1c22; color: #fff; padding: 0.6rem 1.2rem;
          border-radius: 6px; text-decoration: none; }}
</style>
</head>
<body>
<h1>{listing['name']}</h1>
<p>{description}</p>
<p><a class="cta" href="{add_link}">VCC にこのリスティングを追加</a></p>
<p>うまく動かない場合は VCC の Settings &gt; Packages &gt; Add Repository に
<code>{listing['url']}</code> を貼り付けてください。</p>
<table>
<thead><tr><th>パッケージ</th><th>表示名</th><th>最新</th><th>バージョン数</th></tr></thead>
<tbody>
{table}
</tbody>
</table>
</body>
</html>
"""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, default=DEFAULT_CONFIG)
    parser.add_argument("--output", type=Path, default=REPO_ROOT / "_site" / "index.json")
    parser.add_argument("--html", type=Path, help="案内ページの出力先")
    parser.add_argument("--releases", type=Path, help="GitHub API を叩かずこの JSON を使う")
    args = parser.parse_args()

    config = json.loads(args.config.read_text(encoding="utf-8"))
    token = os.environ.get("GITHUB_TOKEN")

    if args.releases:
        releases = json.loads(args.releases.read_text(encoding="utf-8"))
        fetch = False
    else:
        releases = fetch_releases(config["repository"], token)
        fetch = True

    versions = collect_versions(releases, config["packages"], token, fetch=fetch)
    listing = build_listing(config, versions)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(listing, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    if args.html:
        args.html.parent.mkdir(parents=True, exist_ok=True)
        args.html.write_text(render_page(listing), encoding="utf-8")

    total = sum(len(entry["versions"]) for entry in listing["packages"].values())
    print(f"{args.output} を書き出しました（{len(listing['packages'])} パッケージ / {total} バージョン）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
