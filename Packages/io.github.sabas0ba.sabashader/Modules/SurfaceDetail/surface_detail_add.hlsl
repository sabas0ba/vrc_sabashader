{
    if (_Amount > 0.0 && _Sheen > 0.0)
    {
        SBSSurfaceDetailStyle detailStyle;
        detailStyle.amount = _Amount;
        detailStyle.mode = _Mode;
        detailStyle.scale = _Scale;
        detailStyle.textureStrength = _TextureStrength;
        detailStyle.albedoVariation = _AlbedoVariation;
        detailStyle.normalStrength = _NormalStrength;
        detailStyle.roughnessVariation = _RoughnessVariation;
        detailStyle.pore = _Pore;
        detailStyle.weave = _Weave;
        detailStyle.sheen = _Sheen;
        detailStyle.sheenColor = _SheenColor.rgb;

        half detailMask = (_MaskChannel < 4) ? sd.mask[_MaskChannel] : 1.0;
        half2 detailUV = sd.uv * _Texture_ST.xy + _Texture_ST.zw;
        sd.add += SBSSurfaceDetailSpecular(
            surf.N,
            surf.L,
            surf.V,
            detailUV,
            detailMask,
            detailStyle);
    }
}
