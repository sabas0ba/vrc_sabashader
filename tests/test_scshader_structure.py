"""Unity を起動せずに .scshader の構造を検証する。

Shader Core のインポータと同じ手順で展開し、マーカーの取りこぼしや
未宣言プロパティの参照など「Unity に入れて初めて気付く」種類の壊れ方を防ぐ。
"""

from __future__ import annotations

import re
from typing import Dict, List, Tuple

import pytest

from cases import DEFAULT_OUTLINE, DEFAULT_STYLE, PROPERTY_TO_OUTLINE, PROPERTY_TO_STYLE
from harness.paths import LANG_DIR, PROPERTIES_HLSL, SCSHADER, SHADER_DIR
from harness.scshader import (
    ShaderExpander,
    ensure_shadercore,
    package_modules,
    package_roots,
    parse_properties,
    strip_comments,
    used_property_names,
)

# シェーダー本体が実装しなければならないフェーズ
REQUIRED_PHASES = {
    "morph",
    "postvertex",
    "base",
    "light",
    "customlight",
    "modifylight",
    "shade",
    "reflection",
    "add",
    "postpixel",
}

# Unity / Shader Core 側で宣言されるもの
EXTERNAL_NAMES = {
    "_LightColor0",
    "_WorldSpaceLightPos0",
    "_WorldSpaceCameraPos",
    "_ScreenParams",
    "_ProjectionParams",
    "_Time",
    "_LightShadowData",
    "_CameraDepthTexture",
    "_GrabTexture",
    "_ShaderLabDummy",
}

# Shader Core の lang/*.po が持っている組み込みキー
SHADERCORE_L10N_KEYS = {
    "__Main",
    "__Texture",
    "__Color",
    "__Mask",
    "__SharedMask",
    "__SharedGradients",
    "__NormalMap",
    "__NormalMapWithRoughness",
    "__Roughness",
    "__Cutoff",
}

OUR_SOURCES = [
    "Illust2D.scshader",
    "Illust2D_properties.hlsl",
    "Illust2DCore.hlsl",
    "Illust2DLighting.hlsl",
    "Illust2DFragment.hlsl",
    "Illust2DOutlineFragment.hlsl",
    "sc_common.hlsl",
]


def _our_sources() -> List[Tuple[str, str]]:
    return [(name, (SHADER_DIR / name).read_text(encoding="utf-8")) for name in OUR_SOURCES]


def _parse_po(text: str) -> Dict[str, str]:
    entries: Dict[str, str] = {}
    key = None
    for line in text.splitlines():
        msgid = re.match(r'^msgid\s+"(.*)"\s*$', line)
        msgstr = re.match(r'^msgstr\s+"(.*)"\s*$', line)
        if msgid:
            key = msgid.group(1)
        elif msgstr and key is not None:
            entries[key] = msgstr.group(1)
            key = None
    return entries


def _number(text: str) -> float:
    return float(text)


def _vector(text: str) -> Tuple[float, ...]:
    return tuple(float(part) for part in text.strip("()").split(","))


# --- プロパティ ---------------------------------------------------------------


def test_properties_parse():
    props = parse_properties(PROPERTIES_HLSL)
    assert props, "プロパティが 1 つも読み取れていません"


def test_property_names_are_unique():
    names = [name for prop in parse_properties(PROPERTIES_HLSL) for name in prop.declared_names()]
    duplicates = sorted({n for n in names if names.count(n) > 1})
    assert not duplicates, f"プロパティ名が重複しています: {duplicates}"


def test_foldouts_are_balanced():
    depth_box = 0
    depth_foldout = 0
    for prop in parse_properties(PROPERTIES_HLSL):
        depth_box += {"Box": 1, "BoxEnd": -1}.get(prop.type, 0)
        depth_foldout += {"Foldout": 1, "FoldoutEnd": -1}.get(prop.type, 0)
        assert depth_box >= 0 and depth_foldout >= 0, "SC_BoxEnd / SC_FoldoutEnd が多すぎます"
    assert depth_box == 0, "SC_Box が閉じられていません"
    assert depth_foldout == 0, "SC_Foldout が閉じられていません"


def test_all_used_properties_are_declared():
    expander = ShaderExpander(SCSHADER, {}, package_modules())
    declared = set(expander.declared_property_names()) | EXTERNAL_NAMES

    used = used_property_names(_our_sources())
    undeclared = {name: files for name, files in used.items() if name not in declared}

    assert not undeclared, (
        "宣言されていないプロパティを参照しています "
        f"(Illust2D_properties.hlsl に追加してください): { {k: sorted(v) for k, v in sorted(undeclared.items())} }"
    )


def test_declared_properties_are_used():
    """使われていないプロパティはマテリアルUIのノイズになるので落とす。"""
    expander = ShaderExpander(SCSHADER, {})
    bodies = "\n".join(
        strip_comments(text) for name, text in _our_sources() if name != "Illust2D_properties.hlsl"
    )

    unused = sorted(
        name
        for name in expander.declared_property_names()
        if name != "_ShaderLabDummy" and re.search(rf"(?<![\w]){re.escape(name)}(?![\w])", bodies) is None
    )
    assert not unused, f"どこからも参照されていないプロパティがあります: {unused}"


# --- ローカライズ -------------------------------------------------------------


@pytest.mark.parametrize("language", ["ja-JP", "en-US"])
def test_localization_covers_every_key(language):
    entries = _parse_po((LANG_DIR / f"{language}.po").read_text(encoding="utf-8"))

    missing = []
    for prop in parse_properties(PROPERTIES_HLSL):
        for text in (prop.display, prop.description):
            if not text:
                continue
            key = text.strip('"')
            if key.startswith("__") and key not in entries and key not in SHADERCORE_L10N_KEYS:
                missing.append(key)
        if prop.type == "Foldout" and prop.name.startswith("__") and prop.name not in entries:
            missing.append(prop.name)

    assert not missing, f"{language}.po に翻訳が無いキー: {sorted(set(missing))}"


@pytest.mark.parametrize("language", ["ja-JP", "en-US"])
def test_localization_has_no_stale_keys(language):
    entries = _parse_po((LANG_DIR / f"{language}.po").read_text(encoding="utf-8"))

    used = {""}
    for prop in parse_properties(PROPERTIES_HLSL):
        for text in (prop.display, prop.description):
            if text:
                used.add(text.strip('"'))
        if prop.type == "Foldout":
            used.add(prop.name)

    stale = sorted(set(entries) - used)
    assert not stale, f"{language}.po に使われていないキーが残っています: {stale}"


# --- テストケースとマテリアル初期値の同期 -------------------------------------


def test_default_style_matches_material_defaults():
    defaults = {p.name: p.default for p in parse_properties(PROPERTIES_HLSL) if p.default is not None}

    mismatches = []
    for prop_name, style_name in PROPERTY_TO_STYLE.items():
        assert prop_name in defaults, f"{prop_name} が properties.hlsl にありません"
        expected = DEFAULT_STYLE[style_name]
        raw = defaults[prop_name]
        actual = _vector(raw)[: len(expected)] if raw.startswith("(") else _number(raw)
        if isinstance(expected, (list, tuple)):
            if tuple(float(v) for v in expected) != tuple(actual):
                mismatches.append((prop_name, raw, expected))
        elif float(expected) != actual:
            mismatches.append((prop_name, raw, expected))

    assert not mismatches, (
        "tests/cases.py の DEFAULT_STYLE がマテリアル初期値とずれています: " f"{mismatches}"
    )


def test_default_outline_matches_material_defaults():
    defaults = {p.name: p.default for p in parse_properties(PROPERTIES_HLSL) if p.default is not None}

    mismatches = []
    for prop_name, outline_name in PROPERTY_TO_OUTLINE.items():
        expected = DEFAULT_OUTLINE[outline_name]
        raw = defaults[prop_name]
        actual = _vector(raw)[: len(expected)] if raw.startswith("(") else _number(raw)
        if isinstance(expected, (list, tuple)):
            if tuple(float(v) for v in expected) != tuple(actual):
                mismatches.append((prop_name, raw, expected))
        elif float(expected) != actual:
            mismatches.append((prop_name, raw, expected))

    assert not mismatches, (
        "tests/cases.py の DEFAULT_OUTLINE がマテリアル初期値とずれています: " f"{mismatches}"
    )


# --- 展開結果 -----------------------------------------------------------------


@pytest.fixture(scope="module")
def expanded():
    shadercore = ensure_shadercore()
    if shadercore is None:
        pytest.skip("Shader Core を取得できませんでした (ネットワーク不通)")
    return ShaderExpander(SCSHADER, package_roots(shadercore), package_modules()).expand()


def test_no_markers_left(expanded):
    leftovers = sorted(set(re.findall(r"__SC_[A-Za-z0-9_]+__", expanded.source)))
    assert not leftovers, f"展開されなかったマーカーが残っています: {leftovers}"


def test_all_required_phases_present(expanded):
    missing = sorted(REQUIRED_PHASES - set(expanded.phases))
    assert not missing, f"実装されていないフェーズがあります: {missing}"


def test_only_unity_includes_remain(expanded):
    # warnings.hlsl はインポータが意図的に展開せず Unity 側に残す。
    allowed = {
        "UnityCG.cginc",
        "AutoLight.cginc",
        "Packages/jp.lilxyzw.shadercore/ShaderLibrary/warnings.hlsl",
    }
    unexpected = sorted(set(expanded.unresolved_includes) - allowed)
    assert not unexpected, f"解決できない include があります: {unexpected}"


def test_braces_are_balanced(expanded):
    text = strip_comments(expanded.source)
    assert text.count("{") == text.count("}"), "波括弧の対応が取れていません"
    assert text.count("(") == text.count(")"), "丸括弧の対応が取れていません"


def test_required_hooks_are_defined(expanded):
    source = expanded.source
    for symbol in (
        "struct SCCustomData",
        "void SCVertexMorph",
        "void SCVertexPost",
        "void SCPixelClip",
        "void SCCalculateLight",
        "void SCCalculateEnvironmentLight",
        "half4 frag",
    ):
        assert symbol in source, f"{symbol} が展開結果にありません"


def test_every_pass_has_a_fragment(expanded):
    passes = expanded.source.count("HLSLPROGRAM")
    frags = len(re.findall(r"\bhalf4 frag\s*\(", expanded.source))
    assert passes == 4, f"パス数が想定と違います: {passes}"
    assert frags == passes, f"frag の数がパス数と一致しません: {frags} != {passes}"


def test_shading_data_fully_initialised(expanded):
    """SCShadingData は 0 初期化できない（テクスチャを含む）ので全フィールドの代入が要る。"""
    struct = re.search(r"struct\s+SCShadingData\s*\{(.*?)\};", expanded.source, re.DOTALL)
    assert struct, "展開結果に SCShadingData がありません"

    fields = re.findall(r"^\s*[\w:<>]+\s+(\w+)\s*;", strip_comments(struct.group(1)), re.MULTILINE)
    assert fields, "SCShadingData のフィールドを読み取れませんでした"

    fragment = strip_comments((SHADER_DIR / "Illust2DFragment.hlsl").read_text(encoding="utf-8"))
    prologue = fragment.split("__SC_PHASE_base__", 1)[0]

    missing = [f for f in fields if not re.search(rf"\bsd\.{f}\s*=", prologue)]
    assert not missing, (
        "base フェーズより前に初期化されていない SCShadingData のフィールドがあります: " f"{missing}"
    )


def test_shaderlab_properties_expanded(expanded):
    assert "[SCFoldout(__Main)]" in expanded.source
    assert "_ShadeBorder1 (" in expanded.source
    assert "_BaseTexture_ST" in expanded.source
