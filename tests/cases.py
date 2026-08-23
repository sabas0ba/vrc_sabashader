"""回帰テストのケース定義。

`DEFAULT_STYLE` はマテリアルの初期値
(Packages/io.github.sabas0ba.sabashader/Shaders/Illust2D/Illust2D_properties.hlsl)
と一致させる。ズレは test_scshader_structure.py が検出する。
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Dict, List, Sequence, Tuple

# モード
MODE_SPHERE = 0
MODE_RAMP = 1
MODE_OUTLINE = 2
MODE_LIGHT_LIMIT = 3
# 球では出てこない平らな面・鋭い稜線・自己遮蔽を見るための立体
MODE_BOX = 4
MODE_TORUS = 5
MODE_CAPSULE = 6
# モジュール（表面の重ね掛け）
MODE_OVERLAY = 7
MODE_PIXEL = 8

DEFAULT_STYLE: Dict[str, object] = {
    "shadeBorder1": 0.5,
    "shadeBlur1": 0.06,
    "shade1Color": (1.0, 1.0, 1.0),
    "shade1HueShift": 0.02,
    "shade1Saturation": 1.15,
    "shade1Value": 0.82,
    "shadeBorder2": 0.22,
    "shadeBlur2": 0.06,
    "shade2Color": (1.0, 1.0, 1.0),
    "shade2HueShift": 0.05,
    "shade2Saturation": 1.3,
    "shade2Value": 0.62,
    "shadeSteps": 0.0,
    "shadowStrength": 1.0,
    "specularColor": (1.0, 1.0, 1.0),
    "specularBorder": 0.5,
    "specularBlur": 0.02,
    "specularSmoothness": 0.85,
    "rimColor": (0.0, 0.0, 0.0),
    "rimBorder": 0.72,
    "rimBlur": 0.12,
    "rimLightAlign": 1.0,
    "lightMinLimit": 0.08,
    "lightMaxLimit": 1.0,
    "monochromeLighting": 0.0,
    "asUnlit": 0.0,
    "saturation": 1.05,
    "contrast": 1.02,
}

# モジュールの数式ファイルにある SBSXxxStyle と一致させる。
# ズレは test_core_render.py が検出する。
MODULE_STYLE_DEFAULTS: Dict[str, Dict[str, object]] = {}

# SurfaceOverlayCore.hlsl の SBSOverlayStyle
DEFAULT_OVERLAY: Dict[str, object] = {
    "amount": 1.0,
    "upBias": 0.8,
    "border": 0.6,
    "blur": 0.15,
    "darken": 0.0,
    "flatten": 0.0,
    "streak": 0.0,
    "streakScale": 24.0,
    "streakSpeed": 0.6,
    "time": 0.0,
}

MODULE_STYLE_DEFAULTS["SBSOverlayStyle"] = DEFAULT_OVERLAY

# PixelArtCore.hlsl の SBSPixelStyle
DEFAULT_PIXEL: Dict[str, object] = {
    "amount": 1.0,
    "levels": 6.0,
    "dither": 1.0,
    "cellSize": 4.0,
    "palette": 0.0,
}

MODULE_STYLE_DEFAULTS["SBSPixelStyle"] = DEFAULT_PIXEL

DEFAULT_OUTLINE: Dict[str, object] = {
    "color": (0.15, 0.10, 0.13),
    "hueShift": 0.02,
    "saturation": 1.2,
    "value": 0.45,
}

# マテリアルのプロパティ名 -> SBSStyle のフィールド名
PROPERTY_TO_STYLE: Dict[str, str] = {
    "_ShadeBorder1": "shadeBorder1",
    "_ShadeBlur1": "shadeBlur1",
    "_Shade1Color": "shade1Color",
    "_Shade1HueShift": "shade1HueShift",
    "_Shade1Saturation": "shade1Saturation",
    "_Shade1Value": "shade1Value",
    "_ShadeBorder2": "shadeBorder2",
    "_ShadeBlur2": "shadeBlur2",
    "_Shade2Color": "shade2Color",
    "_Shade2HueShift": "shade2HueShift",
    "_Shade2Saturation": "shade2Saturation",
    "_Shade2Value": "shade2Value",
    "_ShadeSteps": "shadeSteps",
    "_ShadowStrength": "shadowStrength",
    "_SpecularColor": "specularColor",
    "_SpecularBorder": "specularBorder",
    "_SpecularBlur": "specularBlur",
    "_SpecularSmoothness": "specularSmoothness",
    "_RimColor": "rimColor",
    "_RimBorder": "rimBorder",
    "_RimBlur": "rimBlur",
    "_RimLightAlign": "rimLightAlign",
    "_LightMinLimit": "lightMinLimit",
    "_LightMaxLimit": "lightMaxLimit",
    "_MonochromeLighting": "monochromeLighting",
    "_AsUnlit": "asUnlit",
    "_Saturation": "saturation",
    "_Contrast": "contrast",
}

PROPERTY_TO_OUTLINE: Dict[str, str] = {
    "_OutlineColor": "color",
    "_OutlineHueShift": "hueShift",
    "_OutlineSaturation": "saturation",
    "_OutlineValue": "value",
}


@dataclass(frozen=True)
class Case:
    name: str
    mode: int
    description: str
    style: Dict[str, object] = field(default_factory=dict)
    outline: Dict[str, object] = field(default_factory=dict)
    # モジュールのスタイル。{構造体名: 上書きしたい値}
    module_styles: Dict[str, Dict[str, object]] = field(default_factory=dict)
    light_dir: Sequence[float] = (0.55, 0.62, -0.56)
    light_color: Sequence[float] = (1.0, 0.97, 0.92)
    ambient: Sequence[float] = (0.16, 0.18, 0.24)
    resolution: Tuple[int, int] = (256, 256)

    def resolved_style(self) -> Dict[str, object]:
        merged = dict(DEFAULT_STYLE)
        merged.update(self.style)
        return merged

    def resolved_outline(self) -> Dict[str, object]:
        merged = dict(DEFAULT_OUTLINE)
        merged.update(self.outline)
        return merged

    def resolved_module_styles(self) -> Dict[str, Dict[str, object]]:
        merged: Dict[str, Dict[str, object]] = {}
        for struct, defaults in MODULE_STYLE_DEFAULTS.items():
            values = dict(defaults)
            values.update(self.module_styles.get(struct, {}))
            merged[struct] = values
        return merged

    @property
    def golden_name(self) -> str:
        return f"{self.name}.png"


CASES: List[Case] = [
    Case(
        name="sphere_default",
        mode=MODE_SPHERE,
        description="マテリアル初期値のまま。全体の見た目が変わっていないかの基準。",
    ),
    Case(
        name="sphere_hard_cel",
        mode=MODE_SPHERE,
        description="ぼかしを 0 にした完全な 2 値塗り。境界の位置がずれると即座に落ちる。",
        style={"shadeBlur1": 0.0, "shadeBlur2": 0.0, "shadeBorder1": 0.58, "shadeBorder2": 0.34},
    ),
    Case(
        name="sphere_posterized",
        mode=MODE_SPHERE,
        description="広いぼかし + 4 段ポスタライズ。SBSPosterize の段の位置を検証する。",
        style={"shadeBlur1": 0.55, "shadeBlur2": 0.55, "shadeSteps": 4.0},
    ),
    Case(
        name="sphere_rim_specular",
        mode=MODE_SPHERE,
        description="リムライトとハイライトを強めに出した状態。",
        style={
            "rimColor": (0.85, 0.72, 1.0),
            "rimBorder": 0.6,
            "rimBlur": 0.25,
            "specularColor": (1.4, 1.35, 1.2),
            "specularBorder": 0.35,
        },
    ),
    Case(
        name="sphere_flat_lighting",
        mode=MODE_SPHERE,
        description="無彩色ライト + Unlit 寄り。ライトのクランプ経路を通す。",
        style={"monochromeLighting": 1.0, "asUnlit": 0.4, "lightMinLimit": 0.35},
        light_color=(1.6, 0.7, 0.4),
        ambient=(0.05, 0.05, 0.05),
    ),
    Case(
        name="sphere_no_shadow",
        mode=MODE_SPHERE,
        description="落ち影を無視する設定。shadowStrength の分岐を検証する。",
        style={"shadowStrength": 0.0},
    ),
    Case(
        name="box_default",
        mode=MODE_BOX,
        description="平らな面と鋭い稜線。面ごとに塗りが段になるので境界の位置が分かる。",
    ),
    Case(
        name="box_band_per_face",
        mode=MODE_BOX,
        # 見える 3 面の half-lambert 値は 0.224 / 0.642 / 0.892。
        # 境界をその間に置くと 1 面ずつ別の帯に入るので、境界の位置がずれると
        # 面の色が入れ替わって一目で分かる。平面は面内で値が一定なので、
        # ぼかしを変えても絵が動かない（それでは回帰テストにならない）。
        description="見える 3 面がそれぞれ別の帯に入る設定。境界位置のずれが面の色の入れ替わりとして出る。",
        style={"shadeBlur1": 0.0, "shadeBlur2": 0.0, "shadeBorder1": 0.75, "shadeBorder2": 0.45},
    ),
    Case(
        name="torus_default",
        mode=MODE_TORUS,
        description="自己遮蔽のある形。内側に落ちる影が shadowStrength の経路を通る。",
    ),
    Case(
        name="capsule_rim_specular",
        mode=MODE_CAPSULE,
        description="押し出した曲面と半球の継ぎ目。リムとハイライトの伸び方を見る。",
        style={
            "rimColor": (0.85, 0.72, 1.0),
            "rimBorder": 0.6,
            "rimBlur": 0.25,
            "specularColor": (1.4, 1.35, 1.2),
            "specularBorder": 0.35,
        },
    ),
    Case(
        name="swatch_ramp",
        mode=MODE_RAMP,
        description="横軸=ライトの当たり具合 / 縦軸=ベースカラー のランプ表。",
        resolution=(320, 160),
    ),
    Case(
        name="swatch_ramp_saturated",
        mode=MODE_RAMP,
        description="影の色相シフトと彩度を強めた設定でのランプ表。",
        style={
            "shade1HueShift": -0.08,
            "shade1Saturation": 1.8,
            "shade1Value": 0.7,
            "shade2HueShift": -0.14,
            "shade2Saturation": 2.2,
            "shade2Value": 0.45,
        },
        resolution=(320, 160),
    ),
    Case(
        name="swatch_outline",
        mode=MODE_OUTLINE,
        description="横軸=ベースカラーの反映量 のアウトライン色表。",
        resolution=(320, 160),
    ),
    Case(
        name="swatch_light_limit",
        mode=MODE_LIGHT_LIMIT,
        description="横軸=入射光の明るさ のクランプ表。下限・上限の折れ位置を検証する。",
        style={"lightMinLimit": 0.25, "lightMaxLimit": 1.2},
        resolution=(320, 160),
    ),
    Case(
        name="overlay_snow",
        mode=MODE_OVERLAY,
        description="上向き面にだけ積もる設定。upBias と境界の効きを見る。",
        module_styles={"SBSOverlayStyle": {"upBias": 1.0, "border": 0.62, "blur": 0.1}},
        resolution=(320, 160),
    ),
    Case(
        name="overlay_wet",
        mode=MODE_OVERLAY,
        description="向きを問わず濡らす設定。darken が素の色をどれだけ沈めるか。",
        module_styles={"SBSOverlayStyle": {"upBias": 0.5, "border": 0.45, "blur": 0.5, "darken": 1.0}},
        resolution=(320, 160),
    ),
    Case(
        name="overlay_streak",
        mode=MODE_OVERLAY,
        description="垂れを足した状態。時間を固定して決定的に描く。",
        module_styles={"SBSOverlayStyle": {"border": 0.35, "blur": 0.2, "streak": 1.0, "streakScale": 12.0, "time": 3.0}},
        resolution=(320, 160),
    ),
    Case(
        name="pixel_levels",
        mode=MODE_PIXEL,
        description="色数を落としただけの状態。段の位置がずれると落ちる。",
        module_styles={"SBSPixelStyle": {"levels": 4.0, "dither": 0.0}},
        resolution=(320, 160),
    ),
    Case(
        name="pixel_dither",
        mode=MODE_PIXEL,
        description="同じ色数に整列ディザをかけた状態。升目の大きさが効く。",
        module_styles={"SBSPixelStyle": {"levels": 4.0, "dither": 1.0, "cellSize": 4.0}},
        resolution=(320, 160),
    ),
    Case(
        name="pixel_palette",
        mode=MODE_PIXEL,
        description="明るさでパレットに寄せた状態。色が置き換わる。",
        module_styles={"SBSPixelStyle": {"levels": 8.0, "palette": 1.0}},
        resolution=(320, 160),
    ),
]

CASES_BY_NAME = {case.name: case for case in CASES}
