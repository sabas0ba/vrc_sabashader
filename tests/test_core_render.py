"""Illust2DCore.hlsl をヘッドレスで描画してゴールデン画像と比較する。

シェーディングの数式を変えると必ずここが落ちる。意図した変更なら
    pytest tests -k render --update-goldens
でゴールデンを更新し、差分画像を確認してからコミットすること。
"""

from __future__ import annotations

import os

import pytest

from cases import CASES
from harness import compare as cmp
from harness.glsl import build_scene_source, module_cores, parse_struct_fields, read_core
from harness.paths import ARTIFACT_DIR, GOLDEN_DIR
from harness.render import render_fragment, renderer_info

# llvmpipe のバージョン差で 1 階調程度はずれうるので許容幅を持たせる。
MEAN_TOLERANCE = float(os.environ.get("SABASHADER_GOLDEN_MEAN_TOL", "1.5"))
MAX_TOLERANCE = float(os.environ.get("SABASHADER_GOLDEN_MAX_TOL", "12"))


def _render(case) -> "object":
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


def test_headless_context_available():
    assert renderer_info()


def test_style_struct_matches_cases():
    """SBSStyle にフィールドを足したらケース側の初期値も足させる。"""
    from cases import DEFAULT_STYLE

    fields = {name for _, name in parse_struct_fields(read_core(), "SBSStyle")}
    assert fields == set(DEFAULT_STYLE), (
        "Illust2DCore.hlsl の SBSStyle と tests/cases.py の DEFAULT_STYLE がずれています: "
        f"コアのみ={sorted(fields - set(DEFAULT_STYLE))} ケースのみ={sorted(set(DEFAULT_STYLE) - fields)}"
    )


def test_overlay_struct_matches_cases():
    """SBSOverlayStyle にフィールドを足したらケース側の初期値も足させる。"""
    from cases import DEFAULT_OVERLAY

    for name, body in module_cores():
        if "struct SBSOverlayStyle" not in body:
            continue
        fields = {field for _, field in parse_struct_fields(body, "SBSOverlayStyle")}
        assert fields == set(DEFAULT_OVERLAY), (
            f"{name} の SBSOverlayStyle と tests/cases.py の DEFAULT_OVERLAY がずれています: "
            f"コアのみ={sorted(fields - set(DEFAULT_OVERLAY))} "
            f"ケースのみ={sorted(set(DEFAULT_OVERLAY) - fields)}"
        )


def test_video_input_struct_matches_cases():
    """SBSVideoInputStyle にフィールドを足したらケース側の初期値も足させる。"""
    from cases import DEFAULT_VIDEO_INPUT

    for name, body in module_cores():
        if "struct SBSVideoInputStyle" not in body:
            continue
        fields = {field for _, field in parse_struct_fields(body, "SBSVideoInputStyle")}
        assert fields == set(DEFAULT_VIDEO_INPUT), (
            f"{name} の SBSVideoInputStyle と tests/cases.py の DEFAULT_VIDEO_INPUT がずれています: "
            f"コアのみ={sorted(fields - set(DEFAULT_VIDEO_INPUT))} "
            f"ケースのみ={sorted(set(DEFAULT_VIDEO_INPUT) - fields)}"
        )


def test_display_panel_struct_matches_cases():
    """SBSDisplayPanelStyle とケース側の初期値を一致させる。"""
    from cases import DEFAULT_DISPLAY_PANEL

    for name, body in module_cores():
        if "struct SBSDisplayPanelStyle" not in body:
            continue
        fields = {field for _, field in parse_struct_fields(body, "SBSDisplayPanelStyle")}
        assert fields == set(DEFAULT_DISPLAY_PANEL), (
            f"{name} の SBSDisplayPanelStyle と tests/cases.py の DEFAULT_DISPLAY_PANEL がずれています: "
            f"コアのみ={sorted(fields - set(DEFAULT_DISPLAY_PANEL))} "
            f"ケースのみ={sorted(set(DEFAULT_DISPLAY_PANEL) - fields)}"
        )


@pytest.mark.parametrize("case", CASES, ids=lambda c: c.name)
def test_render_matches_golden(case, update_goldens):
    image = _render(case)
    golden_path = GOLDEN_DIR / case.golden_name

    if update_goldens:
        cmp.save_png(golden_path, image)
        pytest.skip(f"ゴールデンを更新しました: {golden_path.relative_to(GOLDEN_DIR.parent.parent)}")

    if not golden_path.exists():
        cmp.save_png(ARTIFACT_DIR / f"{case.name}.actual.png", image)
        pytest.fail(
            f"ゴールデン画像がありません: {golden_path}\n"
            "`pytest tests --update-goldens` で生成してください。"
        )

    expected = cmp.load_png(golden_path)
    diff = cmp.compare(image, expected)

    if diff.mean > MEAN_TOLERANCE or diff.max > MAX_TOLERANCE:
        cmp.save_png(ARTIFACT_DIR / f"{case.name}.actual.png", image)
        cmp.save_png(ARTIFACT_DIR / f"{case.name}.expected.png", expected)
        cmp.save_png(ARTIFACT_DIR / f"{case.name}.diff.png", cmp.diff_image(image, expected))
        pytest.fail(
            f"{case.name} がゴールデンと一致しません ({diff.summary()})\n"
            f"  {case.description}\n"
            f"  差分画像: {ARTIFACT_DIR}\n"
            f"  レンダラ: {renderer_info()}"
        )


def test_all_cases_have_unique_names():
    names = [case.name for case in CASES]
    assert len(names) == len(set(names))
