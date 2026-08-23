"""Shader Core の SCShaderImporter を最低限エミュレートする。

Unity なしで .scshader を最終的な ShaderLab まで展開し、
「マーカーの取りこぼし」「include の解決漏れ」「未宣言のプロパティ参照」といった
Unity に入れるまで気付けない種類の壊れ方を CI で捕まえるためのもの。

C# 側 (Editor/Importer/SCShaderImporter*.cs, Editor/Core/SCProperty.cs) の
挙動に合わせてあるが、モジュール(.scmodule)の読み込みまでは再現しない。
"""

from __future__ import annotations

import re
import subprocess
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from .paths import (
    SHADERCORE_CACHE,
    SHADERCORE_COMMIT,
    SHADERCORE_PACKAGE_PATH,
    SHADERCORE_URL,
)

# --- SCProperty.cs のパターンを移植したもの -----------------------------------

_REG_VARIABLE = r"\w+"
_REG_NUM = r"[\d\.\-]+"
_REG_VECTOR = r"\([\d\.\-,\s]*\)"
_REG_STRING = r'"[^"]*"'

_REG_PROPERTY = re.compile(
    r"^\s*SC_(" + _REG_VARIABLE + r")"
    r"\s*\(\s*(" + _REG_VARIABLE + r")"
    r"\s*,\s*(" + _REG_NUM + "|" + _REG_VECTOR + "|" + _REG_STRING + r")"
    r"\s*,\s*((?:\[[^\[\]]*\]\s*)*)"
    r"\s*,\s*(" + _REG_STRING + r")"
    r"\s*,\s*(" + _REG_STRING + r")\s*\)\s*$"
)
_REG_SAMPLER = re.compile(r"^\s*SC_SamplerState\(\s*(" + _REG_VARIABLE + r")\s*\)\s*$")
_REG_SCALE_OFFSET = re.compile(r"^\s*SC_ScaleOffset\(\s*(" + _REG_VARIABLE + r")\s*\)\s*$")
_REG_BOX = re.compile(r"^\s*SC_Box\s*$")
_REG_BOX_END = re.compile(r"^\s*SC_BoxEnd\s*$")
_REG_FOLDOUT = re.compile(r"^\s*SC_Foldout\(([^()]*)\)\s*$")
_REG_FOLDOUT_END = re.compile(r"^\s*SC_FoldoutEnd\s*$")

_REG_INCLUDE = re.compile(r'^\s*#include\s*"([^"]*)"\s*(?://|$)')
_REG_PHASE = re.compile(r"^\s*__SC_PHASE_([a-zA-Z0-9_]+)__\s*$")

_TEXTURE_TYPES = {"Texture2D", "Texture2DArray", "Texture3D", "TextureCube", "TextureCubeArray"}
_VALUE_TYPES = {"float", "float4", "uint", "uint4", "int", "int4", "color"}

_SHADERLAB_TYPE = {
    "Texture2D": "2D",
    "Texture2DArray": "2DArray",
    "Texture3D": "3D",
    "TextureCube": "Cube",
    "TextureCubeArray": "CubeArray",
    "float": "Float",
    "float4": "Vector",
    "uint": "Integer",
    "uint4": "Vector",
    "int": "Integer",
    "int4": "Vector",
    "color": "Color",
}


class PropertyParseError(ValueError):
    pass


@dataclass
class Property:
    type: str
    name: str
    default: Optional[str] = None
    attributes: List[str] = field(default_factory=list)
    display: Optional[str] = None
    description: Optional[str] = None

    @property
    def is_layout(self) -> bool:
        return self.type in {"Box", "BoxEnd", "Foldout", "FoldoutEnd"}

    def hlsl_declaration(self) -> Optional[str]:
        if self.type in _TEXTURE_TYPES:
            return f"{self.type} {self.name};"
        if self.type in {"float", "float4", "uint", "uint4", "int", "int4"}:
            return f"{self.type} {self.name};"
        if self.type == "color":
            return f"float4 {self.name};"
        if self.type == "SamplerState":
            return f"SamplerState {self.name};"
        if self.type == "ScaleOffset":
            return f"float4 {self.name}_ST;"
        return None

    def shaderlab_declaration(self) -> Optional[str]:
        attrs = "".join(a.replace("-", "_") for a in self.attributes)
        if attrs:
            attrs += " "
        if self.type in _TEXTURE_TYPES:
            return f"{attrs}{self.name} ({self.display}, {_SHADERLAB_TYPE[self.type]}) = {self.default} {{}}"
        if self.type in _VALUE_TYPES:
            return f"{attrs}{self.name} ({self.display}, {_SHADERLAB_TYPE[self.type]}) = {self.default}"
        if self.type == "Box":
            return "[SCBox]"
        if self.type == "BoxEnd":
            return "[SCBoxEnd]"
        if self.type == "Foldout":
            return f"[SCFoldout({self.name})]"
        if self.type == "FoldoutEnd":
            return "[SCFoldoutEnd]"
        return None

    def declared_names(self) -> List[str]:
        if self.type == "ScaleOffset":
            return [f"{self.name}_ST"]
        if self.is_layout:
            return []
        return [self.name]


def parse_properties(path: Path) -> List[Property]:
    props: List[Property] = []
    for lineno, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        # Shader Core の SCProperty.Parse は空行以外の解釈できない行で例外を投げる。
        # コメント行も例外になるので、ここでも読み飛ばさない。
        if not line.strip():
            continue

        match = _REG_PROPERTY.match(line)
        if match:
            attributes = re.findall(r"\[[^\[\]]*\]", match.group(4))
            props.append(
                Property(
                    type=match.group(1),
                    name=match.group(2),
                    default=match.group(3),
                    attributes=[a for a in attributes if a != "[]"],
                    display=match.group(5),
                    description=match.group(6),
                )
            )
            continue

        for pattern, ptype in ((_REG_SAMPLER, "SamplerState"), (_REG_SCALE_OFFSET, "ScaleOffset")):
            simple = pattern.match(line)
            if simple:
                props.append(Property(type=ptype, name=simple.group(1)))
                break
        else:
            if _REG_BOX.match(line):
                props.append(Property(type="Box", name=""))
            elif _REG_BOX_END.match(line):
                props.append(Property(type="BoxEnd", name=""))
            elif _REG_FOLDOUT_END.match(line):
                props.append(Property(type="FoldoutEnd", name=""))
            else:
                foldout = _REG_FOLDOUT.match(line)
                if foldout:
                    props.append(Property(type="Foldout", name=foldout.group(1)))
                else:
                    raise PropertyParseError(f"{path.name}:{lineno}: 解釈できない行です: {line!r}")
    return props


# --- .scshader の展開 ---------------------------------------------------------


@dataclass
class ExpandResult:
    source: str
    phases: List[str]
    unresolved_includes: List[str]
    included_files: List[Path]


class ShaderExpander:
    def __init__(self, shader_path: Path, package_roots: Dict[str, Path]) -> None:
        self.shader_path = shader_path
        self.shader_dir = shader_path.parent
        self.package_roots = package_roots
        self.properties = self._load_properties()

    def _load_properties(self) -> List[Property]:
        props_path = self.shader_path.with_name(f"{self.shader_path.stem}_properties.hlsl")
        return parse_properties(props_path) if props_path.exists() else []

    def declared_property_names(self) -> List[str]:
        names: List[str] = []
        for prop in self.properties:
            names.extend(prop.declared_names())
        return names

    def _resolve_include(self, raw: str, current_dir: Path) -> Optional[Path]:
        for candidate in (current_dir / raw, self.shader_dir / raw):
            if candidate.is_file():
                return candidate
        if raw.startswith("Packages/com.unity."):
            return None
        if raw.endswith("jp.lilxyzw.shadercore/ShaderLibrary/warnings.hlsl"):
            return None
        for prefix, root in self.package_roots.items():
            if raw.startswith(prefix + "/"):
                candidate = root / raw[len(prefix) + 1 :]
                if candidate.is_file():
                    return candidate
        return None

    def expand(self) -> ExpandResult:
        phases: List[str] = []
        unresolved: List[str] = []
        included: List[Path] = []

        def read(path: Path) -> List[str]:
            included.append(path)
            out: List[str] = []
            for line in path.read_text(encoding="utf-8").splitlines():
                phase = _REG_PHASE.match(line)
                if phase:
                    if phase.group(1) not in phases:
                        phases.append(phase.group(1))
                    continue  # モジュール未導入なので空に展開される

                include = _REG_INCLUDE.match(line)
                if include:
                    resolved = self._resolve_include(include.group(1), path.parent)
                    if resolved is not None:
                        out.extend(read(resolved))
                        continue
                    unresolved.append(include.group(1))

                out.append(line)
            return out

        lines = read(self.shader_path)

        expanded: List[str] = []
        for line in lines:
            indent = re.match(r"^\s*", line).group(0)
            if "__SC_SHADERLAB_properties__" in line:
                expanded.extend(self._shaderlab_block(indent))
            elif "__SC_BIRP_properties__" in line or "__SC_URP_properties__" in line:
                expanded.extend(self._hlsl_block(indent))
            elif "__SC_INCLUDES__" in line:
                continue
            else:
                expanded.append(line)

        return ExpandResult("\n".join(expanded), phases, unresolved, included)

    def _shaderlab_block(self, indent: str) -> List[str]:
        lines = [f"{indent}[SCModule()][SCFoldout(__Main)]"]
        scale_offsets = {p.name for p in self.properties if p.type == "ScaleOffset"}
        for prop in self.properties:
            declaration = prop.shaderlab_declaration()
            if declaration is None:
                continue
            prefix = "[NoScaleOffset]" if prop.type in _TEXTURE_TYPES and prop.name not in scale_offsets else ""
            lines.append(f"{indent}    {prefix}{declaration}")
        lines.append(f"{indent}[SCFoldoutEnd]")
        return lines

    def _hlsl_block(self, indent: str) -> List[str]:
        lines = []
        for prop in self.properties:
            declaration = prop.hlsl_declaration()
            if declaration is not None:
                lines.append(f"{indent}{declaration}")
        return lines


# --- Shader Core の取得 -------------------------------------------------------


def ensure_shadercore() -> Optional[Path]:
    """テスト用に Shader Core を shallow clone する。取得できなければ None。"""
    marker = SHADERCORE_CACHE / "ShaderLibrary" / "birp.hlsl"
    if marker.is_file():
        return SHADERCORE_CACHE

    SHADERCORE_CACHE.parent.mkdir(parents=True, exist_ok=True)
    commands = [
        ["git", "init", "--quiet", str(SHADERCORE_CACHE)],
        ["git", "-C", str(SHADERCORE_CACHE), "remote", "add", "origin", SHADERCORE_URL],
        ["git", "-C", str(SHADERCORE_CACHE), "fetch", "--quiet", "--depth", "1", "origin", SHADERCORE_COMMIT],
        ["git", "-C", str(SHADERCORE_CACHE), "checkout", "--quiet", "FETCH_HEAD"],
    ]
    try:
        for command in commands:
            subprocess.run(command, check=True, capture_output=True, timeout=180)
    except (subprocess.SubprocessError, OSError):
        return None

    return SHADERCORE_CACHE if marker.is_file() else None


def package_roots(shadercore: Path) -> Dict[str, Path]:
    return {SHADERCORE_PACKAGE_PATH: shadercore}


# --- 補助 ---------------------------------------------------------------------


def strip_comments(source: str) -> str:
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.DOTALL)
    return re.sub(r"//[^\n]*", "", source)


def used_property_names(sources: List[Tuple[str, str]]) -> Dict[str, List[str]]:
    """(ファイル名, 中身) から `_Foo` 形式の識別子を集める。"""
    found: Dict[str, List[str]] = {}
    for name, text in sources:
        for token in set(re.findall(r"(?<![\w])_[A-Za-z][A-Za-z0-9_]*", strip_comments(text))):
            found.setdefault(token, []).append(name)
    return found
