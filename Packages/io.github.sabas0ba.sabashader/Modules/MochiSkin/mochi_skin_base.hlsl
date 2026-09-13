{
    if (_Amount > 0.0 && _Depth > 0.0 && _NormalStrength > 0.0)
    {
        float2 mochiUV = vertex.uv[0].xy;
        if (_UVChannel == 1) mochiUV = vertex.uv[1].xy;
        if (_UVChannel == 2) mochiUV = vertex.uv[2].xy;
        if (_UVChannel == 3) mochiUV = vertex.uv[3].xy;

        half4 mochiPressure = half4(_Pressure0, _Pressure1, _Pressure2, _Pressure3);
        float2 mochiGradient = SBSMochiGradient4(
            mochiUV,
            _Point0,
            _Point1,
            _Point2,
            _Point3,
            _Shape0,
            _Shape1,
            _Shape2,
            _Shape3,
            mochiPressure,
            _Depth,
            _Bulge,
            _IndentSpread,
            _EdgeSoftness,
            _Irregularity,
            _IrregularityScale,
            _ContactThreshold,
            _ContactSoftness);
        // Differentiate height * compliance, including mask boundaries.
        half mochiHeight = SBSMochiHeight4(
            mochiUV, _Point0, _Point1, _Point2, _Point3,
            _Shape0, _Shape1, _Shape2, _Shape3, mochiPressure,
            _Depth, _Bulge, _IndentSpread, _EdgeSoftness, _Irregularity,
            _IrregularityScale, _ContactThreshold, _ContactSoftness);
        float mochiEpsilon = 7.5e-4;
        half mochiMask = saturate(_ComplianceMask.SampleLevel(sampler_linear_clamp, mochiUV, 0).r);
        float2 mochiMaskGradient = float2(
            _ComplianceMask.SampleLevel(sampler_linear_clamp, mochiUV + float2(mochiEpsilon, 0), 0).r -
            _ComplianceMask.SampleLevel(sampler_linear_clamp, mochiUV - float2(mochiEpsilon, 0), 0).r,
            _ComplianceMask.SampleLevel(sampler_linear_clamp, mochiUV + float2(0, mochiEpsilon), 0).r -
            _ComplianceMask.SampleLevel(sampler_linear_clamp, mochiUV - float2(0, mochiEpsilon), 0).r
        ) / (2.0 * mochiEpsilon);
        float mochiBone = _UseBoneCompliance != 0 ? saturate(vertex.uv[3].x) : 1.0;
        float2 mochiBoneGradient = 0.0;
        float2 mochiDx = ddx(mochiUV);
        float2 mochiDy = ddy(mochiUV);
        float mochiDet = mochiDx.x * mochiDy.y - mochiDx.y * mochiDy.x;
        if (abs(mochiDet) > 1.0e-12)
        {
            mochiBoneGradient = float2(
                ddx(mochiBone) * mochiDy.y - ddy(mochiBone) * mochiDx.y,
                ddy(mochiBone) * mochiDx.x - ddx(mochiBone) * mochiDy.x) / mochiDet;
        }
        mochiGradient = (mochiGradient * mochiMask * mochiBone +
            mochiHeight * (mochiMaskGradient * mochiBone + mochiMask * mochiBoneGradient)) * saturate(_Compliance);
        half mochiNormalAmount = saturate(_Amount) * _NormalStrength;
        sd.N = SBSMochiApplyNormal(sd.N, mochiGradient, mochiNormalAmount);
        sd.N_detail = SBSMochiApplyNormal(sd.N_detail, mochiGradient, mochiNormalAmount);
    }
}
