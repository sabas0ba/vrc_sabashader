"""`.scmodule` の読み込みとフェーズへの差し込みの検証。

Shader Core のモジュールは、シェーダー本体が置いた `__SC_PHASE_*__` の位置に
コードを差し込む仕組み。ハーネスがこれを再現できていないと、
モジュールを足したときに「マーカーの取りこぼし」「未宣言プロパティの参照」を
Unity に入れるまで検出できなくなる。

C# 側の対応箇所:
    Editor/Core/SCModule.cs        SCModule.FromFile / SCPhase.LoadHLSL / CompareTo
    Editor/Core/SCProperty.cs      FromFile の uniqueID 前置き
    Editor/Importer/SCShaderImporter.cs  モジュールの収集と差し込み
"""

from __future__ import annotations

import json
import re
from pathlib import Path

import pytest

from harness.paths import MODULES_DIR
from harness.scshader import (
    Module,
    ModuleLoadError,
    ShaderExpander,
    discover_modules,
    load_module,
    order_phases,
    parse_properties,
)


def write_module(
    directory: Path,
    unique_id: str,
    *,
    name: str = "Test Module",
    phases: dict | None = None,
    properties: str | None = None,
    includes: str | None = None,
    json_phases: list | None = None,
    keep_property_names: bool = False,
) -> Path:
    """テスト用のモジュールを 1 つ作る。phases は {フェーズ名: HLSL}。"""
    directory.mkdir(parents=True, exist_ok=True)

    body: dict = {"name": name, "uniqueID": unique_id}
    if keep_property_names:
        body["keepPropertyNames"] = True
    if json_phases is not None:
        body["phases"] = json_phases

    path = directory / f"{unique_id}.scmodule"
    path.write_text(json.dumps(body, ensure_ascii=False, indent=2), encoding="utf-8")

    for phase, code in (phases or {}).items():
        (directory / f"phase_{phase}.hlsl").write_text(code, encoding="utf-8")

    if properties is not None:
        (directory / "properties.hlsl").write_text(properties, encoding="utf-8")
    if includes is not None:
        (directory / "includes.hlsl").write_text(includes, encoding="utf-8")

    return path


SHADER_TEMPLATE = """Shader "Test/Sample"
{
    Properties
    {
        __SC_SHADERLAB_properties__
        [SCHide]_ShaderLabDummy("", Float) = 0
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            __SC_BIRP_properties__
            __SC_INCLUDES__
            void frag()
            {
                __SC_PHASE_base__
                __SC_PHASE_add__
            }
            ENDHLSL
        }
    }
}
"""


@pytest.fixture
def shader(tmp_path: Path) -> Path:
    path = tmp_path / "Sample.scshader"
    path.write_text(SHADER_TEMPLATE, encoding="utf-8")
    (tmp_path / "Sample_properties.hlsl").write_text(
        'SC_color(_BaseColor, (1,1,1,1), [], "__Color", "")\n', encoding="utf-8"
    )
    return path


# --- 読み込み -----------------------------------------------------------------


def test_phase_file_is_discovered_without_json(tmp_path: Path):
    """phase_<名前>.hlsl は JSON に書かなくても拾われる。"""
    path = write_module(tmp_path / "mod", "com.example.a", phases={"base": "// base\n"})
    module = load_module(path)
    assert [p.phase for p in module.phases] == ["base"]


def test_missing_unique_id_is_rejected(tmp_path: Path):
    directory = tmp_path / "mod"
    directory.mkdir()
    (directory / "broken.scmodule").write_text('{"name": "x"}', encoding="utf-8")
    with pytest.raises(ModuleLoadError, match="uniqueID"):
        load_module(directory / "broken.scmodule")


def test_missing_phase_file_is_rejected(tmp_path: Path):
    directory = tmp_path / "mod"
    directory.mkdir()
    (directory / "m.scmodule").write_text(
        json.dumps({"name": "m", "uniqueID": "com.example.m", "phases": [{"phase": "base"}]}),
        encoding="utf-8",
    )
    with pytest.raises(ModuleLoadError, match="フェーズのファイル"):
        load_module(directory / "m.scmodule")


def test_discover_finds_every_module(tmp_path: Path):
    write_module(tmp_path / "a", "com.example.a", phases={"base": "// a\n"})
    write_module(tmp_path / "b", "com.example.b", phases={"add": "// b\n"})
    assert {m.unique_id for m in discover_modules(tmp_path)} == {"com.example.a", "com.example.b"}


# --- プロパティ名の書き換え ---------------------------------------------------


def test_properties_are_prefixed_with_unique_id(tmp_path: Path):
    path = write_module(
        tmp_path / "mod",
        "com.example.rain",
        phases={"base": "// noop\n"},
        properties='SC_float(_Amount, 0.5, [], "__Amount", "")\n',
    )
    module = load_module(path)
    assert module.properties[0].name == "_com_example_rain_Amount"
    assert module.properties[0].original_name == "_Amount"


def test_keep_property_names_skips_the_prefix(tmp_path: Path):
    path = write_module(
        tmp_path / "mod",
        "com.example.rain",
        phases={"base": "// noop\n"},
        properties='SC_float(_Amount, 0.5, [], "__Amount", "")\n',
        keep_property_names=True,
    )
    assert load_module(path).properties[0].name == "_Amount"


def test_module_hlsl_references_are_rewritten(tmp_path: Path, shader: Path):
    """モジュールは素の名前で書き、展開時に前置きされた名前へ直る。"""
    path = write_module(
        tmp_path / "mod",
        "com.example.rain",
        phases={"base": "sd.col.rgb *= _Amount;\n"},
        properties='SC_float(_Amount, 0.5, [], "__Amount", "")\n',
    )
    result = ShaderExpander(shader, {}, [load_module(path)]).expand()
    assert "_com_example_rain_Amount" in result.source
    assert "*= _Amount;" not in result.source


def test_scale_offset_reference_is_rewritten(tmp_path: Path, shader: Path):
    path = write_module(
        tmp_path / "mod",
        "com.example.dirt",
        phases={"base": "float2 uv = sd.uv * _Mask_ST.xy;\n"},
        properties=('SC_Texture2D(_Mask, "white", [], "__Mask", "")\nSC_ScaleOffset(_Mask)\n'),
    )
    result = ShaderExpander(shader, {}, [load_module(path)]).expand()
    assert "_com_example_dirt_Mask_ST.xy" in result.source


# --- 差し込み -----------------------------------------------------------------


def test_phase_code_is_inserted_at_the_marker(tmp_path: Path, shader: Path):
    path = write_module(
        tmp_path / "mod",
        "com.example.a",
        phases={"base": "// BASE MARK\n", "add": "// ADD MARK\n"},
    )
    result = ShaderExpander(shader, {}, [load_module(path)]).expand()

    assert "// BASE MARK" in result.source
    assert "// ADD MARK" in result.source
    assert "__SC_PHASE_" not in result.source
    assert result.phases == ["base", "add"]


def test_module_properties_reach_both_blocks(tmp_path: Path, shader: Path):
    path = write_module(
        tmp_path / "mod",
        "com.example.a",
        phases={"base": "// noop\n"},
        properties='SC_float(_Amount, 0.5, [], "__Amount", "")\n',
    )
    result = ShaderExpander(shader, {}, [load_module(path)]).expand()

    # ShaderLab 側と HLSL 側の両方に出ること
    assert '_com_example_a_Amount ("__Amount", Float) = 0.5' in result.source
    assert "float _com_example_a_Amount;" in result.source


def test_includes_marker_takes_module_includes(tmp_path: Path, shader: Path):
    path = write_module(
        tmp_path / "mod",
        "com.example.a",
        phases={"base": "// noop\n"},
        includes='#include "Packages/com.example/util.hlsl"\n',
    )
    result = ShaderExpander(shader, {}, [load_module(path)]).expand()
    assert '#include "Packages/com.example/util.hlsl"' in result.source


def test_declared_names_include_modules(tmp_path: Path, shader: Path):
    path = write_module(
        tmp_path / "mod",
        "com.example.a",
        phases={"base": "// noop\n"},
        properties='SC_float(_Amount, 0.5, [], "__Amount", "")\n',
    )
    names = ShaderExpander(shader, {}, [load_module(path)]).declared_property_names()
    assert "_BaseColor" in names
    assert "_com_example_a_Amount" in names


def test_no_modules_leaves_phases_empty(shader: Path):
    """モジュールを渡さなければ、マーカーは消えるだけ。"""
    result = ShaderExpander(shader, {}).expand()
    assert "__SC_PHASE_" not in result.source
    assert result.phases == ["base", "add"]


# --- 並び順 -------------------------------------------------------------------


def _module(tmp_path: Path, unique_id: str, name: str, phase: str, **kwargs) -> Module:
    return load_module(
        write_module(
            tmp_path / unique_id,
            unique_id,
            name=name,
            phases={phase: f"// {name}\n"},
            **kwargs,
        )
    )


def test_same_phase_is_ordered_by_name(tmp_path: Path):
    modules = [
        _module(tmp_path, "com.example.z", "Zeta", "base"),
        _module(tmp_path, "com.example.a", "Alpha", "base"),
    ]
    assert [p.name for _, p in order_phases(modules, "base")] == ["Alpha", "Zeta"]


def test_afters_moves_module_later(tmp_path: Path):
    early = _module(tmp_path, "com.example.a", "Alpha", "base")
    late = load_module(
        write_module(
            tmp_path / "late",
            "com.example.b",
            name="Beta",
            json_phases=[{"phase": "base", "name": "Beta", "afters": ["Alpha"]}],
            phases={"base": "// Beta\n"},
        )
    )
    # 名前順なら Alpha, Beta。afters を付けても順序は保たれる
    assert [p.name for _, p in order_phases([late, early], "base")] == ["Alpha", "Beta"]


def test_befores_moves_module_earlier(tmp_path: Path):
    late = _module(tmp_path, "com.example.z", "Zeta", "base")
    early = load_module(
        write_module(
            tmp_path / "early",
            "com.example.a",
            name="Alpha",
            json_phases=[{"phase": "base", "name": "Alpha", "befores": ["Zeta"]}],
            phases={"base": "// Alpha\n"},
        )
    )
    assert [p.name for _, p in order_phases([late, early], "base")] == ["Alpha", "Zeta"]


def test_contradictory_order_is_rejected(tmp_path: Path):
    a = load_module(
        write_module(
            tmp_path / "a",
            "com.example.a",
            name="Alpha",
            json_phases=[{"phase": "base", "name": "Alpha", "afters": ["Beta"], "befores": ["Beta"]}],
            phases={"base": "// Alpha\n"},
        )
    )
    b = _module(tmp_path, "com.example.b", "Beta", "base")
    with pytest.raises(ModuleLoadError, match="矛盾"):
        order_phases([a, b], "base")


def test_module_phase_files_are_reported_as_included(tmp_path: Path, shader: Path):
    """依存として数えられていないと、変更してもテストが再評価されない。"""
    path = write_module(tmp_path / "mod", "com.example.a", phases={"base": "// noop\n"})
    result = ShaderExpander(shader, {}, [load_module(path)]).expand()
    assert any(p.name == "phase_base.hlsl" for p in result.included_files)


# --- パッケージが持つモジュール ------------------------------------------------

# Shader Core やパイプラインが用意していて、モジュール側で宣言しないもの
_EXTERNAL_NAMES = {
    "_Time",
    "_SinTime",
    "_CosTime",
    "_WorldSpaceLightPos0",
    "_LightColor0",
    "_ScreenParams",
}


def _package_modules():
    from harness.scshader import package_modules

    return package_modules()


def test_package_modules_load():
    modules = _package_modules()
    assert modules, "Modules 配下にモジュールが見つかりません"
    for module in modules:
        assert module.phases, f"{module.unique_id}: フェーズがありません"


def test_crt_vertex_tearing_respects_module_amount():
    module = next(
        module
        for module in _package_modules()
        if module.unique_id == "io.github.sabas0ba.crtglitch"
    )
    morph = next(phase for phase in module.phases if phase.phase == "morph")
    source = morph.path.read_text(encoding="utf-8")

    assert re.search(
        r"crtMorphStyle\.tearing\s*=\s*_Tearing\s*\*\s*_Amount\s*;",
        source,
    ), "CRT の頂点裂けはモジュール全体の Amount に従う必要があります"


def test_video_input_runs_before_pixel_art_and_crt():
    modules = _package_modules()
    phases = [phase.name for _, phase in order_phases(modules, "postpixel")]

    assert phases.index("__VideoInput") < phases.index("__PixelArt")
    assert phases.index("__VideoInput") < phases.index("__CrtGlitch")


def test_video_input_forward_add_only_attenuates_existing_light():
    module = next(
        module
        for module in _package_modules()
        if module.unique_id == "io.github.sabas0ba.videoinput"
    )
    postpixel = next(phase for phase in module.phases if phase.phase == "postpixel")
    source = postpixel.path.read_text(encoding="utf-8")

    guard = re.search(
        r"#ifdef\s+UNITY_PASS_FORWARDADD(?P<body>.*?)#endif",
        source,
        re.DOTALL,
    )
    assert guard, "Video Input の postpixel に ForwardAdd 用の分岐がありません"
    assert re.search(r"videoStyle\.additivePass\s*=\s*1\.0\s*;", guard.group("body"))

    core = (MODULES_DIR / "VideoInput" / "VideoInputCore.hlsl").read_text(encoding="utf-8")
    apply = core[core.index("half3 SBSVideoInputApply") : core.index("#endif")]
    assert re.search(
        r"if \(st\.additivePass > 0\.5\)\s+return base \* \(1\.0 - opacity\);",
        apply,
    )
    assert apply.index("return base * (1.0 - opacity)") < apply.index("video.rgb")


def test_crt_forward_add_does_not_add_light_independent_noise():
    module = next(
        module
        for module in _package_modules()
        if module.unique_id == "io.github.sabas0ba.crtglitch"
    )
    postpixel = next(phase for phase in module.phases if phase.phase == "postpixel")
    source = postpixel.path.read_text(encoding="utf-8")

    guard = re.search(
        r"#ifdef\s+UNITY_PASS_FORWARDADD(?P<body>.*?)#endif",
        source,
        re.DOTALL,
    )
    assert guard, "CRT の postpixel に ForwardAdd 用の分岐がありません"
    assert re.search(r"crtStyle\.additivePass\s*=\s*1\.0\s*;", guard.group("body"))
    assert re.search(r"crtStyle\.noise\s*=\s*0\.0\s*;", guard.group("body"))
    assert not re.search(r"crtStyle\.staticAmount\s*=\s*0\.0\s*;", guard.group("body"))

    apply_position = source.index("SBSCrtApply")
    assert guard.end() < apply_position
    assert "crtStyle.noise =" not in source[guard.end() : apply_position]
    assert "crtStyle.staticAmount =" not in source[guard.end() : apply_position]


def test_crt_forward_add_keeps_static_attenuation_without_adding_static():
    core = (
        MODULES_DIR / "CrtGlitch" / "CrtGlitchCore.hlsl"
    ).read_text(encoding="utf-8")
    static = core[core.index("half3 SBSCrtStatic") : core.index("half2 SBSCrtBand")]

    attenuation = static.index("torn * (1.0 - amount)")
    base_only = static.index("if (st.additivePass < 0.5)")
    static_level = static.index("SBSCrtHash(cell")
    assert attenuation < base_only < static_level


def test_crt_forward_add_does_not_quantize_each_light():
    core = (
        MODULES_DIR / "CrtGlitch" / "CrtGlitchCore.hlsl"
    ).read_text(encoding="utf-8")
    block = core[core.index("half3 SBSCrtBlock") : core.index("half SBSCrtTear")]

    base_only = block.index("st.additivePass < 0.5")
    quantize = block.index("floor(saturate(shifted)")
    assert base_only < quantize


def test_crt_apply_skips_disabled_stages_and_refreshes_gradients():
    core = (
        MODULES_DIR / "CrtGlitch" / "CrtGlitchCore.hlsl"
    ).read_text(encoding="utf-8")
    apply = core[core.index("half3 SBSCrtApply") : core.index("#endif")]

    assert re.search(r"if \(amount <= 0\.0\)\s+return color;", apply)
    for control, call in (
        ("block", "SBSCrtBlock"),
        ("glitch", "SBSCrtBand"),
        ("aberration", "SBSCrtAberration"),
        ("staticAmount", "SBSCrtStatic"),
        ("scanline", "SBSCrtScanline"),
        ("mask", "SBSCrtMask"),
        ("roll", "SBSCrtRoll"),
        ("noise", "SBSCrtGrain"),
        ("vignette", "SBSCrtVignette"),
    ):
        assert re.search(rf"if \(st\.{control} > 0\.0\).*?{call}\(", apply, re.DOTALL)

    channel_swap = apply.index("SBSCrtChannelSwap")
    aberration = apply.index("SBSCrtAberration")
    refreshed_gradient = apply.index("ddx(result)", channel_swap)
    assert channel_swap < aberration < refreshed_gradient
    assert re.search(
        r"SBSCrtAberration\(\s*result,\s*ddx\(result\),\s*ddy\(result\)",
        apply,
    )


def test_crt_curvature_uses_matching_projection_matrices():
    postvertex = (
        MODULES_DIR / "CrtGlitch" / "crt_postvertex.hlsl"
    ).read_text(encoding="utf-8")

    assert "unity_CameraProjection" in postvertex
    assert "unity_CameraInvProjection" in postvertex
    assert "UNITY_MATRIX_I_V" in postvertex
    assert "UNITY_MATRIX_P._m00" not in postvertex
    assert "UNITY_MATRIX_P._m11" not in postvertex
    assert re.search(r"if \(crtClipPosition\.w > 1\.0e-3\)", postvertex)
    assert "abs(crtClipPosition.w)" not in postvertex


@pytest.mark.parametrize("module", _package_modules(), ids=lambda m: m.unique_id)
def test_package_module_only_uses_declared_properties(module):
    """モジュールの HLSL は自分が宣言したプロパティだけを参照すること。

    モジュール側は素の名前で書くので、宣言側も素の名前で突き合わせる。
    """
    from harness.scshader import strip_comments, used_property_names

    declared = {p.original_name for p in module.properties if p.original_name}
    declared |= {f"{p.original_name}_ST" for p in module.properties if p.type == "ScaleOffset"}

    sources = [
        (phase.path.name, phase.path.read_text(encoding="utf-8")) for phase in module.phases
    ]
    if module.includes:
        sources.append(("includes.hlsl", module.includes))

    used = used_property_names(sources)
    undeclared = {
        name: sorted(files)
        for name, files in used.items()
        if name not in declared and name not in _EXTERNAL_NAMES
    }

    assert not undeclared, (
        f"{module.unique_id}: 宣言していないプロパティを参照しています "
        f"(properties.hlsl に追加してください): {undeclared}"
    )


@pytest.mark.parametrize("module", _package_modules(), ids=lambda m: m.unique_id)
def test_package_module_does_not_declare_sampler(module):
    """自前のサンプラー宣言は uniqueID が前置きされて Unity の規約から外れる。"""
    samplers = [p.original_name for p in module.properties if p.type == "SamplerState"]
    assert not samplers, (
        f"{module.unique_id}: サンプラーを宣言しています {samplers}。"
        " SCSampleRepeat / SCSampleClamp か sampler_linear_repeat を使ってください。"
    )


@pytest.mark.parametrize("module", _package_modules(), ids=lambda m: m.unique_id)
def test_package_module_localization_is_complete(module):
    """表示名と説明のキーが po に揃っていること。"""
    lang_dir = module.directory / "lang"
    if not lang_dir.is_dir():
        pytest.skip(f"{module.unique_id}: lang がありません")

    # モジュール名は Shader Core が自動で付ける外側の折りたたみに使われる
    # （SCShaderImporter.ShaderLab.cs の ReplaceProperties）。properties.hlsl に
    # 現れなくても翻訳が要る。
    keys = {module.name}
    for prop in module.properties:
        for text in (prop.display, prop.description):
            if not text:
                continue
            key = text.strip('"')
            if key:
                keys.add(key)
        if prop.type == "Foldout" and prop.name:
            keys.add(prop.name)

    for po in sorted(lang_dir.glob("*.po")):
        entries = dict(
            re.findall(r'^msgid\s+"(.+)"\s*\nmsgstr\s+"(.*)"', po.read_text(encoding="utf-8"), re.MULTILINE)
        )
        missing = sorted(key for key in keys if key not in entries)
        assert not missing, f"{module.unique_id}: {po.name} に翻訳がありません: {missing}"

        unused = sorted(key for key in entries if key not in keys)
        assert not unused, f"{module.unique_id}: {po.name} に使われていないキーがあります: {unused}"


@pytest.mark.parametrize("module", _package_modules(), ids=lambda m: m.unique_id)
def test_package_module_phase_files_are_declared_once(module):
    """同じフェーズを JSON と phase_*.hlsl の両方に置かないこと。

    Shader Core は JSON に書いたフェーズを読んだあと、ディレクトリの
    phase_*.hlsl を無条件に足す（SCModule.FromFile の AddRange。重複を
    除くための exists は組み立てられるだけで使われていない）。両方に
    該当すると同じコードが 2 回差し込まれ、効果が二重にかかる。

    ハーネス側は重複を除くので、この食い違いはハーネスでは見えない。
    並び順を afters で指定したいときは、ファイル名を phase_*.hlsl から
    外して JSON にだけ載せること。
    """
    discovered = {
        re.match(r"phase_(\w+)\.hlsl", path.name).group(1): path.name
        for path in module.directory.glob("phase_*.hlsl")
    }

    from_json = json.loads(module.path.read_text(encoding="utf-8")).get("phases", [])
    declared_in_json = {entry.get("phase") for entry in from_json}

    both = sorted(declared_in_json & set(discovered))
    assert not both, (
        f"{module.unique_id}: フェーズ {both} を JSON でも宣言し、"
        f" {[discovered[phase] for phase in both]} も置いています。"
        " Unity 側では両方が差し込まれて効果が二重にかかるので、どちらかにしてください。"
    )
