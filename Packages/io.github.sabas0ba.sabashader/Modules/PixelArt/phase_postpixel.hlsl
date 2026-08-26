{
    if (_Amount > 0.0)
    {
        // 本体が描き終えた色に対してかける。隣接ピクセルは読めないので、
        // 画面を実際に間引くことはできない。升目は整列ディザの粒度を指す。
        SBSPixelStyle pixelStyle;
        pixelStyle.amount = _Amount;
        pixelStyle.levels = _Levels;
        pixelStyle.dither = _Dither;
        pixelStyle.cellSize = _CellSize;
        pixelStyle.palette = _PaletteBlend;
        pixelStyle.preset = _PalettePreset;

        half pixelThreshold = SBSPixelThreshold(vertex.positionRaw.xy, pixelStyle);
        half3 pixelQuantized = SBSPixelQuantize(sd.col.rgb, pixelThreshold, pixelStyle);

        half pixelCoord = SBSPixelPaletteCoord(sd.col.rgb, pixelThreshold, pixelStyle);

        // 番号を選んでいれば組み込みパレット、0 ならテクスチャを引く。
        half3 pixelPalette = (_PalettePreset < 1)
            ? SCSampleClamp(_Palette, float2(pixelCoord, 0.5)).rgb
            : SBSPixelPalettePreset(sd.col.rgb, pixelCoord, pixelStyle);

        sd.col.rgb = SBSPixelApply(sd.col.rgb, pixelQuantized, pixelPalette, pixelStyle);
    }
}
