#!/usr/bin/env python3
"""`.scshader` を Unity が実際に見る ShaderLab まで展開して書き出す。

Shader Core のインポータが何を生成しているのかを Unity 無しで確認したり、
Unity 側でエラーが出たときに行番号を突き合わせたりするためのツール。

    python tools/expand_shader.py
    python tools/expand_shader.py --output /tmp/Illust2D.shader
    python tools/expand_shader.py --no-modules

既定ではパッケージのモジュールを全部有効にした状態で展開する。Unity では
シェーダーごとに有効なモジュールを選べるので、素の状態を見たいときは
`--no-modules` を渡す。
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO_ROOT / "tests"))

from harness.paths import SCSHADER  # noqa: E402
from harness.scshader import (  # noqa: E402
    ShaderExpander,
    ensure_shadercore,
    package_modules,
    package_roots,
)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--shader", type=Path, default=SCSHADER)
    parser.add_argument("--output", type=Path, help="省略時は標準出力")
    parser.add_argument(
        "--no-modules",
        action="store_true",
        help="モジュールを差し込まずに展開する",
    )
    args = parser.parse_args()

    shadercore = ensure_shadercore()
    if shadercore is None:
        print(
            "Shader Core を取得できませんでした。ネットワークに繋がる環境で実行してください。",
            file=sys.stderr,
        )
        return 1

    modules = [] if args.no_modules else package_modules()
    result = ShaderExpander(args.shader, package_roots(shadercore), modules).expand()

    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(result.source, encoding="utf-8")
        print(f"{args.output} に書き出しました（{len(result.source.splitlines())} 行）", file=sys.stderr)
    else:
        print(result.source)

    print(f"フェーズ: {', '.join(result.phases)}", file=sys.stderr)
    if modules:
        print(f"モジュール: {', '.join(m.unique_id for m in modules)}", file=sys.stderr)
    if result.unresolved_includes:
        print(f"未解決の include: {', '.join(sorted(set(result.unresolved_includes)))}", file=sys.stderr)

    return 0


if __name__ == "__main__":
    sys.exit(main())
