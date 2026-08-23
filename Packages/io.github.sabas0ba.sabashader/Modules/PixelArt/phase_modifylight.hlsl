{
    // 塗り分けの入力を升目の中心での値に差し替える。
    //
    // base フェーズの時点では sd.N はまだ接空間で、法線マップが無いと
    // 面の上で一定になってしまい勾配が取れない。全ライトを合成し終えた
    // ここでは sd.N がワールド空間になっているので、升目ごとに一定にできる。
    // 以降の塗り分けはこの法線とライト方向から出るため、帯の境界が升目に乗る。
    SBSPixelStyle pixelLightStyle;
    pixelLightStyle.amount = _Amount;
    pixelLightStyle.levels = _Levels;
    pixelLightStyle.dither = _Dither;
    pixelLightStyle.cellSize = _CellSize;
    pixelLightStyle.palette = _PaletteBlend;

    half2 pixelLightDelta = SBSPixelCellDelta(vertex.positionRaw.xy, pixelLightStyle);

    sd.N = normalize(SBSPixelSnap3(sd.N, pixelLightDelta, _Amount));
    sd.N_detail = normalize(SBSPixelSnap3(sd.N_detail, pixelLightDelta, _Amount));
    sd.L = normalize(SBSPixelSnap3(sd.L, pixelLightDelta, _Amount));
    sd.shadow = SBSPixelSnap1(sd.shadow, pixelLightDelta, _Amount);
}
