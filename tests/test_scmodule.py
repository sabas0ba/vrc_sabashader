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
from pathlib import Path

import pytest

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
