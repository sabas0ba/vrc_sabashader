"""Shader Core の SCShaderImporter を最低限エミュレートする。

Unity なしで .scshader を最終的な ShaderLab まで展開し、
「マーカーの取りこぼし」「include の解決漏れ」「未宣言のプロパティ参照」といった
Unity に入れるまで気付けない種類の壊れ方を CI で捕まえるためのもの。

C# 側 (Editor/Importer/SCShaderImporter*.cs, Editor/Core/SCProperty.cs,
Editor/Core/SCModule.cs) の挙動に合わせてある。モジュール(.scmodule)の
読み込みとフェーズへの差し込み、プロパティ名の uniqueID による書き換えも再現する。
"""

from __future__ import annotations

import json
import re
import subprocess
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from .paths import (
    MODULES_DIR,
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
    # uniqueID を付ける前の名前。モジュールの HLSL 内の参照を
    # 書き換えるときに使う（SCPhase.LoadHLSL と同じ）。
    original_name: Optional[str] = None

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


def parse_properties(path: Path, unique_id: str = "") -> List[Property]:
    """SCProperty.FromFile と同じ。unique_id を渡すとプロパティ名に前置きする。"""
    prefix = "_" + unique_id.replace(".", "_") if unique_id else ""
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
                    name=prefix + match.group(2),
                    default=match.group(3),
                    attributes=[a for a in attributes if a != "[]"],
                    display=match.group(5),
                    description=match.group(6),
                    original_name=match.group(2),
                )
            )
            continue

        for pattern, ptype in ((_REG_SAMPLER, "SamplerState"), (_REG_SCALE_OFFSET, "ScaleOffset")):
            simple = pattern.match(line)
            if simple:
                props.append(
                    Property(type=ptype, name=prefix + simple.group(1), original_name=simple.group(1))
                )
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


# --- .scmodule の読み込み -----------------------------------------------------


class ModuleLoadError(ValueError):
    pass


@dataclass
class ModulePhase:
    """SCPhase 相当。どのフェーズにどのファイルを差し込むか。"""

    phase: str
    path: Path
    name: str
    befores: List[str] = field(default_factory=list)
    afters: List[str] = field(default_factory=list)


@dataclass
class Module:
    """SCModule 相当。"""

    unique_id: str
    name: str
    path: Path
    phases: List[ModulePhase] = field(default_factory=list)
    properties: List[Property] = field(default_factory=list)
    includes: Optional[str] = None
    keep_property_names: bool = False

    @property
    def directory(self) -> Path:
        return self.path.parent

    def declared_property_names(self) -> List[str]:
        names: List[str] = []
        for prop in self.properties:
            names.extend(prop.declared_names())
        return names

    def rename_properties(self, line: str) -> str:
        """モジュールの HLSL 内のプロパティ参照を uniqueID 付きの名前へ書き換える。

        SCPhase.LoadHLSL と同じ。モジュール側は素の名前で書き、
        インポータが衝突しない名前に直す、という約束になっている。
        """
        for prop in self.properties:
            if not prop.original_name or prop.original_name == prop.name:
                continue
            suffix = "_ST" if prop.type == "ScaleOffset" else ""
            line = re.sub(
                r"(?<![\w])" + re.escape(prop.original_name) + suffix + r"(?![\w])",
                prop.name + suffix,
                line,
            )
        return line


def load_module(path: Path) -> Module:
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise ModuleLoadError(f"{path.name}: JSON として読めません: {error}") from error

    unique_id = raw.get("uniqueID")
    if not unique_id:
        raise ModuleLoadError(f"{path.name}: uniqueID がありません")

    name = raw.get("name") or path.stem
    keep = bool(raw.get("keepPropertyNames", False))
    directory = path.parent

    properties: List[Property] = []
    properties_path = directory / "properties.hlsl"
    if properties_path.is_file():
        properties = parse_properties(properties_path, "" if keep else unique_id)

    includes: Optional[str] = None
    includes_path = directory / "includes.hlsl"
    if includes_path.is_file():
        includes = includes_path.read_text(encoding="utf-8")

    phases: List[ModulePhase] = []
    declared: set = set()
    for entry in raw.get("phases", []):
        phase = entry.get("phase")
        if not phase:
            raise ModuleLoadError(f"{path.name}: phase の指定がありません")
        relative = entry.get("path") or f"phase_{phase}.hlsl"
        phases.append(
            ModulePhase(
                phase=phase,
                path=directory / relative,
                name=entry.get("name") or name,
                befores=list(entry.get("befores", [])),
                afters=list(entry.get("afters", [])),
            )
        )
        declared.add(relative)

    # phase_<フェーズ名>.hlsl は JSON に書かなくても拾われる
    for candidate in sorted(directory.glob("phase_*.hlsl")):
        if candidate.name in declared:
            continue
        phases.append(
            ModulePhase(
                phase=re.match(r"phase_(\w+)\.hlsl", candidate.name).group(1),
                path=candidate,
                name=name,
            )
        )

    missing = [str(phase.path) for phase in phases if not phase.path.is_file()]
    if missing:
        raise ModuleLoadError(f"{path.name}: フェーズのファイルがありません: {', '.join(missing)}")

    return Module(
        unique_id=unique_id,
        name=name,
        path=path,
        phases=phases,
        properties=properties,
        includes=includes,
        keep_property_names=keep,
    )


def discover_modules(root: Path) -> List[Module]:
    return [load_module(path) for path in sorted(root.rglob("*.scmodule"))]


def package_modules() -> List[Module]:
    """このパッケージが持つモジュール一式。"""
    return discover_modules(MODULES_DIR) if MODULES_DIR.is_dir() else []


def _phase_sort_key(pair: Tuple[Module, ModulePhase]):
    return (pair[1].phase, pair[1].name)


def order_phases(modules: List[Module], phase: str) -> List[Tuple[Module, ModulePhase]]:
    """同一フェーズ内の並び。befores / afters を尊重し、無ければ名前順。

    SCPhase.CompareTo と同じ判断をするが、比較関数ではなく
    依存関係を解いてから安定ソートする（同じ結果になり、循環を検出できる）。
    """
    pairs = [(m, p) for m in modules for p in m.phases if p.phase == phase]
    pairs.sort(key=_phase_sort_key)

    order = {id(pair): index for index, pair in enumerate(pairs)}
    changed = True
    guard = 0
    while changed:
        changed = False
        guard += 1
        if guard > len(pairs) + 2:
            raise ModuleLoadError(f"フェーズ {phase} の前後関係が循環しています")
        for a in pairs:
            for b in pairs:
                if a is b:
                    continue
                a_after = b[1].name in a[1].afters or a[1].name in b[1].befores
                b_after = a[1].name in b[1].afters or b[1].name in a[1].befores
                if a_after and b_after:
                    raise ModuleLoadError(
                        f"前後関係の指定が矛盾しています: {a[1].name} と {b[1].name}"
                    )
                if a_after and order[id(a)] < order[id(b)]:
                    order[id(a)], order[id(b)] = order[id(b)], order[id(a)]
                    changed = True

    return sorted(pairs, key=lambda pair: order[id(pair)])


def order_modules(modules: List[Module]) -> List[Module]:
    """プロパティを並べる順。フェーズを持たないものが先、あとはフェーズ名順。"""
    return sorted(modules, key=lambda m: (bool(m.phases), m.phases[0].phase if m.phases else ""))


# --- .scshader の展開 ---------------------------------------------------------


@dataclass
class ExpandResult:
    source: str
    phases: List[str]
    unresolved_includes: List[str]
    included_files: List[Path]


class ShaderExpander:
    def __init__(
        self,
        shader_path: Path,
        package_roots: Dict[str, Path],
        modules: Optional[List[Module]] = None,
    ) -> None:
        self.shader_path = shader_path
        self.shader_dir = shader_path.parent
        self.package_roots = package_roots
        self.properties = self._load_properties()
        # Shader Core はシェーダーごとに有効なモジュールを持つ。既定は
        # 「そのシェーダーと同じディレクトリにあるもの」で、他所のものは
        # プロジェクト設定で有効化する。ここでは呼び出し側から明示させる。
        self.modules: List[Module] = list(modules or [])

    def _load_properties(self) -> List[Property]:
        props_path = self.shader_path.with_name(f"{self.shader_path.stem}_properties.hlsl")
        return parse_properties(props_path) if props_path.exists() else []

    def declared_property_names(self) -> List[str]:
        names: List[str] = []
        for prop in self.properties:
            names.extend(prop.declared_names())
        for module in self.modules:
            names.extend(module.declared_property_names())
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
                    name = phase.group(1)
                    if name not in phases:
                        phases.append(name)
                    marker_indent = re.match(r"^\s*", line).group(0)
                    out.extend(self._phase_block(name, marker_indent, included))
                    continue

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
                expanded.extend(self._includes_block(indent))
            else:
                expanded.append(line)

        return ExpandResult("\n".join(expanded), phases, unresolved, included)

    def _shaderlab_block(self, indent: str) -> List[str]:
        lines = self._shaderlab_group(indent, "__Main", self.properties)
        for module in order_modules(self.modules):
            lines.extend(self._shaderlab_group(indent, module.name, module.properties))
        return lines

    @staticmethod
    def _shaderlab_group(indent: str, label: str, properties: List[Property]) -> List[str]:
        lines = [f"{indent}[SCModule()][SCFoldout({label})]"]
        scale_offsets = {p.name for p in properties if p.type == "ScaleOffset"}
        for prop in properties:
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
        for module in order_modules(self.modules):
            for prop in module.properties:
                declaration = prop.hlsl_declaration()
                if declaration is not None:
                    lines.append(f"{indent}{declaration}")
        return lines

    def _phase_block(self, phase: str, indent: str, included: List[Path]) -> List[str]:
        """フェーズのマーカーを、そこに差し込まれるモジュールのコードに置き換える。"""
        lines: List[str] = []
        for module, module_phase in order_phases(self.modules, phase):
            included.append(module_phase.path)
            lines.append(f"{indent}// {module.unique_id} / {module_phase.name}")
            for line in module_phase.path.read_text(encoding="utf-8").splitlines():
                lines.append(indent + module.rename_properties(line))
        return lines

    def _includes_block(self, indent: str) -> List[str]:
        lines: List[str] = []
        for module in order_modules(self.modules):
            if not module.includes:
                continue
            for line in module.includes.splitlines():
                lines.append(indent + line)
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
