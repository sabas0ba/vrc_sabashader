#ifndef SABASHADER_PIXELART_CORE_INCLUDED
#define SABASHADER_PIXELART_CORE_INCLUDED

// =============================================================================
// Pixel Art core
// -----------------------------------------------------------------------------
// 色数を落としてドット絵の質感に寄せる数式。
//
// できることの範囲について:
//   モジュールは「本体が描いた結果」の上に乗るだけで、隣接ピクセルを読めない。
//   そのため **画面を実際に間引く（低解像度化）ことはできない**。
//   GrabPass を使えば可能だが、このリポジトリでは使わない方針。
//   代わりに、色数の量子化・パレット・画面基準の整列ディザで
//   ドット絵の見え方に寄せる。「解像度」はディザの升目の大きさを指す。
//
// Unity / レンダーパイプライン / Shader Core のどれにも依存しない。
// HLSL としても、tests/harness/prelude.glsl を前置した GLSL 3.30 core としても
// そのままコンパイルできる部分集合で書いてある。
// =============================================================================

struct SBSPixelStyle
{
    // 効き。0 で完全に無効。
    half amount;

    // 1 チャンネルあたりの色数。2 で白黒、4 で 64 色相当。
    half levels;

    // ディザの強さと升目の大きさ（画面ピクセル）。
    half dither;
    half cellSize;

    // パレットへの寄せ具合。0 で素の色のまま。
    half palette;
};

// -----------------------------------------------------------------------------
// ディザ
// -----------------------------------------------------------------------------

// 2x2 の整列ディザ。値は 0, 2, 3, 1 の並び。
half SBSPixelBayer2(half2 cell)
{
    return fmod(3.0 * cell.y + 2.0 * cell.x, 4.0);
}

// 4x4 の整列ディザ。0-1 の 16 段。配列を使わずに 2x2 を 2 段重ねて作る。
half SBSPixelBayer4(half2 p)
{
    half2 low = floor(fmod(p, half2(2.0, 2.0)));
    half2 high = floor(fmod(floor(p * 0.5), half2(2.0, 2.0)));
    return (SBSPixelBayer2(low) * 4.0 + SBSPixelBayer2(high)) / 16.0;
}

// 画面座標を升目に落としてディザのしきい値を出す。
half SBSPixelThreshold(half2 screenPosition, SBSPixelStyle st)
{
    half cell = max(st.cellSize, 1.0);
    return SBSPixelBayer4(floor(screenPosition / cell));
}

// -----------------------------------------------------------------------------
// 量子化
// -----------------------------------------------------------------------------

// 1 成分を levels 段に落とす。threshold でディザをかける。
half SBSPixelQuantizeChannel(half value, half levels, half threshold, half dither)
{
    half steps = max(levels, 2.0) - 1.0;
    half offset = (saturate(dither) * (threshold - 0.5));
    return saturate(floor(saturate(value) * steps + 0.5 + offset) / steps);
}

half3 SBSPixelQuantize(half3 c, half threshold, SBSPixelStyle st)
{
    return half3(
        SBSPixelQuantizeChannel(c.r, st.levels, threshold, st.dither),
        SBSPixelQuantizeChannel(c.g, st.levels, threshold, st.dither),
        SBSPixelQuantizeChannel(c.b, st.levels, threshold, st.dither));
}

// -----------------------------------------------------------------------------
// パレット
// -----------------------------------------------------------------------------

// パレットテクスチャを引くための座標。明るさで 1 次元に潰す。
// テクスチャの読み出しはモジュール側（Unity 依存）で行う。
half SBSPixelPaletteCoord(half3 c, half threshold, SBSPixelStyle st)
{
    half lum = dot(saturate(c), half3(0.2126, 0.7152, 0.0722));
    return SBSPixelQuantizeChannel(lum, st.levels, threshold, st.dither);
}

// 素の色とパレット色を混ぜる。
half3 SBSPixelApply(half3 original, half3 quantized, half3 paletteColor, SBSPixelStyle st)
{
    half3 mixed = lerp(quantized, paletteColor, saturate(st.palette));
    return lerp(original, mixed, saturate(st.amount));
}

#endif // SABASHADER_PIXELART_CORE_INCLUDED
