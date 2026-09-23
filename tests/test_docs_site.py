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
from tools.render_docs import (
    PAGE_GROUPS,
    PAGES,
    build,
    collect_pages,
    document_summary,
    document_title,
    render_body,
    render_markdown,
    slugify,
)

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
    return render_page(
        listing,
        [("docs/testing.html", "テスト", "テストの仕組み", "`tests/` の中身の説明です。")],
    )


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
    # 索引には見出しと 1 行の要約が出る。要約の記法も解いてから出す。
    assert "テストの仕組み" in listing_page
    assert "<code>tests/</code>" in listing_page


def test_docs_link_back_to_listing(site):
    for name, text in site.items():
        assert 'href="../index.html"' in text, f"{name} からリスティングに戻れません"


def test_dark_mode_follows_the_os_and_the_toggle(site, listing_page):
    """OS 追従と明示切り替えの両方が効くこと。"""
    for name, text in list(site.items()) + [("index.html", listing_page)]:
        assert "prefers-color-scheme: dark" in text, f"{name} が OS の設定に追従しません"
        assert ':root[data-theme="dark"]' in text, f"{name} に明示的なダーク指定がありません"
        assert 'id="theme-toggle"' in text, f"{name} に切り替えボタンがありません"


def test_theme_color_matches_the_palette(site, listing_page):
    """ブラウザの UI 色もページの背景に合わせる。

    meta のメディアクエリは OS の設定しか見ないので、明示的に切り替えている間は
    トグルが `content` を上書きする。戻すための元の色は `data-color` に持たせる。
    """
    from tools.site_theme import BG_DARK, BG_LIGHT, THEME_TOGGLE_SCRIPT

    for name, text in list(site.items()) + [("index.html", listing_page)]:
        assert f'content="{BG_LIGHT}" media="(prefers-color-scheme: light)"' in text, name
        assert f'content="{BG_DARK}" media="(prefers-color-scheme: dark)"' in text, name
        assert f'data-color="{BG_LIGHT}"' in text, name
        assert f'data-color="{BG_DARK}"' in text, name

    assert 'meta[name="theme-color"]' in THEME_TOGGLE_SCRIPT
    assert BG_DARK in THEME_TOGGLE_SCRIPT and BG_LIGHT in THEME_TOGGLE_SCRIPT


def test_figures_and_toc_are_styled(site):
    """図と目次の見た目も共通のスタイルに入っていること。"""
    from tools.site_theme import PAGE_STYLE

    for selector in (".figures", ".figures img", ".figures figcaption", ".toc"):
        assert selector + " {" in PAGE_STYLE, f"{selector} のスタイルがありません"


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


# --- docs の体裁 -----------------------------------------------------------
#
# ページごとに構えが違うと、サイトに出したときに見出しの位置も目次の有無も
# 揃わない。書き方の規約は tools/render_docs.py の docstring にある。

_HEADING = re.compile(r"^(#{1,6})\s+(.*)$")
_FENCE = re.compile(r"^```")
_IMAGE = re.compile(r"!\[([^\]]*)\]\(([^)]+)\)")

# 図はここのファイルだけを使う。回帰テストが守っている画像なので、
# 数式を変えれば図も変わり、説明が実装から離れない。
FIGURE_PREFIX = "../tests/golden/"


def body_lines(text: str):
    """コードブロックの中を除いた行を返す。"""
    inside = False
    for line in text.replace("\r\n", "\n").split("\n"):
        if _FENCE.match(line.strip()):
            inside = not inside
            continue
        if not inside:
            yield line


@pytest.mark.parametrize("source", sorted(DOCS_DIR.glob("*.md")), ids=lambda p: p.name)
def test_doc_starts_with_a_heading_and_a_summary(source):
    """1 ファイル 1 見出し。その直後は必ず 1 段落の要約にする。"""
    text = source.read_text(encoding="utf-8")
    lines = list(body_lines(text))

    headings = [line for line in lines if _HEADING.match(line)]
    assert lines[0].startswith("# "), f"{source.name}: 先頭が h1 ではありません"
    assert sum(1 for line in headings if line.startswith("# ")) == 1, (
        f"{source.name}: h1 が 2 つ以上あります"
    )

    following = [line for line in lines[1:] if line.strip()]
    assert following, f"{source.name}: 本文がありません"
    assert not following[0].startswith(("#", "-", "|", ">", "1.", "!")), (
        f"{source.name}: h1 の直後が要約の段落ではありません: {following[0]}"
    )
    assert document_summary(text), f"{source.name}: 要約を取り出せません"


@pytest.mark.parametrize("source", sorted(DOCS_DIR.glob("*.md")), ids=lambda p: p.name)
def test_heading_levels_do_not_skip(source):
    level = 1
    for line in body_lines(source.read_text(encoding="utf-8")):
        heading = _HEADING.match(line)
        if not heading:
            continue
        current = len(heading.group(1))
        assert current <= level + 1, f"{source.name}: 見出しの階層が飛んでいます: {line}"
        level = current


@pytest.mark.parametrize("source", sorted(DOCS_DIR.glob("*.md")), ids=lambda p: p.name)
def test_figures_are_golden_images_on_their_own_line(source):
    for line in body_lines(source.read_text(encoding="utf-8")):
        # インラインコードの中は記法の説明なので見ない（レンダラも同じ扱い）
        line = re.sub(r"`[^`]+`", "", line)
        for match in _IMAGE.finditer(line):
            assert line.strip() == match.group(0), (
                f"{source.name}: 図は行頭に単独で置いてください: {line.strip()}"
            )
            href = match.group(2)
            assert href.startswith(FIGURE_PREFIX), (
                f"{source.name}: 図は {FIGURE_PREFIX} のゴールデン画像だけを使います: {href}"
            )
            assert (source.parent / href).resolve().is_file(), (
                f"{source.name}: 図がありません: {href}"
            )
            assert match.group(1).strip(), f"{source.name}: 図に説明がありません: {href}"


def test_pages_cover_every_doc():
    """ナビの並びと docs のファイルが食い違っていないこと。"""
    listed = [name for name, _ in PAGES]
    assert sorted(listed) == sorted(source.name for source in DOCS_DIR.glob("*.md"))
    assert len(set(listed)) == len(listed), "PAGES に重複があります"

    for page in collect_pages(DOCS_DIR):
        assert page.label, f"{page.source.name} のナビ表記がありません"
        assert page.title, f"{page.source.name} の見出しがありません"


def test_page_groups_cover_declared_pages_once():
    """分類とフラットな索引が同じページ集合・同じ順序を持つこと。"""
    grouped = [page for _, pages in PAGE_GROUPS for page in pages]
    assert grouped == PAGES
    assert [label for label, _ in PAGE_GROUPS] == [
        "Core Shader",
        "Shader拡張",
        "利用ガイド",
        "開発・配布",
    ]


def test_nav_follows_the_declared_order(site):
    """全ページのナビが同じ順で並ぶこと。"""
    expected = [name[:-3] + ".html" for name, _ in PAGES]
    for name, text in site.items():
        nav = text.split('<nav class="site">', 1)[1].split("</nav>", 1)[0]
        found = re.findall(r'href="([^"]+\.html)"', nav)
        # 先頭にリスティングへの戻りが入る
        assert found[0] == "../index.html", f"{name}: 戻り先がありません"
        # 現在地はリンクにならないので、残りが順序どおり並んでいれば良い
        assert found[1:] == [href for href in expected if href != name], f"{name}: ナビの並びが違います"


def test_nav_separates_core_shaders_and_extensions(site):
    """Materialで選ぶshaderとmoduleで追加する機能を視覚的に分離する。"""
    expected_groups = ["サイト"] + [label for label, _ in PAGE_GROUPS]
    for name, text in site.items():
        nav = text.split('<nav class="site">', 1)[1].split("</nav>", 1)[0]
        found = re.findall(r'<span class="nav-group-title">([^<]+)</span>', nav)
        assert found == expected_groups, f"{name}: ナビ分類が違います"


def test_category_pages_compare_usage_parameters_and_rendering(site):
    """2つの入口ページだけで選択・導入・主要調整値まで判断できること。"""
    core = site["core-shaders.html"]
    for term in (
        "Illust2D",
        "Debug",
        "使い方",
        "主要パラメータ",
        "sphere_default.png",
        "debug_shader_demo.png",
    ):
        assert term in core, f"Core Shader一覧に {term} がありません"

    extensions = site["shader-extensions.html"]
    for term in (
        "Surface Overlay",
        "Pixel Art",
        "Video Input",
        "Display Panel",
        "CRT / Glitch",
        "Decal",
        "Surface Detail",
        "Spatial Interior",
        "Transition",
        "衣装変身バンク",
        "有効化方法",
        "主要パラメータ",
        "transformation_bank_demo.png",
    ):
        assert term in extensions, f"Shader拡張一覧に {term} がありません"


def test_body_has_a_lede_and_a_table_of_contents():
    """要約と目次が、どのページでも同じ位置に入ること。"""
    for page in collect_pages(DOCS_DIR):
        rendered = render_body(page.source.read_text(encoding="utf-8"))
        head = rendered.body[: rendered.body.index("</nav>") + len("</nav>")]

        assert head.index("<h1") < head.index('<p class="lede">'), f"{page.source.name}"
        assert head.index('<p class="lede">') < head.index('<nav class="toc"'), f"{page.source.name}"

        for heading in rendered.headings:
            if heading.level == 2:
                assert f'href="#{heading.slug}"' in head, f"{page.source.name}: 目次に {heading.text} がありません"


def test_figures_are_copied_and_referenced(site, tmp_path):
    """図がサイトへ複写され、ページがその複写を指していること。"""
    output = tmp_path / "site"
    build(DOCS_DIR, output, extra_nav=[("../index.html", "リスティング")])

    referenced = set()
    for name, text in site.items():
        for src in re.findall(r'<img src="([^"]+)"', text):
            assert src.startswith("figures/"), f"{name}: 図の参照先が違います: {src}"
            referenced.add(src)

    assert referenced, "図が 1 枚も出ていません"
    for src in referenced:
        assert (output / src).is_file(), f"{src} が複写されていません"


def test_figures_have_captions():
    body, _ = render_markdown("![説明](../tests/golden/sphere_default.png)")
    assert '<div class="figures">' in body
    assert '<img src="figures/sphere_default.png" alt="説明"' in body
    assert "<figcaption>説明</figcaption>" in body


def test_consecutive_figures_share_one_row():
    body, _ = render_markdown(
        "![A](../tests/golden/sphere_default.png)\n![B](../tests/golden/box_default.png)"
    )
    assert body.count('<div class="figures">') == 1
    assert body.count("<figure>") == 2
