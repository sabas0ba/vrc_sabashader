#ifndef SABASHADER_PIXELART_CORE_INCLUDED
#define SABASHADER_PIXELART_CORE_INCLUDED

// =============================================================================
// Pixel Art core
// -----------------------------------------------------------------------------
// 色数を落としてドット絵の質感に寄せる数式。
//
// どうやって升目状にしているか:
//   モジュールは隣接ピクセルを読めないので、画面を撮り直して間引くことはできない。
//   代わりに **升目の中心での値を勾配 (ddx/ddy) から 1 次近似で推定** し、
//   シェーディングの入力（ベースカラー・法線・UV）をその値に差し替える。
//   入力が升目内で一定になるので、そこから先の計算結果も升目内で一定になり、
//   描画結果が升目状になる。
//
//   法線や UV は面の上で滑らかなので、この近似はよく合う。
//   一方でシルエット（形の縁）は頂点の位置で決まるため升目状にはならない。
//   テクスチャの模様の境目のように急に変わるところでは近似が外れる。
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

    // 組み込みパレットの番号。0 でテクスチャを使う。
    half preset;
};

// -----------------------------------------------------------------------------
// 升目への吸着
// -----------------------------------------------------------------------------

// 今のピクセルから升目の中心までの、画面上のずれ。
half2 SBSPixelCellDelta(half2 screenPosition, SBSPixelStyle st)
{
    half cell = max(st.cellSize, 1.0);
    half2 center = (floor(screenPosition / cell) + half2(0.5, 0.5)) * cell;
    return center - screenPosition;
}

// 升目の中心での値を勾配から推定して差し替える。
//
// シルエットの縁のように勾配が急なところでは 1 次近似が大きく外れるので、
// 補正量に上限を設けて元の値へ寄せる。これをしないと縁に黒い点が散る。
half SBSPixelSnap1(half value, half2 delta, half amount)
{
    half correction = ddx(value) * delta.x + ddy(value) * delta.y;
    half magnitude = abs(correction);
    correction *= (magnitude > 1.0) ? (1.0 / magnitude) : 1.0;
    return lerp(value, value + correction, saturate(amount));
}

half2 SBSPixelSnap2(half2 value, half2 delta, half amount)
{
    half2 correction = ddx(value) * delta.x + ddy(value) * delta.y;
    half magnitude = length(correction);
    correction *= (magnitude > 1.0) ? (1.0 / magnitude) : 1.0;
    return lerp(value, value + correction, saturate(amount));
}

half3 SBSPixelSnap3(half3 value, half2 delta, half amount)
{
    half3 correction = ddx(value) * delta.x + ddy(value) * delta.y;
    half magnitude = length(correction);
    correction *= (magnitude > 1.0) ? (1.0 / magnitude) : 1.0;
    return lerp(value, value + correction, saturate(amount));
}

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

// 色を段に落とす。
//
// チャンネルごとに独立して落とすと、隣り合う升目で R/G/B が別々の段へ飛び、
// 肌色の上に黄や赤の点が散るような色ノイズになる。明るさだけを段に落として
// 色味の比を保つと、段差が付いたまま色が破綻しない。
half3 SBSPixelQuantize(half3 c, half threshold, SBSPixelStyle st)
{
    half3 col = saturate(c);
    half lum = dot(col, half3(0.2126, 0.7152, 0.0722));
    half quantized = SBSPixelQuantizeChannel(lum, st.levels, threshold, st.dither);
    half scale = quantized / max(lum, 1.0e-3);
    return saturate(col * scale);
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

// -----------------------------------------------------------------------------
// 組み込みパレット
// -----------------------------------------------------------------------------

// 3 点のランプ。暗部・中間・明部を指定して間を埋める。
half3 SBSPixelRamp(half3 dark, half3 mid, half3 bright, half t)
{
    half v = saturate(t);
    return (v < 0.5) ? lerp(dark, mid, v * 2.0) : lerp(mid, bright, v * 2.0 - 1.0);
}

// 番号で組み込みパレットを引く。coord は量子化済みの明るさ。
//
// テクスチャを用意しなくても代表的な見た目を出せるようにしてある。
// 8bit だけは明るさではなく色そのものを段に落とす（RGB332 相当）ので、
// 他と違って色相が保たれる。
half3 SBSPixelPalettePreset(half3 color, half coord, SBSPixelStyle st)
{
    half preset = st.preset;

    if (preset < 0.5) return color;

    // 単色 LCD（緑）
    if (preset < 1.5)
        return SBSPixelRamp(half3(0.05, 0.12, 0.06), half3(0.20, 0.55, 0.20), half3(0.72, 0.95, 0.45), coord);

    // レトロゲーム風（4 階調の緑）
    if (preset < 2.5)
    {
        half stepped = floor(saturate(coord) * 3.999) / 3.0;
        return SBSPixelRamp(half3(0.06, 0.22, 0.06), half3(0.34, 0.60, 0.20), half3(0.88, 0.97, 0.60), stepped);
    }

    // モノクロ（コントラストを立てた白黒）
    if (preset < 3.5)
    {
        half mono = saturate((coord - 0.5) * 1.6 + 0.5);
        return half3(mono, mono, mono);
    }

    // セピア
    if (preset < 4.5)
        return SBSPixelRamp(half3(0.12, 0.08, 0.05), half3(0.55, 0.42, 0.28), half3(0.98, 0.92, 0.80), coord);

    // グレー（素直な灰色階調）
    if (preset < 5.5) return half3(coord, coord, coord);

    // 1bit（白と黒だけ）
    if (preset < 6.5)
    {
        half bit = step(0.5, coord);
        return half3(bit, bit, bit);
    }

    // 8bit（RGB332 相当。色相が残る）
    if (preset < 7.5)
    {
        half3 c = saturate(color);
        return half3(
            floor(c.r * 7.0 + 0.5) / 7.0,
            floor(c.g * 7.0 + 0.5) / 7.0,
            floor(c.b * 3.0 + 0.5) / 3.0);
    }

    // ネオン
    if (preset < 8.5)
        return SBSPixelRamp(half3(0.06, 0.02, 0.14), half3(0.85, 0.10, 0.75), half3(0.35, 0.98, 0.95), coord);

    // 夕焼け
    return SBSPixelRamp(half3(0.10, 0.09, 0.28), half3(0.78, 0.30, 0.32), half3(1.00, 0.86, 0.45), coord);
}

// 素の色とパレット色を混ぜる。
half3 SBSPixelApply(half3 original, half3 quantized, half3 paletteColor, SBSPixelStyle st)
{
    half3 mixed = lerp(quantized, paletteColor, saturate(st.palette));
    return lerp(original, mixed, saturate(st.amount));
}

#endif // SABASHADER_PIXELART_CORE_INCLUDED
