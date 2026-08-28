{
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
    transitionStyle.liquidWobble = _LiquidWobble;
    transitionStyle.liquidPuddle = _LiquidPuddle;
    transitionStyle.liquidPuddleHeight = _LiquidPuddleHeight;
    transitionStyle.liquidPuddleSpread = _LiquidPuddleSpread;
    transitionStyle.liquidTint = _LiquidTint;
    transitionStyle.time = SCTime();

    if (transitionStyle.progress < 1.0 || transitionStyle.mode > 1.5)
    {
        half3 transitionObjectPosition = mul(unity_WorldToObject, float4(vertex.position, 1.0)).xyz;
        half3 transitionObjectNormal = normalize(mul(vertex.N, (half3x3)unity_ObjectToWorld));
        half3 transitionObjectOffset = SBSTransitionMorphOffset(
            transitionObjectPosition,
            transitionObjectNormal,
            transitionStyle);
        vertex.position += mul((half3x3)unity_ObjectToWorld, transitionObjectOffset);
    }
}
