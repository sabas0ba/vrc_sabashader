"""Debug shader の mode と geometry varying の構造を検証する。"""

from __future__ import annotations

import re

from harness.paths import SHADERS_DIR

DEBUG_DIR = SHADERS_DIR / "Debug"
SCSHADER = DEBUG_DIR / "Debug.scshader"
PROPERTIES = DEBUG_DIR / "Debug_properties.hlsl"
FRAGMENT = DEBUG_DIR / "DebugFragment.hlsl"
GEOMETRY = DEBUG_DIR / "DebugGeometry.hlsl"
COMMON = DEBUG_DIR / "sc_common.hlsl"

MODES = {
    "Wireframe": 0,
    "VertexColor": 1,
    "VertexAlpha": 2,
    "UV0": 3,
    "UV1": 4,
    "UV2": 5,
    "UV3": 6,
    "WorldPosition": 7,
    "ObjectPosition": 8,
    "WorldNormal": 9,
    "WorldTangent": 10,
    "WorldBitangent": 11,
    "FrontFace": 12,
    "LightDirection": 13,
    "LightColor": 14,
    "LightAttenuation": 15,
    "ViewDirection": 16,
    "ViewFacing": 17,
}


def _mode_enum() -> dict[str, int]:
    source = PROPERTIES.read_text(encoding="utf-8")
    match = re.search(r"SC_uint\(_Mode,\s*0,\s*\[SCEnum\((.*?)\)\]", source)
    assert match, "_Mode の SCEnum が見つかりません"
    entries = [entry.strip() for entry in match.group(1).split(",")]
    assert len(entries) % 2 == 0, f"SCEnum の名前と値が対になっていません: {entries}"
    return {entries[index]: int(entries[index + 1]) for index in range(0, len(entries), 2)}


def test_debug_modes_are_stable():
    assert _mode_enum() == MODES


def test_fragment_handles_every_mode():
    source = FRAGMENT.read_text(encoding="utf-8")
    branches = {int(value) for value in re.findall(r"_Mode\s*==\s*(\d+)", source)}

    assert branches == set(range(17))
    assert "half facing =" in source, "最後の ViewFacing mode がありません"


def test_wireframe_requires_geometry_shader_model():
    source = SCSHADER.read_text(encoding="utf-8")

    assert "#pragma target 4.0" in source
    assert "#pragma geometry geom" in source
    assert "#define SC_CUSTOM_V2F" in source


def test_geometry_only_reuses_uv3_for_wireframe():
    source = GEOMETRY.read_text(encoding="utf-8")

    assert source.count("if (_Mode == 0) output.uv[3]") == 3
    for barycentric in ("float2(1.0, 0.0)", "float2(0.0, 1.0)", "float2(0.0, 0.0)"):
        assert barycentric in source


def test_vertex_color_reaches_pixel_shader_through_custom_varying():
    common = COMMON.read_text(encoding="utf-8")
    fragment = FRAGMENT.read_text(encoding="utf-8")

    assert "output.customV2f.color = vertex.color;" in common
    assert "i.customV2f.color.rgb" in fragment
    assert "i.customV2f.color.a" in fragment


def test_debug_output_is_not_modified_by_modules():
    sources = "\n".join(
        path.read_text(encoding="utf-8")
        for path in sorted(DEBUG_DIR.glob("*.hlsl"))
    )

    assert "__SC_PHASE_" not in sources
