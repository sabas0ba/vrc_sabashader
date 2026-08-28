"""Decal、Surface Detail、Spatial Interior、Transition とUPM sampleを検証する。"""

from __future__ import annotations

import json
import re

from PIL import Image

from harness.paths import PACKAGE_DIR, REPO_ROOT
from tools.gen_meta import guid_for

MODULES_DIR = PACKAGE_DIR / "Modules"
SAMPLE_DIR = PACKAGE_DIR / "Samples~" / "AdvancedShaderSuiteDemo"
PACKAGE_JSON = PACKAGE_DIR / "package.json"
BUILDER = REPO_ROOT / ".ci" / "UnityProject" / "Assets" / "Editor" / "AdvancedShaderDemoBuilder.cs"
DOCUMENTATION = REPO_ROOT / "docs" / "modules-advanced.md"
SETUP_UNITY = REPO_ROOT / "tools" / "setup_unity_project.py"
CAPTURES = {
    "advanced_shader_suite_demo.png": (2560, 1440),
    "advanced_shader_surface_features.png": (2560, 960),
    "advanced_shader_transitions.png": (2560, 480),
}

MODULE_DESCRIPTORS = {
    "Decal/decal.scmodule": ("io.github.sabas0ba.decal", {"base"}),
    "SurfaceDetail/surface-detail.scmodule": (
        "io.github.sabas0ba.surfacedetail",
        {"base", "add"},
    ),
    "SpatialInterior/spatial-interior.scmodule": (
        "io.github.sabas0ba.spatialinterior",
        {"postpixel"},
    ),
    "Transition/transition.scmodule": (
        "io.github.sabas0ba.transition",
        {"morph", "base", "postpixel", "pixelclip", "outlineclip"},
    ),
}


def test_module_descriptors_have_stable_ids_and_phases():
    for relative, (unique_id, phases) in MODULE_DESCRIPTORS.items():
        descriptor = json.loads((MODULES_DIR / relative).read_text(encoding="utf-8"))
        assert descriptor["uniqueID"] == unique_id
        assert {entry["phase"] for entry in descriptor["phases"]} == phases


def test_modules_are_disabled_by_default():
    for directory in ("Decal", "SurfaceDetail", "SpatialInterior"):
        source = (MODULES_DIR / directory / "properties.hlsl").read_text(encoding="utf-8")
        assert re.search(r"SC_float\(_Amount,\s*0\s*,", source), directory

    transition = (MODULES_DIR / "Transition" / "properties.hlsl").read_text(encoding="utf-8")
    assert re.search(r"SC_float\(_Progress,\s*1\s*,", transition)


def test_decal_supports_uv_projection_and_three_blend_modes():
    properties = (MODULES_DIR / "Decal" / "properties.hlsl").read_text(encoding="utf-8")
    core = (MODULES_DIR / "Decal" / "DecalCore.hlsl").read_text(encoding="utf-8")

    assert "SCEnum(UV Space,0,Projection,1)" in properties
    assert "SCEnum(UV0,0,UV1,1,UV2,2,UV3,3)" in properties
    assert "SCEnum(Alpha,0,Multiply,1,Add,2)" in properties
    assert "objectPosition - st.projectorCenter" in core
    assert "angleCoverage" in core and "depthCoverage" in core


def test_surface_detail_changes_micro_normal_roughness_and_sheen():
    core = (MODULES_DIR / "SurfaceDetail" / "SurfaceDetailCore.hlsl").read_text(encoding="utf-8")

    for function in (
        "SBSSurfaceDetailSkinHeight",
        "SBSSurfaceDetailFabricHeight",
        "SBSSurfaceDetailNormal",
        "SBSSurfaceDetailRoughness",
        "SBSSurfaceDetailSpecular",
    ):
        assert function in core


def test_spatial_interior_has_side_region_mask_and_view_parallax():
    properties = (MODULES_DIR / "SpatialInterior" / "properties.hlsl").read_text(encoding="utf-8")
    core = (MODULES_DIR / "SpatialInterior" / "SpatialInteriorCore.hlsl").read_text(encoding="utf-8")
    phase = (MODULES_DIR / "SpatialInterior" / "spatial_interior_postpixel.hlsl").read_text(
        encoding="utf-8"
    )

    assert "SCEnum(Universe,0,Starfield,1,Cyber,2,Mud,3)" in properties
    assert "SCEnum(Front,0,Back,1,Both,2)" in properties
    assert "SCEnum(Full Surface,0,Rift,1)" in properties
    assert "view * max(st.depth" in core
    assert "SBSSpatialRegion" in core and "SBSSpatialSideMask" in core
    for preset_function in (
        "SBSSpatialUniverse",
        "SBSSpatialStarfield",
        "SBSSpatialCyber",
        "SBSSpatialMud",
    ):
        assert preset_function in core
    assert "dot(normalize(vertex.N), normalize(vertex.V))" in phase


def test_transition_clips_forward_shadow_and_outline_with_one_progress():
    phase_files = sorted((MODULES_DIR / "Transition").glob("transition_*.hlsl"))
    assert len(phase_files) == 5
    for path in phase_files:
        source = path.read_text(encoding="utf-8")
        assert "transitionStyle.progress = _Progress;" in source, path.name

    common = (PACKAGE_DIR / "Shaders" / "Illust2D" / "sc_common.hlsl").read_text(encoding="utf-8")
    outline = (
        PACKAGE_DIR / "Shaders" / "Illust2D" / "Illust2DOutlineFragment.hlsl"
    ).read_text(encoding="utf-8")
    assert "__SC_PHASE_pixelclip__" in common
    assert "__SC_PHASE_outlineclip__" in outline
    transition_core = (MODULES_DIR / "Transition" / "TransitionCore.hlsl").read_text(
        encoding="utf-8"
    )
    assert not re.search(r"\b(?:half|float)\s+active\b", transition_core)


def test_liquid_transition_supports_irregular_wobble_and_puddle_initial_state():
    properties = (MODULES_DIR / "Transition" / "properties.hlsl").read_text(encoding="utf-8")
    core = (MODULES_DIR / "Transition" / "TransitionCore.hlsl").read_text(encoding="utf-8")

    for property_name in (
        "_LiquidWobble",
        "_LiquidPuddle",
        "_LiquidPuddleHeight",
        "_LiquidPuddleSpread",
    ):
        assert property_name in properties
    assert "waveA" in core and "waveB" in core and "waveC" in core
    assert "targetHeight" in core and "puddleOffset" in core
    assert "saturate(st.liquidPuddle)" in core


def test_package_declares_advanced_suite_sample():
    package = json.loads(PACKAGE_JSON.read_text(encoding="utf-8"))
    sample = next(
        item for item in package["samples"] if item["displayName"] == "Advanced Shader Suite Demo"
    )

    assert sample["path"] == "Samples~/AdvancedShaderSuiteDemo"
    assert (PACKAGE_DIR / sample["path"]).is_dir()


def test_sample_contains_scene_component_and_readme():
    expected = {
        "AdvancedShaderSuiteDemo.unity",
        "AdvancedShaderDemoObject.cs",
        "Editor",
        "README.md",
        "Textures",
    }
    assert expected <= {path.name for path in SAMPLE_DIR.iterdir()}

    scene = (SAMPLE_DIR / "AdvancedShaderSuiteDemo.unity").read_text(encoding="utf-8")
    component_guid = guid_for(
        (SAMPLE_DIR / "AdvancedShaderDemoObject.cs").relative_to(PACKAGE_DIR).as_posix()
    )
    assert scene.count(f"guid: {component_guid}") == 11
    assert scene.count("  feature: ") == 11


def test_demo_component_uses_transient_generated_assets_and_all_features():
    source = (SAMPLE_DIR / "AdvancedShaderDemoObject.cs").read_text(encoding="utf-8")

    assert 'ShaderName = "SabaShader/Illust2D"' in source
    assert "HideFlags.HideAndDontSave" in source
    assert "decalTextureAsset" in source
    assert "CreateDecalTexture" not in source
    assert "normals[index] = -normals[index]" in source
    assert '[AddComponentMenu("")]' in source
    assert "ApplyProgress();" in source
    for feature in (
        "DecalUV",
        "DecalProjection",
        "SkinDetail",
        "FabricDetail",
        "SpatialUniverseRift",
        "SpatialStarfield",
        "SpatialCyberBack",
        "SpatialMud",
        "UpwardDissolve",
        "GlitchSpawn",
        "LiquidSolid",
    ):
        assert feature in source


def test_demo_component_is_clearly_marked_as_sample_only():
    editor = (SAMPLE_DIR / "Editor" / "AdvancedShaderDemoObjectEditor.cs").read_text(
        encoding="utf-8"
    )

    assert "SAMPLE ONLY / サンプル専用" in editor
    assert "EditorGUILayout.HelpBox" in editor
    assert "Auto Animate in Play Mode" in editor
    assert "Rebuild Demo Preview" in editor
    assert "EditorGUI.DisabledScope(animateInPlayMode.boolValue)" in editor


def test_builder_contains_stable_scene_and_capture_mapping():
    source = BUILDER.read_text(encoding="utf-8")

    assert "FeatureNames.Length" in source
    assert "PackageInfo.FindForAssetPath" in source
    assert 'AdvancedShaderSuiteDemo.unity"' in source
    assert 'PrimitiveType.Cylinder' in source
    assert 'DecalDemoEmblem.png' in source
    for filename, (width, height) in CAPTURES.items():
        assert f'"{filename}"' in source
        assert f", {width}, {height}," in source


def test_decal_demo_uses_a_directional_transparent_logo_texture():
    path = SAMPLE_DIR / "Textures" / "DecalDemoEmblem.png"

    assert path.is_file()
    with Image.open(path) as image:
        assert image.mode == "RGBA"
        assert image.getextrema()[3][0] == 0
        assert image.getextrema()[3][1] == 255
        assert min(image.size) >= 512

    meta = path.with_name(path.name + ".meta").read_text(encoding="utf-8")
    assert "TextureImporter:" in meta
    assert "alphaIsTransparency: 1" in meta


def test_setup_unity_project_copies_every_declared_sample():
    source = SETUP_UNITY.read_text(encoding="utf-8")

    assert "def copy_samples(" in source
    assert 'for sample in package.get("samples", [])' in source
    assert "copy_debug_sample" not in source


def test_documentation_captures_have_expected_dimensions():
    golden = REPO_ROOT / "tests" / "golden"
    documentation = DOCUMENTATION.read_text(encoding="utf-8")
    for filename, expected_size in CAPTURES.items():
        path = golden / filename
        assert f"../tests/golden/{filename}" in documentation
        assert path.is_file(), f"Unity capture がありません: {path.relative_to(REPO_ROOT)}"
        with Image.open(path) as image:
            assert image.size == expected_size


def test_transition_animation_property_is_documented_verbatim():
    documentation = DOCUMENTATION.read_text(encoding="utf-8")
    sample_readme = (SAMPLE_DIR / "README.md").read_text(encoding="utf-8")
    property_name = "material._io_github_sabas0ba_transition_Progress"

    assert property_name in documentation
    assert property_name.removeprefix("material.") in sample_readme
