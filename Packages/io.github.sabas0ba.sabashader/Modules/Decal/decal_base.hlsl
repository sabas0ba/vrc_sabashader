{
    if (_Amount > 0.0)
    {
        SBSDecalStyle decalStyle;
        decalStyle.amount = _Amount;
        decalStyle.mapping = _Mapping;
        decalStyle.blendMode = _BlendMode;
        decalStyle.tint = _Tint;
        decalStyle.projectorCenter = _ProjectorCenter.xyz;
        decalStyle.projectorRotation = _ProjectorRotation.xyz;
        decalStyle.projectorSize = _ProjectorSize.xyz;
        decalStyle.angleFade = _AngleFade;
        decalStyle.edgeSoftness = _EdgeSoftness;

        half2 decalUV = vertex.uv[0].xy;
        if (_UVChannel == 1) decalUV = vertex.uv[1].xy;
        if (_UVChannel == 2) decalUV = vertex.uv[2].xy;
        if (_UVChannel == 3) decalUV = vertex.uv[3].xy;

        half3 decalObjectPosition = mul(unity_WorldToObject, float4(vertex.position, 1.0)).xyz;
        half3 decalObjectNormal = normalize(mul(vertex.N, (half3x3)unity_ObjectToWorld));
        half4 decalProjection = SBSDecalProjection(
            decalUV,
            decalObjectPosition,
            decalObjectNormal,
            decalStyle);

        half2 decalSampleUV = decalProjection.xy * _Texture_ST.xy + _Texture_ST.zw;
        half4 decalSample = SCSampleClamp(_Texture, decalSampleUV);
        half decalMask = (_MaskChannel < 4) ? sd.mask[_MaskChannel] : 1.0;
        sd.albedoAlpha.rgb = SBSDecalApply(
            sd.albedoAlpha.rgb,
            decalSample,
            decalProjection.z,
            decalMask,
            decalStyle);
    }
}
