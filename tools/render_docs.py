#!/usr/bin/env python3
"""docs/*.md を静的サイト用の HTML にする。

依存パッケージを増やさないため、汎用の Markdown 実装ではなく
**このリポジトリの docs が実際に使っている記法だけ**を扱う。
未対応の記法を書いたら段落として素通しされるので、
docs を書き足すときは `tests/test_docs_site.py` で確認すること。

対応する記法:
    見出し (#..######) / 箇条書き (-) / 番号付き (1.) / 引用 (>)
    表 (| ... |) / コードブロック (```) / 水平線 (---)
    強調 (**) / インラインコード (`) / リンク ([]())

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

GITHUB_BLOB = "https://github.com/sabas0ba/vrc_sabashader/blob/main"

_FENCE = re.compile(r"^```([A-Za-z0-9_+-]*)\s*$")
_HEADING = re.compile(r"^(#{1,6})\s+(.*)$")
_UNORDERED = re.compile(r"^-\s+(.*)$")
_ORDERED = re.compile(r"^\d+\.\s+(.*)$")
_QUOTE = re.compile(r"^>\s?(.*)$")
_TABLE_SEPARATOR = re.compile(r"^\|[\s|:-]+\|$")
_RULE = re.compile(r"^-{3,}$")

_CODE_SPAN = re.compile(r"`([^`]+)`")
_STRONG = re.compile(r"\*\*([^*]+)\*\*")
_LINK = re.compile(r"\[([^\]]+)\]\(([^)]+)\)")


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


def render_inline(text: str, link: Callable[[str], str]) -> str:
    """インライン記法。コードスパンを先に退避してから他を処理する。"""
    spans: List[str] = []

    def stash(match: re.Match) -> str:
        spans.append(html.escape(match.group(1)))
        return f"\x00{len(spans) - 1}\x00"

    text = _CODE_SPAN.sub(stash, text)
    text = html.escape(text)
    text = _STRONG.sub(lambda m: f"<strong>{m.group(1)}</strong>", text)

    def anchor(match: re.Match) -> str:
        label, href = match.group(1), match.group(2)
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
    def __init__(self, link: Callable[[str], str]) -> None:
        self.link = link
        self.out: List[str] = []
        self.headings: List[Heading] = []

    def inline(self, text: str) -> str:
        return render_inline(text, self.link)

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
        self.out.append(f"<table><thead><tr>{head_html}</tr></thead><tbody>{rows}</tbody></table>")
        return index

    def _quote(self, lines: List[str], index: int) -> int:
        body: List[str] = []
        while index < len(lines):
            match = _QUOTE.match(lines[index])
            if not match:
                break
            body.append(match.group(1))
            index += 1

        inner = _Renderer(self.link)
        inner.run(body)
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
            body.append(line.strip())
            index += 1

        self.out.append(f"<p>{self.inline(' '.join(body))}</p>")
        return index


def render_markdown(text: str, link: Callable[[str], str] = default_link) -> tuple[str, List[Heading]]:
    renderer = _Renderer(link)
    renderer.run(text.replace("\r\n", "\n").split("\n"))
    return "".join(renderer.out), renderer.headings


PAGE_STYLE = """
  :root { color-scheme: light dark; }
  body { font-family: system-ui, sans-serif; max-width: 52rem; margin: 0 auto;
         padding: 2rem 1rem 4rem; line-height: 1.75; }
  nav.site { display: flex; flex-wrap: wrap; gap: 0.75rem; padding-bottom: 1rem;
             border-bottom: 1px solid #8884; margin-bottom: 2rem; }
  nav.site a { text-decoration: none; }
  code { background: #8881; padding: 0.1em 0.35em; border-radius: 4px; }
  pre { background: #8881; padding: 0.9rem 1rem; border-radius: 6px; overflow-x: auto; }
  pre code { background: none; padding: 0; }
  table { border-collapse: collapse; width: 100%; margin: 1.5rem 0; display: block; overflow-x: auto; }
  th, td { border-bottom: 1px solid #8884; padding: 0.5rem; text-align: left; vertical-align: top; }
  blockquote { margin: 1.2rem 0; padding: 0.1rem 1rem; border-left: 3px solid #8886; }
  h1, h2, h3 { line-height: 1.35; }
  hr { border: none; border-top: 1px solid #8884; margin: 2.5rem 0; }
  .cta { display: inline-block; background: #1c1c22; color: #fff; padding: 0.6rem 1.2rem;
         border-radius: 6px; text-decoration: none; }
"""


def render_nav(pages: List[tuple[str, str]], current: Optional[str]) -> str:
    links = []
    for href, label in pages:
        if href == current:
            links.append(f"<strong>{html.escape(label)}</strong>")
        else:
            links.append(f'<a href="{html.escape(href, quote=True)}">{html.escape(label)}</a>')
    return '<nav class="site">' + "".join(links) + "</nav>"


def render_page(title: str, body: str, nav: str) -> str:
    return f"""<!DOCTYPE html>
<html lang="ja">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{html.escape(title)}</title>
<style>{PAGE_STYLE}</style>
</head>
<body>
{nav}
{body}
</body>
</html>
"""


def document_title(text: str) -> str:
    for line in text.splitlines():
        heading = _HEADING.match(line)
        if heading and len(heading.group(1)) == 1:
            return heading.group(2).strip()
    return "docs"


def build(docs_dir: Path, output_dir: Path, extra_nav: Optional[List[tuple[str, str]]] = None) -> Dict[str, Path]:
    sources = sorted(docs_dir.glob("*.md"))
    if not sources:
        raise SystemExit(f"docs が見つかりません: {docs_dir}")

    pages = list(extra_nav or [])
    for source in sources:
        text = source.read_text(encoding="utf-8")
        pages.append((source.stem + ".html", document_title(text)))

    written: Dict[str, Path] = {}
    output_dir.mkdir(parents=True, exist_ok=True)

    for source in sources:
        text = source.read_text(encoding="utf-8")
        body, _ = render_markdown(text)
        target = output_dir / (source.stem + ".html")
        target.write_text(
            render_page(document_title(text), body, render_nav(pages, source.stem + ".html")),
            encoding="utf-8",
        )
        written[source.name] = target

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
