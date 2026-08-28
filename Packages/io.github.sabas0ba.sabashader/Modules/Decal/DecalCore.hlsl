#ifndef SABASHADER_DECAL_CORE_INCLUDED
#define SABASHADER_DECAL_CORE_INCLUDED

// UV または object-space projector から decal の UV と被覆率を作り、
// 入力画像を albedo へ合成する数式。行列と texture sampling は phase 側に置く。
struct SBSDecalStyle
{
    half amount;
    half mapping;
    half blendMode;
    half4 tint;
    half3 projectorCenter;
    half3 projectorRotation;
    half3 projectorSize;
    half angleFade;
    half edgeSoftness;
};

half3 SBSDecalRotateX(half3 p, half angle)
{
    half c = cos(angle);
    half s = sin(angle);
    return half3(p.x, p.y * c - p.z * s, p.y * s + p.z * c);
}

half3 SBSDecalRotateY(half3 p, half angle)
{
    half c = cos(angle);
    half s = sin(angle);
    return half3(p.x * c + p.z * s, p.y, -p.x * s + p.z * c);
}

half3 SBSDecalRotateZ(half3 p, half angle)
{
    half c = cos(angle);
    half s = sin(angle);
    return half3(p.x * c - p.y * s, p.x * s + p.y * c, p.z);
}

half3 SBSDecalRotate(half3 p, half3 degrees)
{
    half3 radiansValue = degrees * 0.0174532925;
    p = SBSDecalRotateX(p, radiansValue.x);
    p = SBSDecalRotateY(p, radiansValue.y);
    return SBSDecalRotateZ(p, radiansValue.z);
}

half3 SBSDecalInverseRotate(half3 p, half3 degrees)
{
    half3 radiansValue = degrees * -0.0174532925;
    p = SBSDecalRotateZ(p, radiansValue.z);
    p = SBSDecalRotateY(p, radiansValue.y);
    return SBSDecalRotateX(p, radiansValue.x);
}

half4 SBSDecalProjection(half2 uv, half3 objectPosition, half3 objectNormal, SBSDecalStyle st)
{
    if (st.mapping < 0.5)
        return half4(uv.x, uv.y, 1.0, 0.0);

    half3 size = max(abs(st.projectorSize), half3(1.0e-4, 1.0e-4, 1.0e-4));
    half3 local = SBSDecalInverseRotate(objectPosition - st.projectorCenter, st.projectorRotation);
    half2 projectedUV = local.xy / size.xy + half2(0.5, 0.5);

    half softness = max(st.edgeSoftness, 1.0e-4);
    half2 edgeDistance = half2(0.5, 0.5) - abs(projectedUV - half2(0.5, 0.5));
    half edgeCoverage = saturate(min(edgeDistance.x, edgeDistance.y) / softness);

    half halfDepth = size.z * 0.5;
    half depthCoverage = saturate((halfDepth - abs(local.z)) / max(halfDepth * softness, 1.0e-4));

    half3 forward = SBSDecalRotate(half3(0.0, 0.0, 1.0), st.projectorRotation);
    half facing = dot(normalize(objectNormal), -normalize(forward));
    half angleEnd = min(st.angleFade + 0.2, 1.0);
    half angleCoverage = smoothstep(st.angleFade, max(angleEnd, st.angleFade + 1.0e-4), facing);

    return half4(projectedUV.x, projectedUV.y, edgeCoverage * depthCoverage * angleCoverage, local.z);
}

half SBSDecalOpacity(half decalAlpha, half coverage, half mask, SBSDecalStyle st)
{
    return saturate(st.amount) * saturate(st.tint.a) * saturate(decalAlpha) * saturate(coverage) * saturate(mask);
}

half3 SBSDecalApply(half3 base, half4 decal, half coverage, half mask, SBSDecalStyle st)
{
    half opacity = SBSDecalOpacity(decal.a, coverage, mask, st);
    half3 source = decal.rgb * st.tint.rgb;

    if (st.blendMode < 0.5)
        return lerp(base, source, opacity);
    if (st.blendMode < 1.5)
        return lerp(base, base * source, opacity);
    return base + source * opacity;
}

#endif // SABASHADER_DECAL_CORE_INCLUDED
