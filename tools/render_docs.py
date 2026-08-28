#!/usr/bin/env python3
"""docs/*.md を静的サイト用の HTML にする。

依存パッケージを増やさないため、汎用の Markdown 実装ではなく
**このリポジトリの docs が実際に使っている記法だけ**を扱う。
未対応の記法を書いたら段落として素通しされるので、
docs を書き足すときは `tests/test_docs_site.py` で確認すること。

対応する記法:
    見出し (#..######) / 箇条書き (-) / 番号付き (1.) / 引用 (>)
    表 (| ... |) / コードブロック (```) / 水平線 (---)
    強調 (**) / インラインコード (`) / リンク ([]()) / 図 (![]())

docs の体裁は `tests/test_docs_site.py` が検査する。

* 1 ファイル 1 見出し (h1)、その直後に 1 段落の要約を置く
* 要約の下には h2 の目次が自動で入る
* 見出しの階層は飛ばさない
* 図は `![説明](../tests/golden/<ケース名>.png)` の形で行頭に置く。
  回帰テストのゴールデン画像だけを使うので、図が実装からずれない。

    python tools/render_docs.py --output _site/docs
"""

from __future__ import annotations

import argparse
import html
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, List, Optional

REPO_ROOT = Path(__file__).resolve().parent.parent
DOCS_DIR = REPO_ROOT / "docs"

# `python tools/render_docs.py` で直接動かしたときも
# `tools.site_theme` を import できるようにする。
if __package__ in (None, ""):
    sys.path.insert(0, str(REPO_ROOT))

from tools import site_theme  # noqa: E402  （sys.path を通してから読む）

GITHUB_BLOB = "https://github.com/sabas0ba/vrc_sabashader/blob/main"

# 図の実体は回帰テストのゴールデン画像。docs からは相対パスで参照するので
# GitHub 上の Markdown でもそのまま表示され、サイトへ出すときだけここへ集める。
FIGURE_DIR = REPO_ROOT / "tests" / "golden"
SITE_FIGURE_DIR = "figures"

# ナビと索引に出す順と表記。ファイル名順ではなく読む順に並べる。
# docs/*.md と過不足がないことは tests/test_docs_site.py が検査する。
PAGES: List[tuple] = [
    ("shader-illust2d.md", "Illust2D"),
    ("shader-debug.md", "Debug"),
    ("modules.md", "モジュール"),
    ("avatar-demo.md", "アバターで確認"),
    ("testing.md", "テスト"),
    ("adding-a-shader.md", "シェーダーを追加"),
    ("adding-a-module.md", "モジュールを追加"),
    ("distribution.md", "配布"),
]

_FENCE = re.compile(r"^```([A-Za-z0-9_+-]*)\s*$")
_HEADING = re.compile(r"^(#{1,6})\s+(.*)$")
_UNORDERED = re.compile(r"^-\s+(.*)$")
_ORDERED = re.compile(r"^\d+\.\s+(.*)$")
_QUOTE = re.compile(r"^>\s?(.*)$")
_TABLE_SEPARATOR = re.compile(r"^\|[\s|:-]+\|$")
_RULE = re.compile(r"^-{3,}$")

_IMAGE_LINE = re.compile(r"^!\[([^\]]*)\]\(([^)]+)\)$")

_CODE_SPAN = re.compile(r"`([^`]+)`")
_STRONG = re.compile(r"\*\*([^*]+)\*\*")
_LINK = re.compile(r"(!?)\[([^\]]*)\]\(([^)]+)\)")


def slugify(text: str) -> str:
    """見出しから id を作る。GitHub の付け方に寄せる。"""
    slug = text.strip().lower()
    slug = re.sub(r"[`*_\[\]()]", "", slug)
    slug = re.sub(r"[^\w\s-]", "", slug, flags=re.UNICODE)
    return re.sub(r"\s+", "-", slug).strip("-")


def default_link(href: str) -> str:
    """docs 同士のリンクは .html に、リポジトリ内の他ファイルは GitHub に向ける。"""
    if href.startswith(("http://", "https://", "#", "mailto:")):
        return href

    anchor = ""
    if "#" in href:
        href, _, anchor = href.partition("#")
        anchor = "#" + anchor

    if not href:
        return anchor

    # docs/ 配下は同じサイトに出力されるので拡張子だけ差し替える
    name = href.rsplit("/", 1)[-1]
    if href.endswith(".md") and ("/" not in href or href.startswith("docs/")):
        return name[:-3] + ".html" + anchor

    # それ以外（LICENSE, tests/... など）はリポジトリを見せる
    return f"{GITHUB_BLOB}/{href.lstrip('./')}{anchor}"


def default_figure_src(href: str) -> str:
    """図はサイト側では `figures/` にまとめ直す。docs からの相対パスは使わない。"""
    if href.startswith(("http://", "https://", "data:")):
        return href
    return f"{SITE_FIGURE_DIR}/{href.rsplit('/', 1)[-1]}"


def render_inline(
    text: str,
    link: Callable[[str], str],
    figure: Callable[[str], str] = default_figure_src,
    figures: Optional[List[str]] = None,
) -> str:
    """インライン記法。コードスパンを先に退避してから他を処理する。"""
    spans: List[str] = []

    def stash(match: re.Match) -> str:
        spans.append(html.escape(match.group(1)))
        return f"\x00{len(spans) - 1}\x00"

    text = _CODE_SPAN.sub(stash, text)
    text = html.escape(text)
    text = _STRONG.sub(lambda m: f"<strong>{m.group(1)}</strong>", text)

    def anchor(match: re.Match) -> str:
        bang, label, href = match.group(1), match.group(2), match.group(3)
        if bang:
            if figures is not None:
                figures.append(href)
            return (
                f'<img src="{html.escape(figure(href), quote=True)}" '
                f'alt="{html.escape(label, quote=True)}" loading="lazy" decoding="async">'
            )
        return f'<a href="{html.escape(link(href), quote=True)}">{label}</a>'

    text = _LINK.sub(anchor, text)

    def restore(match: re.Match) -> str:
        return f"<code>{spans[int(match.group(1))]}</code>"

    return re.sub(r"\x00(\d+)\x00", restore, text)


@dataclass
class Heading:
    level: int
    text: str
    slug: str


class _Renderer:
    def __init__(
        self,
        link: Callable[[str], str],
        figure: Callable[[str], str] = default_figure_src,
    ) -> None:
        self.link = link
        self.figure = figure
        self.out: List[str] = []
        self.headings: List[Heading] = []
        # 参照された図の元パス（docs からの相対）。サイトへ出すときに複写する。
        self.figures: List[str] = []

    def inline(self, text: str) -> str:
        return render_inline(text, self.link, self.figure, self.figures)

    def run(self, lines: List[str]) -> None:
        index = 0
        while index < len(lines):
            line = lines[index]

            fence = _FENCE.match(line)
            if fence:
                index = self._code_block(lines, index, fence.group(1))
                continue

            if not line.strip():
                index += 1
                continue

            if _RULE.match(line.strip()):
                self.out.append("<hr>")
                index += 1
                continue

            heading = _HEADING.match(line)
            if heading:
                self._heading(len(heading.group(1)), heading.group(2))
                index += 1
                continue

            if line.startswith("|") and index + 1 < len(lines) and _TABLE_SEPARATOR.match(lines[index + 1].strip()):
                index = self._table(lines, index)
                continue

            if _IMAGE_LINE.match(line.strip()):
                index = self._figures(lines, index)
                continue

            if _QUOTE.match(line):
                index = self._quote(lines, index)
                continue

            if _UNORDERED.match(line):
                index = self._list(lines, index, _UNORDERED, "ul")
                continue

            if _ORDERED.match(line):
                index = self._list(lines, index, _ORDERED, "ol")
                continue

            index = self._paragraph(lines, index)

    def _heading(self, level: int, text: str) -> None:
        slug = slugify(text)
        self.headings.append(Heading(level, text, slug))
        self.out.append(f'<h{level} id="{html.escape(slug, quote=True)}">{self.inline(text)}</h{level}>')

    def _code_block(self, lines: List[str], index: int, language: str) -> int:
        body: List[str] = []
        index += 1
        while index < len(lines) and not _FENCE.match(lines[index]):
            body.append(lines[index])
            index += 1
        index += 1  # 閉じフェンス

        attribute = f' class="language-{html.escape(language, quote=True)}"' if language else ""
        self.out.append(f"<pre><code{attribute}>{html.escape(chr(10).join(body))}</code></pre>")
        return index

    def _table(self, lines: List[str], index: int) -> int:
        def cells(row: str) -> List[str]:
            return [cell.strip() for cell in row.strip().strip("|").split("|")]

        header = cells(lines[index])
        index += 2  # 見出し行と区切り行

        body: List[List[str]] = []
        while index < len(lines) and lines[index].startswith("|"):
            body.append(cells(lines[index]))
            index += 1

        head_html = "".join(f"<th>{self.inline(cell)}</th>" for cell in header)
        rows = "".join(
            "<tr>" + "".join(f"<td>{self.inline(cell)}</td>" for cell in row) + "</tr>" for row in body
        )
        # 横長の表はページごと横スクロールさせず、この箱の中だけで動かす
        self.out.append(
            '<div class="table-scroll"><table><thead><tr>'
            f"{head_html}</tr></thead><tbody>{rows}</tbody></table></div>"
        )
        return index

    def _figures(self, lines: List[str], index: int) -> int:
        """行頭の図をひとまとまりにする。連続して並べると横に並ぶ。

        比較したい図（既定値と極端な設定など）は空行を挟まずに並べて書く。
        """
        items: List[str] = []
        while index < len(lines):
            match = _IMAGE_LINE.match(lines[index].strip())
            if not match:
                break
            alt, href = match.group(1), match.group(2)
            self.figures.append(href)
            items.append(
                f'<figure><img src="{html.escape(self.figure(href), quote=True)}" '
                f'alt="{html.escape(alt, quote=True)}" loading="lazy" decoding="async">'
                f"<figcaption>{self.inline(alt)}</figcaption></figure>"
            )
            index += 1

        self.out.append('<div class="figures">' + "".join(items) + "</div>")
        return index

    def _quote(self, lines: List[str], index: int) -> int:
        body: List[str] = []
        while index < len(lines):
            match = _QUOTE.match(lines[index])
            if not match:
                break
            body.append(match.group(1))
            index += 1

        inner = _Renderer(self.link, self.figure)
        inner.run(body)
        self.figures.extend(inner.figures)
        self.out.append("<blockquote>" + "".join(inner.out) + "</blockquote>")
        return index

    def _list(self, lines: List[str], index: int, pattern: re.Pattern, tag: str) -> int:
        items: List[str] = []
        while index < len(lines):
            match = pattern.match(lines[index])
            if not match:
                break
            items.append(match.group(1))
            index += 1
            # 次の行がインデントされた続きなら同じ項目に足す
            while index < len(lines) and lines[index].startswith(("  ", "\t")) and lines[index].strip():
                if _FENCE.match(lines[index].strip()) or lines[index].strip().startswith("|"):
                    break
                items[-1] += " " + lines[index].strip()
                index += 1

        body = "".join(f"<li>{self.inline(item)}</li>" for item in items)
        self.out.append(f"<{tag}>{body}</{tag}>")
        return index

    def _paragraph(self, lines: List[str], index: int) -> int:
        body: List[str] = []
        while index < len(lines) and lines[index].strip():
            line = lines[index]
            if _HEADING.match(line) or _FENCE.match(line) or _QUOTE.match(line):
                break
            if line.startswith("|") or _UNORDERED.match(line) or _ORDERED.match(line):
                break
            if _IMAGE_LINE.match(line.strip()):
                break
            body.append(line.strip())
            index += 1

        self.out.append(f"<p>{self.inline(' '.join(body))}</p>")
        return index


def render_markdown(text: str, link: Callable[[str], str] = default_link) -> tuple[str, List[Heading]]:
    """記法だけを HTML にする。docs の体裁（要約・目次）は付かない。"""
    renderer = _Renderer(link)
    renderer.run(text.replace("\r\n", "\n").split("\n"))
    return "".join(renderer.out), renderer.headings


@dataclass
class Rendered:
    """1 ページぶんの本文と、そこから拾えた情報。"""

    body: str
    headings: List[Heading]
    figures: List[str]


def render_toc(headings: List[Heading], link: Callable[[str], str] = default_link) -> str:
    """h2 だけの目次。どのページでも同じ位置・同じ形で入る。"""
    items = [heading for heading in headings if heading.level == 2]
    if len(items) < 2:
        return ""

    links = "".join(
        f'<li><a href="#{html.escape(heading.slug, quote=True)}">'
        f"{render_inline(heading.text, link)}</a></li>"
        for heading in items
    )
    return (
        '<nav class="toc" aria-label="このページの内容">'
        '<p class="toc-title">このページの内容</p>'
        f"<ul>{links}</ul></nav>"
    )


def render_body(
    text: str,
    link: Callable[[str], str] = default_link,
    figure: Callable[[str], str] = default_figure_src,
) -> Rendered:
    """docs の体裁を付けた本文を組み立てる。

    見出し（h1）→ 要約の段落 → 目次 → 本編、の順に必ず並べる。
    先頭 2 つの形は `tests/test_docs_site.py` が全ページぶん検査する。
    """
    renderer = _Renderer(link, figure)
    renderer.run(text.replace("\r\n", "\n").split("\n"))
    parts = list(renderer.out)

    if len(parts) >= 2 and parts[0].startswith("<h1") and parts[1].startswith("<p>"):
        parts[1] = '<p class="lede">' + parts[1][len("<p>") :]
        parts.insert(2, render_toc(renderer.headings, link))

    return Rendered("".join(parts), renderer.headings, renderer.figures)


def render_nav(pages: List[tuple[str, str]], current: Optional[str]) -> str:
    return site_theme.render_nav(pages, current)


def render_page(title: str, body: str, nav: str, *, home_href: str = "../index.html") -> str:
    return site_theme.render_document(title, body, nav, home_href=home_href)


def document_title(text: str) -> str:
    for line in text.splitlines():
        heading = _HEADING.match(line)
        if heading and len(heading.group(1)) == 1:
            return heading.group(2).strip()
    return "docs"


def document_summary(text: str) -> str:
    """h1 の直後の段落。索引の 1 行説明に使う。"""
    lines = text.replace("\r\n", "\n").split("\n")
    for index, line in enumerate(lines):
        heading = _HEADING.match(line)
        if not (heading and len(heading.group(1)) == 1):
            continue
        body: List[str] = []
        for following in lines[index + 1 :]:
            if not following.strip():
                if body:
                    break
                continue
            if _HEADING.match(following):
                break
            body.append(following.strip())
        return " ".join(body)
    return ""


def first_sentence(text: str) -> str:
    """索引カードに出す 1 文。要約の先頭だけを取る。"""
    head, mark, _ = text.partition("。")
    return head + mark if mark else text


@dataclass
class Page:
    """docs の 1 ページ。ナビ・索引・出力先はここから決まる。"""

    source: Path
    href: str
    label: str
    title: str
    summary: str


def collect_pages(docs_dir: Path) -> List[Page]:
    """`PAGES` の順に docs を並べる。過不足があればその場で落とす。"""
    found = {source.name for source in docs_dir.glob("*.md")}
    listed = {name for name, _ in PAGES}

    missing = sorted(found - listed)
    if missing:
        raise SystemExit(
            "tools/render_docs.py の PAGES に載っていない docs があります: "
            + ", ".join(missing)
        )
    absent = sorted(listed - found)
    if absent:
        raise SystemExit("PAGES に載っているファイルがありません: " + ", ".join(absent))

    pages: List[Page] = []
    for name, label in PAGES:
        source = docs_dir / name
        text = source.read_text(encoding="utf-8")
        pages.append(
            Page(
                source=source,
                href=name[:-3] + ".html",
                label=label,
                title=document_title(text),
                summary=document_summary(text),
            )
        )
    return pages


def copy_figures(figures: List[str], docs_dir: Path, output_dir: Path) -> List[Path]:
    """docs が参照した図をサイトへ複写する。実体は回帰テストのゴールデン画像。"""
    import shutil

    copied: List[Path] = []
    for href in sorted(set(figures)):
        if href.startswith(("http://", "https://", "data:")):
            continue

        source = (docs_dir / href).resolve()
        if not source.is_file():
            raise SystemExit(f"図が見つかりません: {href}")
        if source.parent != FIGURE_DIR:
            raise SystemExit(
                f"図は {FIGURE_DIR} のゴールデン画像だけを使います: {href}"
            )

        target = output_dir / SITE_FIGURE_DIR / source.name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        copied.append(target)
    return copied


def build(
    docs_dir: Path,
    output_dir: Path,
    extra_nav: Optional[List[tuple[str, str]]] = None,
    home_href: str = "../index.html",
) -> Dict[str, Path]:
    pages = collect_pages(docs_dir)
    nav_items = list(extra_nav or []) + [(page.href, page.label) for page in pages]

    written: Dict[str, Path] = {}
    figures: List[str] = []
    output_dir.mkdir(parents=True, exist_ok=True)

    for page in pages:
        text = page.source.read_text(encoding="utf-8")
        rendered = render_body(text)
        figures.extend(rendered.figures)

        target = output_dir / page.href
        target.write_text(
            render_page(
                page.title,
                rendered.body,
                render_nav(nav_items, page.href),
                home_href=home_href,
            ),
            encoding="utf-8",
        )
        written[page.source.name] = target

    copy_figures(figures, docs_dir, output_dir)
    return written


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--docs", type=Path, default=DOCS_DIR)
    parser.add_argument("--output", type=Path, default=REPO_ROOT / "_site" / "docs")
    args = parser.parse_args()

    written = build(args.docs, args.output)
    for name, target in written.items():
        print(f"{name} -> {target}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
