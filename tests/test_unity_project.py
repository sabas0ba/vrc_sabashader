"""Unity 検証プロジェクトの雛形と、C# 側の期待値の整合を確認する。

Unity 本体は動かせないので、ここでは「C# に書いた期待値がシェーダーの実態と
食い違っていないか」を Python 側から突き合わせる。C# の期待値が古いまま
Unity ジョブが通ってしまう、という抜けを防ぐのが目的。
"""

from __future__ import annotations

import json
import re
from pathlib import Path

import pytest

from harness.paths import PROPERTIES_HLSL, REPO_ROOT, SCSHADER
from harness.scshader import ShaderExpander, package_modules, parse_properties

UNITY_PROJECT = REPO_ROOT / ".ci" / "UnityProject"
MANIFEST = UNITY_PROJECT / "Packages" / "manifest.json"
PROJECT_VERSION = UNITY_PROJECT / "ProjectSettings" / "ProjectVersion.txt"
CHECKER_CS = UNITY_PROJECT / "Assets" / "Editor" / "ShaderCompileChecker.cs"
TESTS_CS = UNITY_PROJECT / "Assets" / "Editor" / "ShaderCompileTests.cs"
ASMDEF = UNITY_PROJECT / "Assets" / "Editor" / "SabaShader.CI.Editor.asmdef"
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "unity-compile.yml"


def _csharp_string_array(source: str, field: str) -> list[str]:
    """`static readonly string[] Foo = { "a", "b" };` の中身を取り出す。"""
    match = re.search(rf"string\[\]\s+{re.escape(field)}\s*=\s*\{{(.*?)\}}\s*;", source, re.DOTALL)
    assert match, f"{field} が C# に見つかりません"
    return re.findall(r'"([^"]*)"', match.group(1))


@pytest.fixture(scope="module")
def checker_source() -> str:
    return CHECKER_CS.read_text(encoding="utf-8")


# --- 雛形 ---------------------------------------------------------------------


def test_project_skeleton_exists():
    for path in (MANIFEST, PROJECT_VERSION, CHECKER_CS, TESTS_CS, ASMDEF):
        assert path.is_file(), f"雛形が足りません: {path.relative_to(REPO_ROOT)}"


def test_manifest_is_valid_json_with_required_dependencies():
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    dependencies = manifest["dependencies"]

    # Shader Core の Editor アセンブリが要求する
    assert "com.unity.nuget.newtonsoft-json" in dependencies
    # EditMode テストを走らせるのに要る
    assert "com.unity.test-framework" in dependencies


def test_asmdef_is_valid_json_and_references_test_runner():
    asmdef = json.loads(ASMDEF.read_text(encoding="utf-8"))
    assert asmdef["includePlatforms"] == ["Editor"]
    assert "UnityEditor.TestRunner" in asmdef["references"]
    assert "nunit.framework.dll" in asmdef["precompiledReferences"]


def test_project_version_matches_package_unity_version():
    package = json.loads((REPO_ROOT / "Packages" / "io.github.sabas0ba.sabashader" / "package.json").read_text("utf-8"))
    version = PROJECT_VERSION.read_text(encoding="utf-8")

    match = re.search(r"m_EditorVersion:\s*(\d+\.\d+)", version)
    assert match, "ProjectVersion.txt から Unity のバージョンを読めません"
    assert match.group(1) == package["unity"], (
        f"検証プロジェクトの Unity {match.group(1)} が package.json の {package['unity']} と違います"
    )


def test_shadercore_commit_is_pinned_consistently():
    """テストハーネスと Unity プロジェクト組み立てで同じコミットを使う。"""
    from harness.paths import SHADERCORE_COMMIT

    setup = (REPO_ROOT / "tools" / "setup_unity_project.py").read_text(encoding="utf-8")
    assert SHADERCORE_COMMIT in setup, (
        "tools/setup_unity_project.py の Shader Core のコミットが "
        "tests/harness/paths.py と一致していません"
    )


def test_demo_setup_enables_package_modules():
    """新規 Demo Project でもレビューシーンが必要なプロパティを持つようにする。"""
    setup = (REPO_ROOT / "tools" / "setup_demo_project.py").read_text(encoding="utf-8")

    assert re.search(r"from tools\.setup_unity_project import .*\benable_modules\b", setup)
    main = setup[setup.index("def main()") :]
    assert main.index("clone_shadercore(") < main.index("enable_modules(project)")


# --- C# の期待値とシェーダーの実態 --------------------------------------------


def test_expected_passes_match_the_shader(checker_source):
    expected = _csharp_string_array(checker_source, "ExpectedPasses")
    actual = re.findall(r'Name\s+"([^"]+)"', SCSHADER.read_text(encoding="utf-8"))

    assert sorted(expected) == sorted(actual), (
        "ShaderCompileChecker.ExpectedPasses と Illust2D.scshader のパス名が食い違っています: "
        f"C#={sorted(expected)} shader={sorted(actual)}"
    )


def test_required_properties_are_actually_declared(checker_source):
    required = _csharp_string_array(checker_source, "RequiredProperties")
    declared = set(ShaderExpander(SCSHADER, {}, package_modules()).declared_property_names())

    missing = [name for name in required if name not in declared]
    assert not missing, (
        "ShaderCompileChecker.RequiredProperties にあるが properties.hlsl に無いプロパティ: " f"{missing}"
    )


def test_required_properties_cover_each_property_kind(checker_source):
    """テクスチャ / ScaleOffset / 色 / float / uint を最低 1 つずつ見ていること。"""
    required = set(_csharp_string_array(checker_source, "RequiredProperties"))

    # ScaleOffset は宣言名が `_Foo_ST` になるので declared_names() で引く
    type_by_declared_name = {
        declared: prop.type
        for prop in parse_properties(PROPERTIES_HLSL)
        for declared in prop.declared_names()
    }

    kinds = {type_by_declared_name[name] for name in required if name in type_by_declared_name}

    for kind in ("Texture2D", "ScaleOffset", "color", "float", "uint"):
        assert kind in kinds, f"RequiredProperties が {kind} 型のプロパティを 1 つも見ていません"


def test_checker_targets_the_real_package_path(checker_source):
    assert 'PackagePath = "Packages/io.github.sabas0ba.sabashader"' in checker_source
    relative = SCSHADER.relative_to(REPO_ROOT / "Packages" / "io.github.sabas0ba.sabashader").as_posix()
    assert relative in checker_source, f"Illust2DPath が {relative} を指していません"


# --- ワークフロー -------------------------------------------------------------


def test_unity_workflow_skips_without_license():
    workflow = WORKFLOW.read_text(encoding="utf-8")
    assert "needs.gate.outputs.licensed == 'true'" in workflow, (
        "ライセンス secret が無いときにスキップするゲートがありません"
    )
    assert "tools/setup_unity_project.py" in workflow
    assert "game-ci/unity-test-runner" in workflow


@pytest.mark.skipif(
    __import__("importlib").util.find_spec("yaml") is None, reason="pyyaml が入っていません"
)
def test_all_workflows_are_valid_yaml():
    import yaml

    for path in sorted((REPO_ROOT / ".github" / "workflows").glob("*.yml")):
        with path.open(encoding="utf-8") as handle:
            document = yaml.safe_load(handle)
        assert isinstance(document, dict), f"{path.name} が辞書として読めません"
        assert "jobs" in document, f"{path.name} に jobs がありません"
