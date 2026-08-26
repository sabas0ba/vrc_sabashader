{
    if (_Amount > 0.0)
    {
        SBSDisplayPanelStyle displayPanelStyle;
        displayPanelStyle.amount = _Amount;
        displayPanelStyle.mode = _Mode;
        displayPanelStyle.pixelPitch = _PixelPitch;
        displayPanelStyle.fill = _Fill;
        displayPanelStyle.grid = _Grid;
        displayPanelStyle.subpixel = _Subpixel;
        displayPanelStyle.subpixelOrder = _SubpixelOrder;
        displayPanelStyle.brightness = _Brightness;
        displayPanelStyle.viewAngle = _ViewAngle;
        displayPanelStyle.tileCells = _TileCells;
        displayPanelStyle.seam = _Seam;
        displayPanelStyle.tileVariation = _TileVariation;

        half displayPanelFacing = abs(dot(normalize(sd.N_detail), normalize(vertex.V)));
        sd.col.rgb = SBSDisplayPanelApply(
            sd.col.rgb,
            vertex.positionRaw.xy,
            displayPanelFacing,
            displayPanelStyle);
    }
}
