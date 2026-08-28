{
    if (_Amount > 0.0)
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
        half4 detailSample = SCSampleRepeat(_Texture, detailUV);
        half3 detailNormal = SBSSurfaceDetailNormal(detailUV, detailStyle);
        half2 detailTextureNormal = detailSample.rg * 2.0 - half2(1.0, 1.0);

        sd.albedoAlpha.rgb = SBSSurfaceDetailAlbedo(
            sd.albedoAlpha.rgb,
            detailUV,
            detailSample.rgb,
            detailMask,
            detailStyle);
        sd.N = SBSSurfaceDetailBlendNormal(sd.N, detailNormal, detailTextureNormal, detailMask, detailStyle);
        sd.N_detail = SBSSurfaceDetailBlendNormal(
            sd.N_detail,
            detailNormal,
            detailTextureNormal,
            detailMask,
            detailStyle);
        sd.roughness = SBSSurfaceDetailRoughness(sd.roughness, detailUV, detailMask, detailStyle);
    }
}
