#!/usr/bin/env python3
"""GitHub Pages に出す HTML の共通ガワ。

リスティング案内ページ (`tools/build_listing.py`) と
docs のページ (`tools/render_docs.py`) は同じ見た目にしたいので、
配色・レイアウト・ヘッダー・フッターをここに集める。

ダークモードは 2 段構えにしてある。

* 既定は OS の設定に追従する（`prefers-color-scheme`）
* 右上のボタンで明示的に切り替えると `localStorage` に覚える

切り替えは `<html data-theme="light|dark">` を書き換えるだけなので、
CSS 側は「システム追従」と「明示指定」の両方を見る必要がある。
色は必ず素の `:root` に定義し、メディアクエリと `[data-theme]` では
上書きだけをする（片方でしか定義しない色を作らない）。
ブラウザの UI 色（`<meta name="theme-color">`）も同じ背景色を使う。

docs のページに出る要素（要約・目次・図）のスタイルもここに置く。
図の中身はシェーダーの出力そのものなので、テーマで色は変えず、
枠と余白だけをテーマに合わせる。等倍で出して拡大による滲みを避ける。
"""

from __future__ import annotations

import html
from typing import List, Optional, Tuple

# ナビゲーションの 1 項目。(href, ラベル)
NavItem = Tuple[str, str]

SITE_NAME = "SabaShader"
REPOSITORY_URL = "https://github.com/sabas0ba/vrc_sabashader"

# 明示切り替えを一瞬でも取りこぼすと白い画面が光るので、
# body より先に <html> へ data-theme を載せる。
THEME_BOOT_SCRIPT = """
(function () {
  try {
    var saved = localStorage.getItem('theme');
    if (saved === 'light' || saved === 'dark') {
      document.documentElement.setAttribute('data-theme', saved);
    }
  } catch (e) {}
})();
"""

THEME_TOGGLE_SCRIPT = """
(function () {
  var root = document.documentElement;
  var button = document.getElementById('theme-toggle');
  if (!button) return;

  var media = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  function current() {
    var explicit = root.getAttribute('data-theme');
    if (explicit === 'light' || explicit === 'dark') return explicit;
    return media && media.matches ? 'dark' : 'light';
  }

  function sync() {
    var dark = current() === 'dark';
    button.setAttribute('aria-pressed', dark ? 'true' : 'false');
    button.setAttribute('title', dark ? 'ライトモードに切り替える' : 'ダークモードに切り替える');
  }

  button.addEventListener('click', function () {
    var next = current() === 'dark' ? 'light' : 'dark';
    root.setAttribute('data-theme', next);
    try { localStorage.setItem('theme', next); } catch (e) {}
    sync();
  });

  if (media && media.addEventListener) {
    media.addEventListener('change', sync);
  }

  button.hidden = false;
  sync();
})();
"""

# ブラウザの UI 色（<meta name="theme-color">）と共有するので、
# 背景だけは定数に出しておく。
BG_LIGHT = "#f7f7fa"
BG_DARK = "#14141a"

# 明るい側を素の :root に置き、暗い側は上書きだけ。
_LIGHT_TOKENS = f"""
    --bg: {BG_LIGHT};
    --surface: #ffffff;
    --surface-soft: #f0f0f5;
    --border: #dededf;
    --border-strong: #c6c6d0;
    --text: #1c1c22;
    --text-muted: #5c5c6b;
    --accent: #3f4bb8;
    --accent-text: #ffffff;
    --accent-soft: #e9ebfb;
    --shadow: 0 1px 2px rgba(20, 20, 35, 0.06), 0 8px 24px rgba(20, 20, 35, 0.06);
"""

_DARK_TOKENS = f"""
    --bg: {BG_DARK};
    --surface: #1c1c24;
    --surface-soft: #23232d;
    --border: #33333f;
    --border-strong: #454553;
    --text: #e8e8ef;
    --text-muted: #a4a4b4;
    --accent: #97a3ff;
    --accent-text: {BG_DARK};
    --accent-soft: #262a45;
    --shadow: 0 1px 2px rgba(0, 0, 0, 0.4), 0 8px 24px rgba(0, 0, 0, 0.35);
"""

PAGE_STYLE = f"""
  :root {{
    color-scheme: light;
{_LIGHT_TOKENS}
  }}

  @media (prefers-color-scheme: dark) {{
    :root:not([data-theme="light"]) {{
      color-scheme: dark;
{_DARK_TOKENS}
    }}
  }}

  :root[data-theme="dark"] {{
    color-scheme: dark;
{_DARK_TOKENS}
  }}

  :root[data-theme="light"] {{
    color-scheme: light;
{_LIGHT_TOKENS}
  }}

  * {{ box-sizing: border-box; }}

  body {{
    margin: 0;
    background: var(--bg);
    color: var(--text);
    font-family: system-ui, -apple-system, "Segoe UI", "Hiragino Sans", "Noto Sans JP", sans-serif;
    line-height: 1.75;
    -webkit-text-size-adjust: 100%;
  }}

  .page {{ max-width: 52rem; margin: 0 auto; padding: 0 1.25rem 4rem; }}

  a {{ color: var(--accent); text-underline-offset: 0.2em; }}
  a:hover {{ text-decoration: underline; }}

  .site-header {{
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.75rem 1rem;
    padding: 1.25rem 0 1rem;
  }}
  .site-header .brand {{
    font-weight: 700;
    font-size: 1.05rem;
    letter-spacing: 0.01em;
    color: var(--text);
    text-decoration: none;
  }}
  .site-header .spacer {{ flex: 1 1 auto; }}

  #theme-toggle {{
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    border: 1px solid var(--border-strong);
    background: var(--surface);
    color: var(--text-muted);
    border-radius: 999px;
    padding: 0.35rem 0.85rem;
    font: inherit;
    font-size: 0.85rem;
    line-height: 1.4;
    cursor: pointer;
  }}
  #theme-toggle:hover {{ color: var(--text); border-color: var(--accent); }}
  #theme-toggle .icon-dark {{ display: none; }}
  :root[data-theme="dark"] #theme-toggle .icon-dark {{ display: inline; }}
  :root[data-theme="dark"] #theme-toggle .icon-light {{ display: none; }}
  @media (prefers-color-scheme: dark) {{
    :root:not([data-theme="light"]) #theme-toggle .icon-dark {{ display: inline; }}
    :root:not([data-theme="light"]) #theme-toggle .icon-light {{ display: none; }}
  }}

  nav.site {{
    display: flex;
    flex-wrap: wrap;
    gap: 0.35rem 0.5rem;
    padding-bottom: 1.1rem;
    border-bottom: 1px solid var(--border);
    margin-bottom: 2.25rem;
  }}
  nav.site a, nav.site strong {{
    display: inline-block;
    padding: 0.25rem 0.7rem;
    border-radius: 999px;
    font-size: 0.9rem;
    text-decoration: none;
  }}
  nav.site a {{ color: var(--text-muted); }}
  nav.site a:hover {{ background: var(--surface-soft); color: var(--text); text-decoration: none; }}
  nav.site strong {{ background: var(--accent-soft); color: var(--accent); font-weight: 600; }}

  h1 {{ font-size: 1.9rem; line-height: 1.3; margin: 0 0 0.75rem; }}
  h2 {{ font-size: 1.35rem; line-height: 1.4; margin: 2.5rem 0 0.75rem; }}
  h3 {{ font-size: 1.1rem; line-height: 1.45; margin: 2rem 0 0.5rem; }}
  h4, h5, h6 {{ margin: 1.75rem 0 0.5rem; }}
  p {{ margin: 0 0 1rem; }}
  ul, ol {{ padding-left: 1.4rem; }}
  li {{ margin: 0.25rem 0; }}

  code {{
    background: var(--surface-soft);
    border: 1px solid var(--border);
    border-radius: 5px;
    padding: 0.08em 0.35em;
    font-size: 0.9em;
    overflow-wrap: break-word;
  }}
  pre {{
    background: var(--surface-soft);
    border: 1px solid var(--border);
    border-radius: 8px;
    padding: 0.9rem 1rem;
    overflow-x: auto;
  }}
  pre code {{ background: none; border: none; padding: 0; font-size: 0.875em; }}

  .table-scroll {{ overflow-x: auto; margin: 1.5rem 0; }}
  table {{
    border-collapse: collapse;
    width: 100%;
    margin: 0;
    font-size: 0.95rem;
  }}
  th, td {{
    border-bottom: 1px solid var(--border);
    padding: 0.55rem 0.7rem;
    text-align: left;
    vertical-align: top;
  }}
  thead th {{
    background: var(--surface-soft);
    border-bottom: 1px solid var(--border-strong);
    font-size: 0.85rem;
    letter-spacing: 0.02em;
    color: var(--text-muted);
    white-space: nowrap;
  }}
  tbody tr:last-child td {{ border-bottom: none; }}

  blockquote {{
    margin: 1.2rem 0;
    padding: 0.1rem 1rem;
    border-left: 3px solid var(--accent);
    background: var(--surface-soft);
    border-radius: 0 6px 6px 0;
    color: var(--text-muted);
  }}

  hr {{ border: none; border-top: 1px solid var(--border); margin: 2.5rem 0; }}

  .cta {{
    display: inline-block;
    background: var(--accent);
    color: var(--accent-text);
    padding: 0.65rem 1.3rem;
    border-radius: 8px;
    font-weight: 600;
    text-decoration: none;
  }}
  .cta:hover {{ filter: brightness(1.08); text-decoration: none; }}

  .card {{
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 12px;
    padding: 1.25rem 1.4rem;
    box-shadow: var(--shadow);
  }}
  .card .table-scroll {{ margin: 0; }}
  .lede {{ font-size: 1.05rem; color: var(--text-muted); }}
  .note {{ font-size: 0.9rem; color: var(--text-muted); }}

  /* 目次。docs のページは h1 と要約の直後に必ずこれが入る。 */
  .toc {{
    margin: 1.75rem 0 2.5rem;
    padding: 0.85rem 1.1rem;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 10px;
  }}
  .toc .toc-title {{
    margin: 0 0 0.35rem;
    font-size: 0.78rem;
    letter-spacing: 0.06em;
    color: var(--text-muted);
  }}
  .toc ul {{
    list-style: none;
    margin: 0;
    padding: 0;
    display: flex;
    flex-wrap: wrap;
    gap: 0.15rem 1rem;
  }}
  .toc li {{ margin: 0; }}
  .toc a {{ font-size: 0.9rem; }}

  /* 図。中身はシェーダーの出力そのものなので、テーマで色を変えず、
     枠と余白だけをテーマに合わせる。等倍で出して拡大による滲みを避ける。 */
  .figures {{
    display: flex;
    flex-wrap: wrap;
    gap: 0.9rem;
    margin: 1.5rem 0;
  }}
  .figures figure {{
    flex: 1 1 15rem;
    min-width: 0;
    margin: 0;
    padding: 0.75rem;
    background: var(--surface-soft);
    border: 1px solid var(--border);
    border-radius: 10px;
  }}
  .figures img {{
    display: block;
    margin: 0 auto;
    max-width: 100%;
    height: auto;
    border-radius: 6px;
  }}
  .figures figcaption {{
    margin-top: 0.6rem;
    font-size: 0.85rem;
    line-height: 1.6;
    color: var(--text-muted);
  }}

  .doc-list {{ list-style: none; padding: 0; display: grid; gap: 0.6rem; }}
  .doc-list a {{
    display: block;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 10px;
    padding: 0.75rem 1rem;
    text-decoration: none;
    font-weight: 600;
  }}
  .doc-list a:hover {{ border-color: var(--accent); text-decoration: none; }}
  .doc-list .doc-title {{ display: block; }}
  .doc-list .doc-summary {{
    display: block;
    margin-top: 0.2rem;
    font-weight: 400;
    font-size: 0.85rem;
    line-height: 1.6;
    color: var(--text-muted);
  }}

  .site-footer {{
    margin-top: 4rem;
    padding-top: 1.25rem;
    border-top: 1px solid var(--border);
    font-size: 0.85rem;
    color: var(--text-muted);
  }}

  @media (max-width: 30rem) {{
    h1 {{ font-size: 1.55rem; }}
    .card {{ padding: 1rem; }}
  }}
"""


def render_nav(pages: List[NavItem], current: Optional[str]) -> str:
    """ページ間のナビ。現在地は強調してリンクにしない。"""
    links = []
    for href, label in pages:
        if href == current:
            links.append(f'<strong aria-current="page">{html.escape(label)}</strong>')
        else:
            links.append(f'<a href="{html.escape(href, quote=True)}">{html.escape(label)}</a>')
    return '<nav class="site">' + "".join(links) + "</nav>"


def render_header(home_href: str) -> str:
    return (
        '<header class="site-header">'
        f'<a class="brand" href="{html.escape(home_href, quote=True)}">{html.escape(SITE_NAME)}</a>'
        '<span class="spacer"></span>'
        '<button id="theme-toggle" type="button" hidden aria-pressed="false">'
        '<span class="icon-light" aria-hidden="true">&#9788;</span>'
        '<span class="icon-dark" aria-hidden="true">&#9789;</span>'
        "テーマ</button>"
        "</header>"
    )


def render_footer() -> str:
    return (
        '<footer class="site-footer">'
        f'<a href="{REPOSITORY_URL}">GitHub リポジトリ</a>'
        f" &middot; {html.escape(SITE_NAME)}"
        "</footer>"
    )


def render_document(title: str, body: str, nav: str, *, home_href: str) -> str:
    """共通のガワで 1 ページぶんの HTML を組み立てる。"""
    return f"""<!DOCTYPE html>
<html lang="ja">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="light dark">
<meta name="theme-color" content="{BG_LIGHT}" media="(prefers-color-scheme: light)">
<meta name="theme-color" content="{BG_DARK}" media="(prefers-color-scheme: dark)">
<title>{html.escape(title)}</title>
<script>{THEME_BOOT_SCRIPT}</script>
<style>{PAGE_STYLE}</style>
</head>
<body>
<div class="page">
{render_header(home_href)}
{nav}
<main>
{body}
</main>
{render_footer()}
</div>
<script>{THEME_TOGGLE_SCRIPT}</script>
</body>
</html>
"""
