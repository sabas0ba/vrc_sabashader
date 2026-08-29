"""衣装変身バンクと NonToon 互換経路の検証。"""

from __future__ import annotations

import re
from pathlib import Path

import pytest

from harness.paths import (
    MODULES_DIR,
    NONTOON_COMMIT,
    NONTOON_PACKAGE_PATH,
    PACKAGE_DIR,
    SHADERCORE_PACKAGE_PATH,
)
from harness.scshader import ShaderExpander, load_module
from harness.scshader import ensure_nontoon, ensure_shadercore

BANK_DIR = MODULES_DIR / "TransformationBank"
BANK_MODULE = BANK_DIR / "transformation-bank.scmodule"
SETUP_UNITY = PACKAGE_DIR.parents[1] / "tools" / "setup_unity_project.py"
DOCUMENTATION = PACKAGE_DIR.parents[1] / "docs" / "transformation-bank.md"


def _smoothstep(start: float, end: float, value: float) -> float:
    value = min(max((value - start) / (end - start), 0.0), 1.0)
    return value * value * (3.0 - 2.0 * value)


def _vector_default(property_name: str) -> tuple[float, float, float, float]:
    source = (BANK_DIR / "properties.hlsl").read_text(encoding="utf-8")
    match = re.search(rf"SC_float4\({re.escape(property_name)},\s*\(([^)]+)\)", source)
    assert match, f"{property_name} の既定値を読めません"
    values = tuple(float(value) for value in match.group(1).split(","))
    assert len(values) == 4
    return values


def test_bank_exposes_roles_styles_and_animation_progress():
    source = (BANK_DIR / "properties.hlsl").read_text(encoding="utf-8")

    assert re.search(r"SC_float\(_Progress,\s*1\s*,", source)
    assert "Incoming,0,Outgoing,1,Safety Cover,2" in source
    assert (
        "Arcane,0,Cyber,1,Astral,2,Gaia,3,Umbra,4,"
        "Flame,5,Shatter,6,Glitch,7,Melt,8"
    ) in source
    assert "_IncomingOutgoingWindow" in source
    assert "_CoverWindow" in source


def test_default_timeline_always_has_one_complete_covering_layer():
    """既定タイミングでは旧衣装、cover、新衣装のいずれかが完全表示される。"""
    incoming_start, incoming_end, outgoing_start, outgoing_end = _vector_default(
        "_IncomingOutgoingWindow"
    )
    cover_in_start, cover_in_end, cover_out_start, cover_out_end = _vector_default(
        "_CoverWindow"
    )
    assert incoming_start <= incoming_end <= cover_out_start <= cover_out_end
    assert cover_in_start <= cover_in_end <= outgoing_start <= outgoing_end

    for index in range(1001):
        progress = index / 1000.0
        incoming = _smoothstep(incoming_start, incoming_end, progress)
        outgoing = 1.0 - _smoothstep(outgoing_start, outgoing_end, progress)
        cover = _smoothstep(cover_in_start, cover_in_end, progress) * (
            1.0 - _smoothstep(cover_out_start, cover_out_end, progress)
        )
        assert max(incoming, outgoing, cover) >= 1.0 - 1.0e-6


def test_bank_uses_common_base_and_illust2d_clip_phases():
    module = load_module(BANK_MODULE)
    phases = {phase.phase for phase in module.phases}

    assert module.unique_id == "io.github.sabas0ba.transformationbank"
    assert phases == {"morph", "base", "postpixel", "pixelclip", "outlineclip"}
    assert "SBSBankRoleProgress" in (BANK_DIR / "TransformationBankCore.hlsl").read_text(
        encoding="utf-8"
    )


def test_nontoon_release_is_pinned_in_unity_project_setup():
    source = SETUP_UNITY.read_text(encoding="utf-8")

    assert NONTOON_COMMIT in source
    assert 'clone_nontoon(packages / "jp.lilxyzw.nontoon")' in source
    assert 'shader == "NonToon"' in source
    assert "io.github.sabas0ba.transformationbank" in source


def test_animation_property_and_safety_constraints_are_documented():
    source = DOCUMENTATION.read_text(encoding="utf-8")

    assert "material._io_github_sabas0ba_transformationbank_Progress" in source
    assert "Opaque" in source
    assert "Safety Cover" in source
    assert NONTOON_COMMIT in source
    for style in (
        "Arcane",
        "Cyber",
        "Astral",
        "Gaia",
        "Umbra",
        "Flame",
        "Shatter",
        "Glitch",
        "Melt",
    ):
        assert style in source


def test_extended_styles_define_distinct_morph_and_surface_paths():
    source = (BANK_DIR / "TransformationBankCore.hlsl").read_text(encoding="utf-8")

    assert "SBSBankFlameField" in source
    assert "SBSBankShatterDirection" in source
    assert "SBSBankGlitchAmount" in source
    assert "SBSBankMeltField" in source
    assert "st.style < 5.5" in source
    assert "st.style < 6.5" in source
    assert "st.style < 7.5" in source
    assert "st.role < 0.5" in source


def test_nontoon_expansion_includes_bank_clip_and_properties():
    nontoon = ensure_nontoon()
    shadercore = ensure_shadercore()
    if nontoon is None or shadercore is None:
        pytest.skip("固定した NonToon または Shader Core を取得できませんでした")

    shader = nontoon / "Shaders" / "NonToon.scshader"
    roots = {
        SHADERCORE_PACKAGE_PATH: shadercore,
        NONTOON_PACKAGE_PATH: nontoon,
        "Packages/io.github.sabas0ba.sabashader": PACKAGE_DIR,
    }
    result = ShaderExpander(shader, roots, [load_module(BANK_MODULE)]).expand()

    assert "_io_github_sabas0ba_transformationbank_Progress" in result.source
    # NonToon は base phase を通常描画と SCPixelClip の双方で呼ぶ。
    assert result.source.count("SBSBankVisibility") >= 2
    assert not any("Modules/TransformationBank" in path for path in result.unresolved_includes)


def test_nontoon_license_is_not_vendored_into_the_package():
    """NonToon は検証時に取得し、配布パッケージへ第三者コードを混ぜない。"""
    vendored = list(Path(PACKAGE_DIR).rglob("*NonToon*"))
    assert not vendored
