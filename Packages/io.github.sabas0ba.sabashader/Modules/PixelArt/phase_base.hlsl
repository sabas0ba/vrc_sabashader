{
    // ベースカラーを升目の中心での値に差し替える。
    // テクスチャを貼っている場合はここで模様が升目状になる。
    SBSPixelStyle pixelBaseStyle;
    pixelBaseStyle.amount = _Amount;
    pixelBaseStyle.levels = _Levels;
    pixelBaseStyle.dither = _Dither;
    pixelBaseStyle.cellSize = _CellSize;
    pixelBaseStyle.palette = _PaletteBlend;
    pixelBaseStyle.preset = _PalettePreset;

    half2 pixelBaseDelta = SBSPixelCellDelta(vertex.positionRaw.xy, pixelBaseStyle);

    sd.albedoAlpha.rgb = SBSPixelSnap3(sd.albedoAlpha.rgb, pixelBaseDelta, _Amount);
    sd.uv = SBSPixelSnap2(sd.uv, pixelBaseDelta, _Amount);
}
