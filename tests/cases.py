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
MODE_DROPLET = 9
MODE_CRT = 10
MODE_CRT_SOLID = 11
MODE_VIDEO_INPUT = 12
MODE_DISPLAY_PANEL = 13

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
    "thickness": 0.0,
    "droplet": 0.0,
    "dropletScale": 40.0,
    "dropletBump": 1.0,
    "dropletSize": 0.28,
    "dropletVariance": 0.6,
    "mobility": 0.0,
    "streak": 0.0,
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
    "preset": 0.0,
}

MODULE_STYLE_DEFAULTS["SBSPixelStyle"] = DEFAULT_PIXEL

# CrtGlitchCore.hlsl の SBSCrtStyle
#
# 効果はすべて 0 から始める。ケースごとに見たいものだけを上げる。
DEFAULT_CRT: Dict[str, object] = {
    "amount": 1.0,
    "additivePass": 0.0,
    "scanline": 0.0,
    "scanlinePitch": 4.0,
    "mask": 0.0,
    "maskPitch": 6.0,
    "vignette": 0.0,
    "aberration": 0.0,
    "roll": 0.0,
    "rollSpeed": 0.15,
    "noise": 0.0,
    "noiseScale": 2.0,
    "noiseTone": 0.0,
    "noiseChroma": 0.0,
    "staticAmount": 0.0,
    "staticTear": 8.0,
    "glitch": 0.0,
    "glitchScale": 12.0,
    "glitchShift": 6.0,
    "glitchColor": 0.5,
    "block": 0.0,
    "blockScale": 16.0,
    "blockShift": 6.0,
    "blockCrush": 0.5,
    "tearing": 0.0,
    "tearScale": 0.05,
    "time": 0.0,
}

MODULE_STYLE_DEFAULTS["SBSCrtStyle"] = DEFAULT_CRT

# VideoInputCore.hlsl の SBSVideoInputStyle
DEFAULT_VIDEO_INPUT: Dict[str, object] = {
    "amount": 1.0,
    "tint": (1.0, 1.0, 1.0, 1.0),
    "brightness": 1.0,
    "mirrorX": 0.0,
    "flipY": 0.0,
    "additivePass": 0.0,
}

MODULE_STYLE_DEFAULTS["SBSVideoInputStyle"] = DEFAULT_VIDEO_INPUT

# DisplayPanelCore.hlsl の SBSDisplayPanelStyle
DEFAULT_DISPLAY_PANEL: Dict[str, object] = {
    "amount": 1.0,
    "mode": 0.0,
    "pixelPitch": 6.0,
    "fill": 0.82,
    "grid": 1.0,
    "subpixel": 0.85,
    "subpixelOrder": 0.0,
    "brightness": 1.0,
    "viewAngle": 0.0,
    "tileCells": 16.0,
    "seam": 2.0,
    "tileVariation": 0.08,
}

MODULE_STYLE_DEFAULTS["SBSDisplayPanelStyle"] = DEFAULT_DISPLAY_PANEL

# DecalCore.hlsl の SBSDecalStyle
DEFAULT_DECAL: Dict[str, object] = {
    "amount": 0.0,
    "mapping": 0.0,
    "blendMode": 0.0,
    "tint": (1.0, 1.0, 1.0, 1.0),
    "projectorCenter": (0.0, 0.0, 0.0),
    "projectorRotation": (0.0, 0.0, 0.0),
    "projectorSize": (1.0, 1.0, 0.2),
    "angleFade": 0.2,
    "edgeSoftness": 0.03,
}

MODULE_STYLE_DEFAULTS["SBSDecalStyle"] = DEFAULT_DECAL

# SurfaceDetailCore.hlsl の SBSSurfaceDetailStyle
DEFAULT_SURFACE_DETAIL: Dict[str, object] = {
    "amount": 0.0,
    "mode": 0.0,
    "scale": 120.0,
    "textureStrength": 0.0,
    "albedoVariation": 0.08,
    "normalStrength": 0.35,
    "roughnessVariation": 0.35,
    "pore": 0.7,
    "weave": 0.8,
    "sheen": 0.25,
    "sheenColor": (1.0, 0.95, 0.9),
}

MODULE_STYLE_DEFAULTS["SBSSurfaceDetailStyle"] = DEFAULT_SURFACE_DETAIL

# SpatialInteriorCore.hlsl の SBSSpatialStyle
DEFAULT_SPATIAL: Dict[str, object] = {
    "amount": 0.0,
    "preset": 0.0,
    "side": 1.0,
    "region": 0.0,
    "colorA": (0.015, 0.025, 0.09),
    "colorB": (0.35, 0.08, 0.55),
    "emission": 2.0,
    "scale": 5.0,
    "depth": 2.0,
    "parallax": 1.0,
    "starDensity": 0.28,
    "starSize": 0.18,
    "nebula": 0.7,
    "nebulaScale": 0.55,
    "time": 0.0,
    "riftCenter": (0.5, 0.5),
    "riftSize": (0.8, 0.8),
    "riftNoise": 0.22,
    "edgeWidth": 0.08,
    "edgeColor": (0.15, 0.8, 1.0),
    "additivePass": 0.0,
}

MODULE_STYLE_DEFAULTS["SBSSpatialStyle"] = DEFAULT_SPATIAL

# TransitionCore.hlsl の SBSTransitionStyle
DEFAULT_TRANSITION: Dict[str, object] = {
    "progress": 1.0,
    "mode": 0.0,
    "direction": (0.0, 1.0, 0.0),
    "boundsMin": -1.0,
    "boundsMax": 1.0,
    "noiseScale": 8.0,
    "noiseAmount": 0.3,
    "edgeWidth": 0.06,
    "edgeColor": (0.3, 1.6, 2.0),
    "displacement": 0.2,
    "blockScale": 8.0,
    "liquidAmplitude": 0.06,
    "liquidFrequency": 5.0,
    "liquidSpeed": 1.0,
    "liquidWobble": 0.5,
    "liquidPuddle": 0.0,
    "liquidPuddleHeight": 0.08,
    "liquidPuddleSpread": 0.45,
    "liquidTint": (0.25, 0.55, 1.0, 0.35),
    "time": 0.0,
}

MODULE_STYLE_DEFAULTS["SBSTransitionStyle"] = DEFAULT_TRANSITION

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
        name="overlay_droplet",
        mode=MODE_DROPLET,
        description="付着した粒だけの状態。動かないので大きさのばらつきが見える。",
        module_styles={
            "SBSOverlayStyle": {
                "upBias": 0.0,
                "border": 0.3,
                "blur": 0.1,
                "droplet": 1.0,
                "dropletScale": 14.0,
            }
        },
        resolution=(320, 160),
    ),
    Case(
        name="overlay_runoff",
        mode=MODE_DROPLET,
        description="半分の列が流れ出した状態。止まる粒と流れる粒が混ざる。時間は固定。",
        module_styles={
            "SBSOverlayStyle": {
                "upBias": 0.0,
                "border": 0.3,
                "blur": 0.1,
                "droplet": 1.0,
                "dropletScale": 14.0,
                "mobility": 0.5,
                "streak": 1.0,
                "time": 3.0,
            }
        },
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
    Case(
        name="pixel_preset_lcd",
        mode=MODE_PIXEL,
        description="組み込みパレットの単色 LCD。明るさをランプに載せ替える。",
        module_styles={"SBSPixelStyle": {"levels": 8.0, "dither": 0.0, "palette": 1.0, "preset": 1.0}},
        resolution=(320, 160),
    ),
    Case(
        name="pixel_preset_8bit",
        mode=MODE_PIXEL,
        description="組み込みパレットの 8bit。色そのものを段に落とすので色相が残る。",
        module_styles={"SBSPixelStyle": {"levels": 8.0, "dither": 0.0, "palette": 1.0, "preset": 7.0}},
        resolution=(320, 160),
    ),
    Case(
        name="video_input",
        mode=MODE_VIDEO_INPUT,
        description="外部テクスチャを Unlit で表示する。UV、入力色、入力アルファが変わると落ちる。",
        resolution=(320, 160),
    ),
    Case(
        name="video_input_mix",
        mode=MODE_VIDEO_INPUT,
        description="入力を色付けし、非対称な UV 範囲の中で上下左右反転する。元の色との合成率も見る。",
        module_styles={
            "SBSVideoInputStyle": {
                "amount": 0.8,
                "tint": (0.72, 1.05, 1.30, 0.85),
                "brightness": 1.2,
                "mirrorX": 1.0,
                "flipY": 1.0,
            }
        },
        resolution=(320, 160),
    ),
    Case(
        name="display_lcd",
        mode=MODE_DISPLAY_PANEL,
        description="LCD の RGB ストライプと画素間の遮光部を表示する。",
        module_styles={
            "SBSDisplayPanelStyle": {
                "mode": 0.0,
                "pixelPitch": 9.0,
                "fill": 0.82,
                "subpixel": 0.9,
            }
        },
        resolution=(320, 160),
    ),
    Case(
        name="display_led",
        mode=MODE_DISPLAY_PANEL,
        description="LED の RGB 発光点と画素間の暗部を表示する。",
        module_styles={
            "SBSDisplayPanelStyle": {
                "mode": 1.0,
                "pixelPitch": 12.0,
                "fill": 0.9,
                "subpixel": 0.9,
                "brightness": 1.15,
            }
        },
        resolution=(320, 160),
    ),
    Case(
        name="display_led_wall",
        mode=MODE_DISPLAY_PANEL,
        description="LED 大画面のタイル継ぎ目とタイル単位の輝度差を表示する。",
        module_styles={
            "SBSDisplayPanelStyle": {
                "mode": 2.0,
                "pixelPitch": 8.0,
                "fill": 0.9,
                "tileCells": 8.0,
                "seam": 3.0,
                "tileVariation": 0.15,
            }
        },
        resolution=(320, 160),
    ),
    Case(
        name="crt_scanline",
        mode=MODE_CRT,
        description="走査線とシャドウマスクだけ。線の間隔と縞の周期がずれると落ちる。",
        module_styles={
            "SBSCrtStyle": {"scanline": 0.6, "scanlinePitch": 4.0, "mask": 0.5, "maskPitch": 6.0}
        },
        resolution=(320, 160),
    ),
    Case(
        name="crt_aberration",
        mode=MODE_CRT,
        description="色ずれと周辺の落ち込みだけ。外側ほど赤と青が離れる。"
        "ずれ幅は差が見えるようスライダの上限を使っている。",
        module_styles={"SBSCrtStyle": {"aberration": 8.0, "vignette": 0.5}},
        resolution=(320, 160),
    ),
    Case(
        name="crt_glitch",
        mode=MODE_CRT,
        description="乱れた帯だけ。時間を固定して、帯の位置と色の入れ替えを見る。",
        module_styles={
            "SBSCrtStyle": {
                "glitch": 0.5,
                "glitchScale": 10.0,
                "glitchShift": 12.0,
                "glitchColor": 0.8,
                "time": 1.0,
            }
        },
        resolution=(320, 160),
    ),
    Case(
        name="crt_roll",
        mode=MODE_CRT,
        description="ロールバーだけ。時間を固定して、帯の位置と幅を見る。"
        "帯が色帯とちょうど重ならないよう、時間を選んである。",
        module_styles={"SBSCrtStyle": {"roll": 1.0, "rollSpeed": 0.15, "time": 5.0}},
        resolution=(320, 160),
    ),
    Case(
        name="crt_noise",
        mode=MODE_CRT,
        description="ざらつきだけ。時間を固定して、粒の位置と大きさを見る。",
        module_styles={"SBSCrtStyle": {"noise": 0.25, "noiseScale": 3.0, "time": 1.0}},
        resolution=(320, 160),
    ),
    Case(
        name="crt_block",
        mode=MODE_CRT,
        description="升の破綻だけ。時間を固定して、升の位置・横ずれ・色の潰れを見る。",
        module_styles={
            "SBSCrtStyle": {
                "block": 0.5,
                "blockScale": 20.0,
                "blockShift": 10.0,
                "blockCrush": 0.9,
                "time": 1.0,
            }
        },
        resolution=(320, 160),
    ),
    Case(
        name="crt_static",
        mode=MODE_CRT,
        description="砂嵐で半分ほど置き換えた状態。置き換える前の横方向の引き裂きも見る。",
        module_styles={
            "SBSCrtStyle": {
                "staticAmount": 0.6,
                "staticTear": 16.0,
                "noiseScale": 3.0,
                "time": 1.0,
            }
        },
        resolution=(320, 160),
    ),
    Case(
        name="crt_grain_tone",
        mode=MODE_CRT,
        description="中間調へ寄せた色付きのざらつき。明部と暗部で粒が消えることを見る。",
        module_styles={
            "SBSCrtStyle": {
                "noise": 0.4,
                "noiseScale": 2.0,
                "noiseTone": 1.0,
                "noiseChroma": 1.0,
                "time": 1.0,
            }
        },
        resolution=(320, 160),
    ),
    Case(
        name="crt_solid",
        mode=MODE_CRT_SOLID,
        description="立体に一式かけた状態。シルエットの上で走査線と縞が"
        "どう乗るかを見る。色ずれはレイマーチの分岐と相性が悪いので入れていない。",
        module_styles={
            "SBSCrtStyle": {
                "scanline": 0.5,
                "scanlinePitch": 4.0,
                "mask": 0.3,
                "maskPitch": 6.0,
                "vignette": 0.35,
                "noise": 0.05,
                "noiseScale": 2.0,
                "time": 1.0,
            }
        },
    ),
]

CASES_BY_NAME = {case.name: case for case in CASES}
