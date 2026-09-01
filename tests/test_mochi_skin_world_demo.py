"""Mochi SkinのWorld展示用UPM sampleを検証する。"""

from __future__ import annotations

import json

from PIL import Image

from harness.paths import PACKAGE_DIR, REPO_ROOT
from tools.gen_meta import guid_for

SAMPLE_DIR = PACKAGE_DIR / "Samples~" / "MochiSkinWorldDemo"
PACKAGE_JSON = PACKAGE_DIR / "package.json"
BUILDER = (
    REPO_ROOT
    / ".ci"
    / "UnityProject"
    / "Assets"
    / "Editor"
    / "MochiSkinWorldDemoBuilder.cs"
)
DOCUMENTATION = REPO_ROOT / "docs" / "modules-advanced.md"
CAPTURE = REPO_ROOT / "tests" / "golden" / "mochi_skin_world_demo.png"


def test_package_declares_mochi_skin_world_demo_sample():
    package = json.loads(PACKAGE_JSON.read_text(encoding="utf-8"))
    sample = next(
        item for item in package["samples"] if item["displayName"] == "Mochi Skin World Demo"
    )

    assert sample["path"] == "Samples~/MochiSkinWorldDemo"
    assert SAMPLE_DIR.is_dir()


def test_world_demo_contains_scene_component_editor_and_readme():
    expected = {
        "MochiSkinWorldDemo.unity",
        "MochiSkinWorldDemoObject.cs",
        "Editor",
        "README.md",
    }
    assert expected <= {path.name for path in SAMPLE_DIR.iterdir()}

    scene = (SAMPLE_DIR / "MochiSkinWorldDemo.unity").read_text(encoding="utf-8")
    component_guid = guid_for(
        (SAMPLE_DIR / "MochiSkinWorldDemoObject.cs").relative_to(PACKAGE_DIR).as_posix()
    )
    assert scene.count(f"guid: {component_guid}") == 2
    assert "Contact Driven Surface" in scene
    assert scene.count("  pressure0: ") == 2


def test_world_demo_uses_transient_dense_mesh_and_mochi_properties():
    source = (SAMPLE_DIR / "MochiSkinWorldDemoObject.cs").read_text(encoding="utf-8")

    assert 'ShaderName = "SabaShader/Illust2D"' in source
    assert 'MochiSkin = "_io_github_sabas0ba_mochiskin_"' in source
    assert "HorizontalSegments = 64" in source
    assert "VerticalSegments = 48" in source
    assert "HideFlags.HideAndDontSave" in source
    assert "SetFloat(MochiSkin + \"Pressure\" + index" in source
    assert "Application.isPlaying" in source
    assert '[AddComponentMenu("")]' in source


def test_world_demo_is_marked_sample_only_and_explains_vrc_boundary():
    editor = (SAMPLE_DIR / "Editor" / "MochiSkinWorldDemoObjectEditor.cs").read_text(
        encoding="utf-8"
    )
    readme = (SAMPLE_DIR / "README.md").read_text(encoding="utf-8")

    assert "SAMPLE ONLY / サンプル専用" in editor
    assert "Auto Animate in Play Mode" in editor
    assert "Rebuild Demo Preview" in editor
    assert "VRCSDKに依存せず" in readme
    assert "アップロードするWorldへ追加しない" in readme
    assert "material._io_github_sabas0ba_mochiskin_Pressure0" in readme


def test_world_demo_builder_has_stable_scene_and_capture_mapping():
    source = BUILDER.read_text(encoding="utf-8")

    assert "PackageInfo.FindForAssetPath" in source
    assert 'MochiSkinWorldDemo.unity"' in source
    assert '"Rest Surface"' in source
    assert '"Contact Driven Surface"' in source
    assert "CreateProbe(patch.transform, index)" in source
    assert '"mochi_skin_world_demo.png"' in source
    assert "1920," in source and "1080);" in source


def test_world_demo_capture_is_documented_and_has_expected_size():
    documentation = DOCUMENTATION.read_text(encoding="utf-8")

    assert "../tests/golden/mochi_skin_world_demo.png" in documentation
    assert CAPTURE.is_file()
    with Image.open(CAPTURE) as image:
        assert image.size == (1920, 1080)
