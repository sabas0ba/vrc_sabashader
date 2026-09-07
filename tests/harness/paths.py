"""リポジトリ内の主要なパス。"""

from __future__ import annotations

from pathlib import Path

HARNESS_DIR = Path(__file__).resolve().parent
TESTS_DIR = HARNESS_DIR.parent
REPO_ROOT = TESTS_DIR.parent

PACKAGE_DIR = REPO_ROOT / "Packages" / "io.github.sabas0ba.sabashader"
SHADERS_DIR = PACKAGE_DIR / "Shaders"
SHADER_DIR = SHADERS_DIR / "Illust2D"
MODULES_DIR = PACKAGE_DIR / "Modules"

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

# NonToon との組み合わせは常用経路として Unity と構造検査の両方で確認する。
# tag だけでは上流で参照先が変わり得るため、release 0.1.3 の commit を固定する。
NONTOON_CACHE = REPO_ROOT / ".cache" / "nontoon-0.1.3"
NONTOON_URL = "https://github.com/lilxyzw/NonToon.git"
NONTOON_COMMIT = "130bea3e6be5183b4fceb60df0062d38ef98067c"  # tag 0.1.3
NONTOON_PACKAGE_PATH = "Packages/jp.lilxyzw.nontoon"
