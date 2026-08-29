"""Transformation BankのUPM Demo Sceneを検証する。"""

from __future__ import annotations

import json

from PIL import Image

from harness.paths import PACKAGE_DIR, REPO_ROOT
from tools.gen_meta import guid_for

SAMPLE_DIR = PACKAGE_DIR / "Samples~" / "TransformationBankDemo"
PACKAGE_JSON = PACKAGE_DIR / "package.json"
BUILDER = REPO_ROOT / ".ci" / "UnityProject" / "Assets" / "Editor" / "TransformationBankDemoBuilder.cs"
DOCUMENTATION = REPO_ROOT / "docs" / "transformation-bank.md"
CAPTURE = REPO_ROOT / "tests" / "golden" / "transformation_bank_demo.png"


def test_package_declares_transformation_bank_demo():
    package = json.loads(PACKAGE_JSON.read_text(encoding="utf-8"))
    sample = next(item for item in package["samples"] if item["displayName"] == "Transformation Bank Demo")

    assert sample["path"] == "Samples~/TransformationBankDemo"
    assert (PACKAGE_DIR / sample["path"]).is_dir()


def test_sample_contains_scene_controller_inspector_and_readme():
    assert {
        "TransformationBankDemo.unity",
        "TransformationBankDemoController.cs",
        "Editor",
        "README.md",
    } <= {path.name for path in SAMPLE_DIR.iterdir()}

    scene = (SAMPLE_DIR / "TransformationBankDemo.unity").read_text(encoding="utf-8")
    component_guid = guid_for(
        (SAMPLE_DIR / "TransformationBankDemoController.cs").relative_to(PACKAGE_DIR).as_posix()
    )
    assert scene.count(f"guid: {component_guid}") == 14
    assert scene.count("  animateInPlayMode: 1") == 9
    assert scene.count("  animateInPlayMode: 0") == 5


def test_demo_controller_generates_three_transient_role_materials():
    source = (SAMPLE_DIR / "TransformationBankDemoController.cs").read_text(encoding="utf-8")

    assert 'ShaderName = "SabaShader/Illust2D"' in source
    assert "HideFlags.HideAndDontSave" in source
    assert 'CreateRoleMaterial(shader, 1, "Outgoing")' in source
    assert 'CreateRoleMaterial(shader, 0, "Incoming")' in source
    assert 'CreateRoleMaterial(shader, 2, "Safety Cover")' in source
    assert "Mathf.PingPong" in source
    assert "MaterialPropertyBlock" in source
    assert "StabilizeTextRendering();" in source
    assert '[AddComponentMenu("")]' in source
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


def test_demo_inspector_is_sample_only_and_supports_manual_scrub():
    source = (SAMPLE_DIR / "Editor" / "TransformationBankDemoControllerEditor.cs").read_text(
        encoding="utf-8"
    )

    assert "SAMPLE ONLY / サンプル専用" in source
    assert "Auto Animate in Play Mode" in source
    assert "Rebuild Demo Preview" in source
    assert "material._io_github_sabas0ba_transformationbank_Progress" in source


def test_builder_has_nine_styles_distinct_roles_five_timeline_snapshots_and_capture():
    source = BUILDER.read_text(encoding="utf-8")

    assert '"Flame", "Shatter", "Glitch", "Melt"' in source
    assert "TimelineProgress = { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f }" in source
    assert 'CreateShell(station.transform, "Outgoing / Old Outfit", PrimitiveType.Capsule' in source
    assert 'CreateShell(station.transform, "Incoming / New Outfit", PrimitiveType.Cylinder' in source
    assert 'CreateShell(station.transform, "Safety Cover", PrimitiveType.Sphere' in source
    assert '"transformation_bank_demo.png"' in source
    assert ", 2560, 1440);" in source


def test_documentation_uses_select_modules_and_real_unity_capture():
    documentation = DOCUMENTATION.read_text(encoding="utf-8")

    assert "Select Modules" in documentation
    assert "Shader Core の Project Settings" not in documentation
    assert "../tests/golden/transformation_bank_demo.png" in documentation
    assert CAPTURE.is_file()
    with Image.open(CAPTURE) as image:
        assert image.size == (2560, 1440)
