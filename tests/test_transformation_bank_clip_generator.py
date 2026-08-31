"""Transformation Bank Animation Clip Generatorの静的構成を検証する。"""

from __future__ import annotations

from pathlib import Path

from harness.paths import PACKAGE_DIR, REPO_ROOT

EDITOR_DIR = PACKAGE_DIR / "Editor"
GENERATOR = EDITOR_DIR / "TransformationBankClipGenerator.cs"
WINDOW = EDITOR_DIR / "TransformationBankClipGeneratorWindow.cs"
REPORT = EDITOR_DIR / "TransformationBankGenerationReport.cs"
PRESET = EDITOR_DIR / "TransformationBankMaterialPreset.cs"
UNITY_TEST = (
    REPO_ROOT
    / ".ci"
    / "UnityProject"
    / "Assets"
    / "Editor"
    / "TransformationBankClipGeneratorTests.cs"
)
DOCUMENTATION = REPO_ROOT / "docs" / "transformation-bank.md"


def test_package_contains_editor_only_clip_generator():
    assert {GENERATOR, WINDOW, REPORT, PRESET} <= set(EDITOR_DIR.iterdir())
    window = WINDOW.read_text(encoding="utf-8")

    assert 'MenuItem("Tools/SabaShader/Transformation Bank Clip Generator")' in window
    assert "Avatar Root" in window
    assert "衣装 A" in window
    assert "衣装 B" in window
    assert "Particle SystemとAnimator Controllerへの組み込みは生成しません" in window


def test_generator_is_non_destructive_and_creates_both_direction_clips():
    source = GENERATOR.read_text(encoding="utf-8")

    assert "CreateTransitionClip(" in source
    assert 'options.OutfitA.name + "_To_" + options.OutfitB.name' in source
    assert 'options.OutfitB.name + "_To_" + options.OutfitA.name' in source
    assert "m_Materials.Array.data[" in source
    assert '"m_IsActive"' in source
    assert '"material." + ProgressProperty' in source
    assert "AssetDatabase.GenerateUniqueAssetPath" in source
    assert "renderer.sharedMaterials =" not in source
    assert "renderer.sharedMaterial =" not in source
    assert "UnityEditor.Animations" not in source
    assert "AnimatorController" not in source


def test_generator_requires_transformation_bank_materials_and_keeps_overlap():
    source = GENERATOR.read_text(encoding="utf-8")

    for property_name in ("Progress", "Role", "Style", "EffectIntensity"):
        assert f'BankPrefix + "{property_name}"' in source
    assert "material.HasProperty(property)" in source
    assert "duration - 1.0f / FrameRate" in source
    assert "new Keyframe(duration, 0.0f)" in source
    assert "new Keyframe(duration, 1.0f)" in source
    assert "TransformationBankGenerationReport" in source


def test_unity_editmode_tests_cover_generation_and_invalid_materials():
    source = UNITY_TEST.read_text(encoding="utf-8")

    assert "GenerateCreatesTwoClipsRoleMaterialsAndReportWithoutChangingScene" in source
    assert "GeneratedClipsKeepBothOutfitsActiveUntilOutgoingIsFullyClipped" in source
    assert "GenerateCreatesMaterialReferenceCurveForEveryMaterialSlot" in source
    assert "GeneratedClipSamplesRoleMaterialsAndRestoresSceneAfterPreview" in source
    assert "ValidateRejectsMaterialWithoutTransformationBankProperties" in source
    assert "ValidateRejectsDuplicateRendererBindingPaths" in source
    assert "t:AnimatorController" in source


def test_clip_generator_is_documented_as_a_separate_integration_step():
    source = DOCUMENTATION.read_text(encoding="utf-8")

    assert "Transformation Bank Clip Generator" in source
    assert "Animator Controller" in source
    assert "Particle System" in source
    assert "元Material" in source
