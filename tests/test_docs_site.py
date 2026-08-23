"""docs を静的サイトへ書き出す処理の検証。

`tools/render_docs.py` は汎用の Markdown 実装ではなく、
このリポジトリの docs が使っている記法だけを扱う。未対応の記法を書くと
段落として素通しされて気付けないので、ここで見張る。
"""

from __future__ import annotations

import re
from pathlib import Path

import pytest

from harness.paths import REPO_ROOT
from tools.render_docs import build, document_title, render_markdown, slugify

DOCS_DIR = REPO_ROOT / "docs"

# 変換されずに残ると本文に出てしまう記法
UNCONVERTED = (
    (re.compile(r"\]\("), "リンク"),
    (re.compile(r"^#{1,6}\s", re.MULTILINE), "見出し"),
    (re.compile(r"\*\*"), "強調"),
    (re.compile(r"^\|", re.MULTILINE), "表"),
)


@pytest.fixture(scope="module")
def site(tmp_path_factory) -> dict:
    output = tmp_path_factory.mktemp("site")
    build(DOCS_DIR, output, extra_nav=[("../index.html", "リスティング")])
    return {path.name: path.read_text(encoding="utf-8") for path in output.glob("*.html")}


def test_every_doc_is_rendered(site):
    expected = {source.stem + ".html" for source in DOCS_DIR.glob("*.md")}
    assert set(site) == expected


def test_pages_are_not_empty(site):
    for name, text in site.items():
        body = text.split("<body>", 1)[1]
        assert len(body) > 500, f"{name} の本文が短すぎます"


@pytest.mark.parametrize("source", sorted(DOCS_DIR.glob("*.md")), ids=lambda p: p.name)
def test_no_unconverted_markup(source):
    """本文に Markdown の記法がそのまま残っていないこと。"""
    body, _ = render_markdown(source.read_text(encoding="utf-8"))

    # コードブロックの中は原文のままで正しいので取り除いてから見る
    without_code = re.sub(r"<pre>.*?</pre>", "", body, flags=re.DOTALL)
    without_code = re.sub(r"<code>.*?</code>", "", without_code, flags=re.DOTALL)

    for pattern, label in UNCONVERTED:
        assert not pattern.search(without_code), f"{source.name}: {label}が変換されていません"


def test_internal_links_resolve(site):
    """docs 同士のリンクとアンカーが実在すること。"""
    ids = {name: set(re.findall(r'id="([^"]+)"', text)) for name, text in site.items()}
    problems = []

    for name, text in site.items():
        for href in re.findall(r'href="([^"]+)"', text):
            if href.startswith(("http://", "https://", "mailto:", "../")):
                continue

            target, _, anchor = href.partition("#")
            if target and target not in site:
                problems.append(f"{name}: リンク先がありません {href}")
            elif anchor and anchor not in ids.get(target or name, set()):
                problems.append(f"{name}: アンカーがありません {href}")

    assert not problems, "\n".join(problems)


def test_titles_come_from_first_heading():
    for source in DOCS_DIR.glob("*.md"):
        title = document_title(source.read_text(encoding="utf-8"))
        assert title != "docs", f"{source.name} に h1 がありません"


def test_html_is_escaped():
    body, _ = render_markdown("<script>alert(1)</script> と `<b>` の話")
    assert "<script>" not in body
    assert "&lt;script&gt;" in body
    assert "<code>&lt;b&gt;</code>" in body


def test_code_block_keeps_content():
    body, _ = render_markdown("```bash\npython -m pytest tests -q\n```")
    assert '<pre><code class="language-bash">' in body
    assert "python -m pytest tests -q" in body


def test_table_is_rendered():
    body, _ = render_markdown("| a | b |\n| --- | --- |\n| 1 | 2 |")
    assert "<table>" in body and "<th>a</th>" in body and "<td>2</td>" in body


def test_markdown_links_point_at_generated_pages():
    body, _ = render_markdown("[テスト](testing.md) と [節](avatar-demo.md#ライセンス表記)")
    assert 'href="testing.html"' in body
    assert 'href="avatar-demo.html#ライセンス表記"' in body


def test_repository_links_go_to_github():
    body, _ = render_markdown("[ライセンス](LICENSE)")
    assert "https://github.com/sabas0ba/vrc_sabashader/blob/main/LICENSE" in body


def test_slug_matches_heading_anchor():
    body, _ = render_markdown("## ライセンス表記")
    assert f'id="{slugify("ライセンス表記")}"' in body


# --- 共通のガワとダークモード ---------------------------------------------


@pytest.fixture(scope="module")
def listing_page() -> str:
    """リスティング案内ページ。docs と同じガワで出ているかを見る。"""
    from tools.build_listing import render_page

    listing = {
        "name": "SabaShader",
        "url": "https://example.invalid/index.json",
        "description": "説明",
        "packages": {
            "io.github.sabas0ba.sabashader": {
                "versions": {"1.0.0": {"displayName": "SabaShader"}},
            }
        },
    }
    return render_page(listing, [("docs/testing.html", "テストの仕組み")])


def test_listing_and_docs_share_the_shell(site, listing_page):
    """ヘッダー・ナビ・フッター・スタイルが 2 種類に分かれていないこと。"""
    from tools.site_theme import PAGE_STYLE

    for name, text in list(site.items()) + [("index.html", listing_page)]:
        assert PAGE_STYLE in text, f"{name} が共通スタイルを使っていません"
        assert '<header class="site-header">' in text, f"{name} にヘッダーがありません"
        assert '<nav class="site">' in text, f"{name} にナビがありません"
        assert '<footer class="site-footer">' in text, f"{name} にフッターがありません"


def test_listing_links_to_docs(listing_page):
    assert 'href="docs/testing.html"' in listing_page


def test_docs_link_back_to_listing(site):
    for name, text in site.items():
        assert 'href="../index.html"' in text, f"{name} からリスティングに戻れません"


def test_dark_mode_follows_the_os_and_the_toggle(site, listing_page):
    """OS 追従と明示切り替えの両方が効くこと。"""
    for name, text in list(site.items()) + [("index.html", listing_page)]:
        assert "prefers-color-scheme: dark" in text, f"{name} が OS の設定に追従しません"
        assert ':root[data-theme="dark"]' in text, f"{name} に明示的なダーク指定がありません"
        assert 'id="theme-toggle"' in text, f"{name} に切り替えボタンがありません"


def test_colors_are_defined_for_both_themes():
    """色は素の :root に定義し、暗い側では上書きだけにすること。

    片方のブロックでしか定義していない変数があると、
    もう片方のテーマで色が抜け落ちる。
    """
    from tools.site_theme import PAGE_STYLE

    def variables(block: str) -> set:
        return set(re.findall(r"(--[\w-]+):", block))

    root = PAGE_STYLE.split(":root {", 1)[1].split("}", 1)[0]
    dark = PAGE_STYLE.split(':root[data-theme="dark"] {', 1)[1].split("}", 1)[0]

    assert variables(root), "既定のテーマに色が定義されていません"
    assert variables(dark) == variables(root), "明暗で定義されている色が食い違っています"
