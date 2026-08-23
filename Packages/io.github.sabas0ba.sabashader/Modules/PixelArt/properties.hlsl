SC_Foldout(__PixelArt)
SC_float(_Amount, 0, [SCRange(0,1)], "__Amount", "__AmountDesc")
SC_float(_Levels, 6, [SCRangeInt(2,32)], "__Levels", "__LevelsDesc")
SC_float(_CellSize, 4, [SCRange(1,32)], "__CellSize", "__CellSizeDesc")
SC_float(_Dither, 1, [SCRange(0,1)], "__Dither", "__DitherDesc")
SC_Texture2D(_Palette, "white", [], "__Palette", "__PaletteDesc")
SC_float(_PaletteBlend, 0, [SCRange(0,1)], "__PaletteBlend", "")
SC_FoldoutEnd
