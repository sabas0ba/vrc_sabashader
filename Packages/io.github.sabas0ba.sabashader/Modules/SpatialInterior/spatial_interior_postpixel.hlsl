{
    if (_Amount > 0.0)
    {
        SBSSpatialStyle spatialStyle;
        spatialStyle.amount = _Amount;
        spatialStyle.preset = _Preset;
        spatialStyle.side = _Side;
        spatialStyle.region = _Region;
        spatialStyle.colorA = _ColorA.rgb;
        spatialStyle.colorB = _ColorB.rgb;
        spatialStyle.emission = _Emission;
        spatialStyle.scale = _Scale;
        spatialStyle.depth = _Depth;
        spatialStyle.parallax = _Parallax;
        spatialStyle.starDensity = _StarDensity;
        spatialStyle.starSize = _StarSize;
        spatialStyle.nebula = _Nebula;
        spatialStyle.nebulaScale = _NebulaScale;
        spatialStyle.time = SCTime() * _TimeScale;
        spatialStyle.riftCenter = _RiftCenter.xy;
        spatialStyle.riftSize = _RiftSize.xy;
        spatialStyle.riftNoise = _RiftNoise;
        spatialStyle.edgeWidth = _EdgeWidth;
        spatialStyle.edgeColor = _EdgeColor.rgb * _EdgeColor.a;
        spatialStyle.additivePass = 0.0;

        #ifdef UNITY_PASS_FORWARDADD
            spatialStyle.additivePass = 1.0;
        #endif

        half3 spatialObjectPosition = mul(unity_WorldToObject, float4(vertex.position, 1.0)).xyz;
        half3 spatialObjectView = normalize(mul((half3x3)unity_WorldToObject, vertex.V));
        // 一部のplatformではSV_IsFrontFaceと動的Cullの組み合わせが安定しないため、
        // 補間前のworld normalとview directionから表裏を判定する。
        half spatialIsFront = step(0.0, dot(normalize(vertex.N), normalize(vertex.V)));
        half spatialMask = (_MaskChannel < 4) ? sd.mask[_MaskChannel] : 1.0;
        sd.col.rgb = SBSSpatialApply(
            sd.col.rgb,
            spatialObjectPosition,
            spatialObjectView,
            sd.uv,
            spatialIsFront,
            spatialMask,
            spatialStyle);
    }
}
