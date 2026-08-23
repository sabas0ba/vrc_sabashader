"""リポジトリ内の主要なパス。"""

from __future__ import annotations

from pathlib import Path

HARNESS_DIR = Path(__file__).resolve().parent
TESTS_DIR = HARNESS_DIR.parent
REPO_ROOT = TESTS_DIR.parent

PACKAGE_DIR = REPO_ROOT / "Packages" / "io.github.sabas0ba.sabashader"
SHADER_DIR = PACKAGE_DIR / "Shaders" / "Illust2D"

CORE_HLSL = SHADER_DIR / "Illust2DCore.hlsl"
SCSHADER = SHADER_DIR / "Illust2D.scshader"
PROPERTIES_HLSL = SHADER_DIR / "Illust2D_properties.hlsl"
LANG_DIR = SHADER_DIR / "lang"

PRELUDE_GLSL = HARNESS_DIR / "prelude.glsl"
SCENE_FRAG = HARNESS_DIR / "scene.frag"

GOLDEN_DIR = TESTS_DIR / "golden"
ARTIFACT_DIR = REPO_ROOT / "_test_artifacts"

# 構造チェックで参照する Shader Core。CI ではここに shallow clone される。
SHADERCORE_CACHE = REPO_ROOT / ".cache" / "Shader-Core"
SHADERCORE_URL = "https://github.com/lilxyzw/Shader-Core.git"
SHADERCORE_COMMIT = "0a0b2fef78fc3b0438b58f443a2e75210db83ec4"  # tag 0.1.9
SHADERCORE_PACKAGE_PATH = "Packages/jp.lilxyzw.shadercore"
