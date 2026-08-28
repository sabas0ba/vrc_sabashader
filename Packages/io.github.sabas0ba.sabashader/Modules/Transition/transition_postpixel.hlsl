{
    #ifndef UNITY_PASS_FORWARDADD
        SBSTransitionStyle transitionStyle;
        transitionStyle.progress = _Progress;
        transitionStyle.mode = _Mode;
        transitionStyle.direction = _Direction.xyz;
        if (length(transitionStyle.direction) < 1.0e-4)
            transitionStyle.direction = half3(0.0, 1.0, 0.0);
        transitionStyle.boundsMin = min(_Bounds.x, _Bounds.y);
        transitionStyle.boundsMax = max(_Bounds.x, _Bounds.y);
        transitionStyle.noiseScale = _NoiseScale;
        transitionStyle.noiseAmount = _Noise;
        transitionStyle.edgeWidth = _EdgeWidth;
        transitionStyle.edgeColor = _EdgeColor.rgb * _EdgeColor.a * _EdgeEmission;
        transitionStyle.displacement = _Displacement;
        transitionStyle.blockScale = _BlockScale;
        transitionStyle.liquidAmplitude = _LiquidAmplitude;
        transitionStyle.liquidFrequency = _LiquidFrequency;
        transitionStyle.liquidSpeed = _LiquidSpeed;
        transitionStyle.liquidTint = _LiquidTint;
        transitionStyle.time = SCTime();

        half3 transitionObjectPosition = mul(unity_WorldToObject, float4(vertex.position, 1.0)).xyz;
        sd.col.rgb = SBSTransitionEdgeColor(sd.col.rgb, transitionObjectPosition, transitionStyle);
    #endif
}
