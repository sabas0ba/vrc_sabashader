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
