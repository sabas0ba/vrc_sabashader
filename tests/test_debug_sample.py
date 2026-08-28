"""Debug shader の UPM sample 構成を検証する。"""

from __future__ import annotations

import json

from harness.paths import PACKAGE_DIR, REPO_ROOT

SAMPLE_DIR = PACKAGE_DIR / "Samples~" / "DebugShaderDemo"
PACKAGE_JSON = PACKAGE_DIR / "package.json"
BUILDER = REPO_ROOT / ".ci" / "UnityProject" / "Assets" / "Editor" / "DebugShaderDemoBuilder.cs"


def test_package_declares_debug_shader_sample():
    package = json.loads(PACKAGE_JSON.read_text(encoding="utf-8"))
    sample = next(item for item in package["samples"] if item["displayName"] == "Debug Shader Demo")

    assert sample["path"] == "Samples~/DebugShaderDemo"
    assert (PACKAGE_DIR / sample["path"]).is_dir()


def test_sample_contains_scene_component_and_readme():
    expected = {
        "DebugShaderDemo.unity",
        "DebugShaderDemoObject.cs",
        "README.md",
    }
    assert expected <= {path.name for path in SAMPLE_DIR.iterdir()}

    scene = (SAMPLE_DIR / "DebugShaderDemo.unity").read_text(encoding="utf-8")
    assert scene.count("  mode: ") == 18
    assert scene.count("guid: de39db50900b55e19ca09f08149e7836") == 18


def test_demo_component_covers_all_modes_without_persistent_generated_assets():
    source = (SAMPLE_DIR / "DebugShaderDemoObject.cs").read_text(encoding="utf-8")

    assert "Range(0, 17)" in source
    assert 'ShaderName = "SabaShader/Debug"' in source
    assert "HideFlags.HideAndDontSave" in source
    for channel in range(1, 4):
        assert f"SetUVs({channel}," in source


def test_builder_contains_stable_mode_mapping_and_scene_path():
    source = BUILDER.read_text(encoding="utf-8")

    assert source.count('"Wireframe"') == 1
    assert source.count('"View Facing"') == 1
    assert "ModeNames.Length" in source
    assert "PackageInfo.FindForAssetPath" in source
    assert 'DebugShaderDemo.unity"' in source
