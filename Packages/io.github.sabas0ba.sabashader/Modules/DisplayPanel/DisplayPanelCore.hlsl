#ifndef SABASHADER_DISPLAY_PANEL_CORE_INCLUDED
#define SABASHADER_DISPLAY_PANEL_CORE_INCLUDED

// =============================================================================
// Display panel core
// -----------------------------------------------------------------------------
// LCD、LED、LED Wall の画素構造を画面ピクセル座標から作る。
// 入力色の再サンプリングは行わず、画素内の開口部、サブピクセル、パネル継ぎ目を
// 乗算するため、任意のシェーダー出力や VideoInput の後段で使用できる。
//
// Unity / レンダーパイプライン / Shader Core に依存しない。テストでは GLSL 3.30
// core として同じ数式をコンパイルする。
// =============================================================================

struct SBSDisplayPanelStyle
{
    // 全体の合成率。0 なら元の色をそのまま返す。
    half amount;

    // 0: LCD、1: LED、2: LED Wall。
    half mode;

    // 1 画素の幅（画面ピクセル）、発光部の占有率、格子の合成率。
    half pixelPitch;
    half fill;
    half grid;

    // RGB サブピクセルの分離量と並び。0: RGB、1: BGR。
    half subpixel;
    half subpixelOrder;

    // 表示輝度と、正面以外から見たときの減衰量。
    half brightness;
    half viewAngle;

    // LED Wall のタイル幅（画素数）、継ぎ目幅（画面ピクセル）、タイルごとの輝度差。
    half tileCells;
    half seam;
    half tileVariation;
};

half SBSDisplayPanelAAMask(float distanceFromEdge, float aa)
{
    return 1.0 - smoothstep(-aa, aa, distanceFromEdge);
}

half SBSDisplayPanelRect(float2 local, float halfSize, float aa)
{
    float distanceFromEdge = max(abs(local.x), abs(local.y)) - halfSize;
    return SBSDisplayPanelAAMask(distanceFromEdge, aa);
}

half SBSDisplayPanelCircle(float2 local, float radius, float aa)
{
    return SBSDisplayPanelAAMask(length(local) - radius, aa);
}

half3 SBSDisplayPanelOrder(half3 rgb, half order)
{
    return (order < 0.5) ? rgb : rgb.bgr;
}

half3 SBSDisplayPanelLCD(half3 color, float2 screen, SBSDisplayPanelStyle st)
{
    float pitch = max(st.pixelPitch, 1.0);
    float2 cellUV = frac(screen / pitch);
    float2 local = cellUV - float2(0.5, 0.5);
    float aa = max(0.5 / pitch, 1.0e-3);
    half aperture = SBSDisplayPanelRect(local, saturate(st.fill) * 0.5, aa);

    half stripeR = 1.0 - step(1.0 / 3.0, cellUV.x);
    half stripeG = step(1.0 / 3.0, cellUV.x) * (1.0 - step(2.0 / 3.0, cellUV.x));
    half stripeB = step(2.0 / 3.0, cellUV.x);
    half3 stripe = SBSDisplayPanelOrder(half3(stripeR, stripeG, stripeB), st.subpixelOrder);

    // 3 本を平均したときに元の色へ戻るよう、選択チャンネルを 3 倍する。
    half3 subpixelMask = lerp(half3(1.0, 1.0, 1.0), stripe * 3.0, saturate(st.subpixel));
    half apertureMask = lerp(1.0, aperture, saturate(st.grid));
    return color * subpixelMask * apertureMask;
}

half3 SBSDisplayPanelLED(half3 color, float2 screen, SBSDisplayPanelStyle st)
{
    float pitch = max(st.pixelPitch, 1.0);
    float2 cellUV = frac(screen / pitch);
    float aa = max(0.5 / pitch, 1.0e-3);
    float radius = saturate(st.fill) / 6.0;

    half ledR = SBSDisplayPanelCircle(cellUV - float2(1.0 / 6.0, 0.5), radius, aa);
    half ledG = SBSDisplayPanelCircle(cellUV - float2(3.0 / 6.0, 0.5), radius, aa);
    half ledB = SBSDisplayPanelCircle(cellUV - float2(5.0 / 6.0, 0.5), radius, aa);
    half3 emitters = SBSDisplayPanelOrder(half3(ledR, ledG, ledB), st.subpixelOrder);

    half aperture = max(emitters.r, max(emitters.g, emitters.b));
    half3 emitterMask = lerp(half3(aperture, aperture, aperture), emitters * 3.0, saturate(st.subpixel));
    return color * lerp(half3(1.0, 1.0, 1.0), emitterMask, saturate(st.grid));
}

half SBSDisplayPanelWallMask(float2 screen, SBSDisplayPanelStyle st)
{
    if (st.seam <= 0.0)
        return 1.0;

    float pitch = max(st.pixelPitch, 1.0);
    float cells = max(floor(st.tileCells + 0.5), 2.0);
    float period = pitch * cells;
    float2 inTile = frac(screen / period) * period;
    float2 edge = min(inTile, float2(period, period) - inTile);
    float nearestEdge = min(edge.x, edge.y);
    float halfSeam = st.seam * 0.5;
    return smoothstep(halfSeam, halfSeam + 1.0, nearestEdge);
}

half SBSDisplayPanelTileBrightness(float2 screen, SBSDisplayPanelStyle st)
{
    float pitch = max(st.pixelPitch, 1.0);
    float cells = max(floor(st.tileCells + 0.5), 2.0);
    float2 tile = floor(screen / (pitch * cells));

    // タイル番号の線形結合を折り返す。擬似乱数ではなく、隣接タイルの差を
    // 決定的に作るための単純な位相である。
    float phase = frac(tile.x * 0.37 + tile.y * 0.61);
    return 1.0 + (phase * 2.0 - 1.0) * saturate(st.tileVariation);
}

half3 SBSDisplayPanelApply(
    half3 color,
    float2 screen,
    half facing,
    SBSDisplayPanelStyle st)
{
    half amount = saturate(st.amount);
    if (amount <= 0.0)
        return color;

    half3 panel = (st.mode < 0.5)
        ? SBSDisplayPanelLCD(color, screen, st)
        : SBSDisplayPanelLED(color, screen, st);

    if (st.mode >= 1.5)
        panel *= SBSDisplayPanelWallMask(screen, st) * SBSDisplayPanelTileBrightness(screen, st);

    half angleResponse = lerp(1.0, saturate(facing), saturate(st.viewAngle));
    panel *= max(st.brightness, 0.0) * angleResponse;
    return lerp(color, panel, amount);
}

#endif // SABASHADER_DISPLAY_PANEL_CORE_INCLUDED
