#!/usr/bin/env python3
"""シェーディングの見た目を Unity 無しで PNG に書き出す。

テストが使っているヘッドレス描画（EGL + llvmpipe）をそのまま呼ぶので、
Unity もディスプレイも要らない。ドキュメント用の図や、数式をいじった
ときの当たり確認に使う。

    tools/dev.sh python tools/render_preview.py --list
    tools/dev.sh python tools/render_preview.py --case sphere_default
    tools/dev.sh python tools/render_preview.py --all --output _preview
    tools/dev.sh python tools/render_preview.py --sheet _preview/sheet.png

見ているのは `*Core.hlsl` の数式で、Unity のマテリアルそのものではない。
テクスチャや実際のメッシュを含めた確認は `.ci/UnityProject` の確認シーンを使う。

`--compare` を渡すとゴールデン画像との差分も書き出す。
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import List, Optional

REPO_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO_ROOT / "tests"))

from cases import CASES, CASES_BY_NAME  # noqa: E402
from harness import compare as cmp  # noqa: E402
from harness.glsl import build_scene_source  # noqa: E402
from harness.paths import GOLDEN_DIR  # noqa: E402
from harness.render import render_fragment, renderer_info  # noqa: E402

DEFAULT_OUTPUT = REPO_ROOT / "_preview"

# 並べたときに見やすい順。ここに無いものは後ろにまとめる。
SHEET_ORDER = ("sphere", "box", "torus", "capsule", "overlay", "pixel", "swatch")


def render_case(case) -> "object":
    source = build_scene_source(
        style=case.resolved_style(),
        mode=case.mode,
        resolution=case.resolution,
        light_dir=case.light_dir,
        light_color=case.light_color,
        ambient=case.ambient,
        outline=case.resolved_outline(),
        module_styles=case.resolved_module_styles(),
    )
    return render_fragment(source, case.resolution)


def sheet_key(name: str) -> tuple:
    for index, prefix in enumerate(SHEET_ORDER):
        if name.startswith(prefix):
            return (index, name)
    return (len(SHEET_ORDER), name)


def write_sheet(names: List[str], destination: Path, columns: int) -> None:
    """全ケースを 1 枚に並べる。ドキュメントに貼る用。"""
    from PIL import Image, ImageDraw

    padding = 12
    label_height = 16

    images = {
        name: Image.fromarray(render_case(CASES_BY_NAME[name]), mode="RGBA").convert("RGB")
        for name in names
    }
    cell_width = max(image.width for image in images.values())
    cell_height = max(image.height for image in images.values())

    rows = (len(names) + columns - 1) // columns
    width = columns * (cell_width + padding) + padding
    height = rows * (cell_height + label_height + padding) + padding

    sheet = Image.new("RGB", (width, height), (24, 26, 32))
    draw = ImageDraw.Draw(sheet)

    for index, name in enumerate(names):
        column = index % columns
        row = index // columns
        x = padding + column * (cell_width + padding)
        y = padding + row * (cell_height + label_height + padding)

        image = images[name]
        sheet.paste(image, (x, y))
        # ラベルは絵の実寸の直下に置く。縦横比の違うケースが混ざるので、
        # 升目の高さを基準にすると離れてしまう。
        draw.text((x, y + image.height + 3), name, fill=(210, 214, 224))

    destination.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(destination)
    print(f"{destination} に {len(names)} 枚を並べました")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--case", action="append", help="書き出すケース名（複数可）")
    parser.add_argument("--all", action="store_true", help="全ケースを書き出す")
    parser.add_argument("--list", action="store_true", help="ケース名を並べて終わる")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT, help="出力先ディレクトリ")
    parser.add_argument("--sheet", type=Path, help="全ケースを 1 枚に並べて書き出す")
    parser.add_argument("--columns", type=int, default=5, help="--sheet の列数")
    parser.add_argument("--compare", action="store_true", help="ゴールデンとの差分も書き出す")
    args = parser.parse_args()

    if args.list:
        for case in CASES:
            print(f"{case.name:26} {case.description}")
        return 0

    names: List[str]
    if args.all or args.sheet:
        names = sorted((case.name for case in CASES), key=sheet_key)
    elif args.case:
        unknown = [name for name in args.case if name not in CASES_BY_NAME]
        if unknown:
            print(f"知らないケースです: {', '.join(unknown)}", file=sys.stderr)
            print("--list で一覧を出せます。", file=sys.stderr)
            return 1
        names = args.case
    else:
        parser.error("--case か --all か --sheet のどれかを指定してください")
        return 1

    print(f"レンダラ: {renderer_info()}")

    if args.sheet:
        write_sheet(names, args.sheet, max(args.columns, 1))
        return 0

    args.output.mkdir(parents=True, exist_ok=True)
    for name in names:
        case = CASES_BY_NAME[name]
        image = render_case(case)

        target = args.output / f"{name}.png"
        cmp.save_png(target, image)
        print(f"{target}")

        if args.compare:
            golden = GOLDEN_DIR / case.golden_name
            if not golden.exists():
                print(f"  ゴールデンがありません: {golden}")
                continue

            expected = cmp.load_png(golden)
            diff = cmp.compare(image, expected)
            print(f"  ゴールデンとの差: {diff.summary()}")
            if diff.max > 0:
                cmp.save_png(args.output / f"{name}.diff.png", cmp.diff_image(image, expected))

    return 0


if __name__ == "__main__":
    sys.exit(main())
