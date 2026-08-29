{
    SBSBankStyle bankStyle;
    bankStyle.progress = _Progress;
    bankStyle.visibilityProgress = SBSBankRoleProgress(
        _Progress,
        _Role,
        _IncomingOutgoingWindow,
        _CoverWindow);
    bankStyle.role = _Role;
    bankStyle.style = _Style;
    bankStyle.direction = _Direction.xyz;
    if (length(bankStyle.direction) < 1.0e-4)
        bankStyle.direction = half3(0.0, 1.0, 0.0);
    bankStyle.boundsMin = min(_Bounds.x, _Bounds.y);
    bankStyle.boundsMax = max(_Bounds.x, _Bounds.y);
    bankStyle.noiseScale = _NoiseScale;
    bankStyle.noiseAmount = _Noise;
    bankStyle.edgeWidth = _EdgeWidth;
    bankStyle.edgeColor = _EdgeColor.rgb * _EdgeColor.a * _EdgeEmission;
    bankStyle.displacement = _Displacement;
    bankStyle.blockScale = _BlockScale;
    bankStyle.coverColor = _CoverColor;
    bankStyle.patternColor = _PatternColor;
    bankStyle.patternScale = _PatternScale;
    bankStyle.patternSpeed = _PatternSpeed;
    bankStyle.patternEmission = _PatternEmission;
    bankStyle.time = SCTime();

    half3 bankObjectPosition = mul(unity_WorldToObject, float4(vertex.position, 1.0)).xyz;
    clip(SBSBankVisibility(bankObjectPosition, bankStyle) - 0.5);

    if (bankStyle.role > 1.5)
    {
        sd.albedoAlpha.rgb = bankStyle.coverColor.rgb;
        sd.albedoAlpha.a = 1.0;
    }
}
