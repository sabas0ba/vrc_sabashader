#ifndef SABASHADER_ILLUST2D_CORE_INCLUDED
#define SABASHADER_ILLUST2D_CORE_INCLUDED

// =============================================================================
// Illust2D shading core
// -----------------------------------------------------------------------------
// 2D イラスト風シェーディングの数式だけを集めたファイル。
// Unity / レンダーパイプライン / Shader Core のどれにも依存しない。
//
// このファイルは HLSL としても、tests/harness/prelude.glsl を前置した GLSL 3.30
// core としても、そのままコンパイルできる部分集合で書かれている。
// ヘッドレス回帰テストは「このファイル自身」をコンパイルして描画するので、
// テストされる数式と出荷される数式は同一のコードになる。
//
// 編集時のルール（docs/testing.md も参照）:
//   * ベクターは必ず全成分を書く:  half3(0.0, 0.0, 0.0)   NG: half3 a = 0.0;
//   * 行列・テクスチャ・グローバル変数・static は使わない
//   * 使ってよい組み込みは prelude.glsl が用意しているものだけ
//     (saturate / lerp / frac / rsqrt / abs / min / max / pow / exp2 /
//      floor / clamp / dot / normalize / step / smoothstep / mix 相当)
//   * スカラーとベクターの暗黙変換に頼らない
// =============================================================================

// -----------------------------------------------------------------------------
// 色空間ユーティリティ
// -----------------------------------------------------------------------------

half SBSLuminance(half3 c)
{
    return dot(c, half3(0.2126, 0.7152, 0.0722));
}

// Sam Hocevar / branchless RGB<->HSV
half3 SBSRgbToHsv(half3 c)
{
    half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    half4 p = (c.g < c.b) ? half4(c.b, c.g, K.w, K.z) : half4(c.g, c.b, K.x, K.y);
    half4 q = (c.r < p.x) ? half4(p.x, p.y, p.w, c.r) : half4(c.r, p.y, p.z, p.x);
    half d = q.x - min(q.w, q.y);
    half e = 1.0e-6;
    return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

half3 SBSHsvToRgb(half3 c)
{
    half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    half3 p = abs(frac(half3(c.x + K.x, c.x + K.y, c.x + K.z)) * 6.0 - half3(K.w, K.w, K.w));
    half3 one = half3(1.0, 1.0, 1.0);
    return c.z * lerp(one, saturate(p - one), c.y);
}

// 色相/彩度/明度を相対的にずらす。2D イラストの「影は青紫寄りで彩度高め」を作る中核。
half3 SBSShiftHsv(half3 col, half hueShift, half saturationMul, half valueMul)
{
    half3 hsv = SBSRgbToHsv(max(col, half3(0.0, 0.0, 0.0)));
    hsv.x = frac(hsv.x + hueShift);
    hsv.y = saturate(hsv.y * saturationMul);
    hsv.z = saturate(hsv.z * valueMul);
    return SBSHsvToRgb(hsv);
}

// -----------------------------------------------------------------------------
// ランプ（セルシェーディング）
// -----------------------------------------------------------------------------

// border を境に blur 幅で 0->1 に線形遷移する。blur=0 で完全な 2 値。
half SBSToonStep(half x, half border, half blur)
{
    half b = max(blur, 1.0e-4);
    return saturate((x - border) / b + 0.5);
}

// 0..1 を steps 段に量子化する。steps < 2 のときは何もしない。
half SBSPosterize(half x, half steps)
{
    half s = floor(steps);
    if (s < 2.0) return x;
    return min(floor(saturate(x) * s) / (s - 1.0), 1.0);
}

// -----------------------------------------------------------------------------
// パラメータ構造体
// -----------------------------------------------------------------------------

// ピクセルごとに変わる入力
struct SBSSurface
{
    half3 albedo;        // ベースカラー（テクスチャ適用済み）
    half3 N;             // ワールド法線（正規化済み）
    half3 L;             // ワールドライト方向（正規化済み、面 -> ライト）
    half3 V;             // ワールド視線方向（正規化済み、面 -> カメラ）
    half3 lightColor;    // 指向性ライトの色
    half3 ambientColor;  // 環境光の色
    half  attenuation;   // リアルタイム影 * 距離減衰 (0..1)
    half  shadeMask;     // 影の付きやすさマスク (0..1, 1 で通常)
    half  specularMask;  // ハイライトマスク (0..1)
    half  rimMask;       // リムライトマスク (0..1)
};

// マテリアルごとに固定のスタイル設定
struct SBSStyle
{
    // 1 影
    half  shadeBorder1;
    half  shadeBlur1;
    half3 shade1Color;
    half  shade1HueShift;
    half  shade1Saturation;
    half  shade1Value;

    // 2 影
    half  shadeBorder2;
    half  shadeBlur2;
    half3 shade2Color;
    half  shade2HueShift;
    half  shade2Saturation;
    half  shade2Value;

    // ランプの段数（0 または 1 で無効、2 以上でポスタライズ）
    half  shadeSteps;
    // 影の受け方 (0: リアルタイム影を無視, 1: そのまま乗せる)
    half  shadowStrength;

    // トゥーンスペキュラ
    half3 specularColor;
    half  specularBorder;
    half  specularBlur;
    half  specularSmoothness;

    // リムライト
    half3 rimColor;
    half  rimBorder;
    half  rimBlur;
    half  rimLightAlign;   // 1 でライト側にだけ出る

    // ライト強度のクランプ（VRChat のワールド差を吸収する）
    half  lightMinLimit;
    half  lightMaxLimit;
    half  monochromeLighting;
    half  asUnlit;

    // 仕上げのカラーグレーディング
    half  saturation;
    half  contrast;
};

// -----------------------------------------------------------------------------
// シェーディング
// -----------------------------------------------------------------------------

// 影の落ち具合。1 = 完全に光が当たっている。
half SBSShadingFactor(SBSSurface s, SBSStyle st)
{
    half nl = dot(s.N, s.L) * 0.5 + 0.5;
    half atten = lerp(1.0, saturate(s.attenuation), saturate(st.shadowStrength));
    return saturate(nl * atten * s.shadeMask);
}

// 1 影 / 2 影を albedo に適用した「塗り」の色。ライトの明るさはまだ掛けていない。
half3 SBSShadedAlbedo(SBSSurface s, SBSStyle st)
{
    half ln = SBSShadingFactor(s, st);

    half t1 = SBSPosterize(SBSToonStep(ln, st.shadeBorder1, st.shadeBlur1), st.shadeSteps);
    half t2 = SBSPosterize(SBSToonStep(ln, st.shadeBorder2, st.shadeBlur2), st.shadeSteps);

    half3 shade1 = SBSShiftHsv(s.albedo, st.shade1HueShift, st.shade1Saturation, st.shade1Value) * st.shade1Color;
    half3 shade2 = SBSShiftHsv(s.albedo, st.shade2HueShift, st.shade2Saturation, st.shade2Value) * st.shade2Color;

    half3 col = lerp(shade2, shade1, t2);
    col = lerp(col, s.albedo, t1);
    return col;
}

// ワールドの明るさを一定レンジに押し込める。VRChat 用トゥーンの定番処理。
half3 SBSLimitLight(half3 lightColor, SBSStyle st)
{
    half3 white = half3(1.0, 1.0, 1.0);
    half lum = SBSLuminance(lightColor);
    half3 c = lerp(lightColor, half3(lum, lum, lum), saturate(st.monochromeLighting));

    half l = SBSLuminance(c);
    half target = clamp(l, st.lightMinLimit, max(st.lightMaxLimit, st.lightMinLimit));
    half3 dir = (l > 1.0e-4) ? (c / l) : white;

    return lerp(dir * target, white, saturate(st.asUnlit));
}

// 面に届く総照度（指向性 + 環境光）をクランプしたもの。
half3 SBSIlluminate(SBSSurface s, SBSStyle st)
{
    return SBSLimitLight(s.lightColor + s.ambientColor, st);
}

// アニメ塗り風の硬いハイライト。
half3 SBSSpecularTerm(SBSSurface s, SBSStyle st)
{
    half3 H = normalize(s.L + s.V);
    half nh = saturate(dot(s.N, H));
    half power = exp2(saturate(st.specularSmoothness) * 10.0 + 1.0);
    half spec = pow(nh, power);
    half shape = SBSToonStep(spec, st.specularBorder, st.specularBlur);
    half facing = saturate(dot(s.N, s.L) * 4.0);
    return st.specularColor * (shape * facing * s.specularMask * saturate(s.attenuation));
}

// 輪郭沿いの光。
half3 SBSRimTerm(SBSSurface s, SBSStyle st)
{
    half nv = saturate(1.0 - dot(s.N, s.V));
    half rim = SBSToonStep(nv, st.rimBorder, st.rimBlur);
    half nl = saturate(dot(s.N, s.L) * 2.0 - 0.2);
    half align = lerp(1.0, nl, saturate(st.rimLightAlign));
    return st.rimColor * (rim * align * s.rimMask);
}

// 仕上げの彩度・コントラスト調整。イラストらしい色の締まりを出す。
half3 SBSGrade(half3 col, SBSStyle st)
{
    half3 c = max(col, half3(0.0, 0.0, 0.0));
    half lum = SBSLuminance(c);
    c = max(lerp(half3(lum, lum, lum), c, st.saturation), half3(0.0, 0.0, 0.0));
    half3 mid = half3(0.5, 0.5, 0.5);
    return max((c - mid) * st.contrast + mid, half3(0.0, 0.0, 0.0));
}

// フルの合成。Unity 側フラグメントもテストシーンも最終的にこの並びを再現する。
half3 SBSComposeIllust(SBSSurface s, SBSStyle st)
{
    half3 shaded = SBSShadedAlbedo(s, st);
    half3 add = SBSSpecularTerm(s, st) + SBSRimTerm(s, st);
    half3 illum = SBSIlluminate(s, st);
    return SBSGrade((shaded + add) * illum, st);
}

// -----------------------------------------------------------------------------
// アウトライン
// -----------------------------------------------------------------------------

// アウトラインの色。albedo に寄せるほど「線画に色トレスした」見た目になる。
half3 SBSOutlineColor(half3 albedo, half3 outlineColor, half albedoBlend, half hueShift, half saturationMul, half valueMul)
{
    half3 tinted = SBSShiftHsv(albedo, hueShift, saturationMul, valueMul);
    return lerp(outlineColor, tinted * outlineColor, saturate(albedoBlend));
}

// アウトラインの押し出し幅。カメラ距離に対して一定の太さに見えるように補正する。
half SBSOutlineWidth(half width, half vertexMask, half distanceToCamera, half fixedWidth)
{
    half w = width * max(vertexMask, 0.0);
    half scaled = w * clamp(distanceToCamera, 0.05, 10.0);
    return lerp(w, scaled, saturate(fixedWidth));
}

#endif // SABASHADER_ILLUST2D_CORE_INCLUDED
