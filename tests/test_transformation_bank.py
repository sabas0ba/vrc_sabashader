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


def test_bank_exposes_two_roles_styles_and_animation_progress():
    source = (BANK_DIR / "properties.hlsl").read_text(encoding="utf-8")

    assert re.search(r"SC_float\(_Progress,\s*1\s*,", source)
    assert "Incoming,0,Outgoing,1" in source
    assert "Safety Cover" not in source
    assert (
        "Arcane,0,Cyber,1,Astral,2,Gaia,3,Umbra,4,"
        "Flame,5,Shatter,6,Glitch,7,Melt,8,Cosmic Rift,9,"
        "Magical Sparkle,10,Mana Mist,11"
    ) in source
    assert "_IncomingOutgoingWindow" in source
    assert "_EffectIntensity" in source
    assert "_CoverWindow" not in source
    assert "_CoverColor" not in source


def test_default_timeline_crossfades_without_a_visibility_gap():
    """既定タイミングでは旧衣装と新衣装の表示率の和が1を下回らない。"""
    incoming_start, incoming_end, outgoing_start, outgoing_end = _vector_default(
        "_IncomingOutgoingWindow"
    )
    assert incoming_start <= outgoing_start <= incoming_end <= outgoing_end

    for index in range(1001):
        progress = index / 1000.0
        incoming = _smoothstep(incoming_start, incoming_end, progress)
        outgoing = 1.0 - _smoothstep(outgoing_start, outgoing_end, progress)
        assert incoming + outgoing >= 1.0 - 1.0e-6


def test_activity_envelope_eases_into_both_endpoints():
    source = (BANK_DIR / "TransformationBankCore.hlsl").read_text(encoding="utf-8")
    assert "half bell = 4.0 * progress * (1.0 - progress);" in source
    assert "return bell * bell;" in source

    def activity(progress: float) -> float:
        bell = 4.0 * progress * (1.0 - progress)
        return bell * bell

    epsilon = 1.0e-3
    assert activity(0.5) == pytest.approx(1.0)
    assert activity(0.2) < 0.5
    assert activity(0.8) < 0.5
    assert activity(epsilon) / epsilon < 0.02
    assert activity(1.0 - epsilon) / epsilon < 0.02


def test_style_perturbations_ease_at_role_window_boundaries():
    source = (BANK_DIR / "TransformationBankCore.hlsl").read_text(encoding="utf-8")
    flame = source[source.index("half SBSBankFlameField"):source.index("half SBSBankGlitchAmount")]
    glitch = source[source.index("half SBSBankGlitchAmount"):source.index("half SBSBankMeltField")]
    melt = source[source.index("half SBSBankMeltField"):source.index("half SBSBankCosmicRiftField")]
    cosmic = source[source.index("half SBSBankCosmicRiftField"):source.index("half SBSBankSparkleField")]
    sparkle = source[source.index("half SBSBankSparkleField"):source.index("half SBSBankManaMistField")]
    mana = source[source.index("half SBSBankManaMistField"):source.index("half SBSBankField")]

    for field in (flame, glitch, melt, cosmic, sparkle, mana):
        assert "SBSBankActivity(st.visibilityProgress)" in field

    incoming_near_end = _smoothstep(0.25, 0.65, 0.64)
    outgoing_near_start = 1.0 - _smoothstep(0.35, 0.75, 0.36)
    for role_progress in (incoming_near_end, outgoing_near_start):
        bell = 4.0 * role_progress * (1.0 - role_progress)
        assert bell * bell < 1.0e-3


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


def test_animation_property_effect_intensity_and_particles_are_documented():
    source = DOCUMENTATION.read_text(encoding="utf-8")

    assert "material._io_github_sabas0ba_transformationbank_Progress" in source
    assert "Effect Intensity" in source
    assert "Particle System" in source
    assert "Safety Cover" not in source
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
        "Cosmic Rift",
        "Magical Sparkle",
        "Mana Mist",
    ):
        assert style in source


def test_extended_styles_define_distinct_morph_and_surface_paths():
    source = (BANK_DIR / "TransformationBankCore.hlsl").read_text(encoding="utf-8")

    assert "SBSBankFlameField" in source
    assert "SBSBankShatterDirection" in source
    assert "SBSBankGlitchAmount" in source
    assert "SBSBankMeltField" in source
    assert "liquidFront" in source
    assert "liquidWave" in source
    assert "melt * melt * 1.4" in source
    assert "if (st.role < 0.5)\n        return st.visibilityProgress - height;" not in source
    assert "if (st.role < 0.5)\n            return half3(0.0, 0.0, 0.0);" not in source
    assert "SBSBankActivity(st.visibilityProgress)" in source
    assert "SBSBankCosmicRiftField" in source
    assert "SBSBankSparkleField" in source
    assert "SBSBankManaMistField" in source
    assert "st.style < 5.5" in source
    assert "st.style < 6.5" in source
    assert "st.style < 7.5" in source
    assert "st.effectIntensity" in source


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
