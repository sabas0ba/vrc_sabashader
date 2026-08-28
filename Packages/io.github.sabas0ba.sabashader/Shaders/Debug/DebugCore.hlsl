#ifndef SABASHADER_DEBUG_CORE_INCLUDED
#define SABASHADER_DEBUG_CORE_INCLUDED

half3 SBSDebugSignedVector(half3 value)
{
    return saturate(value * 0.5 + 0.5);
}

half3 SBSDebugUV(float2 uv, float scale)
{
    float2 wrapped = frac(uv * scale);
    return half3(wrapped.x, wrapped.y, 0.0);
}

half3 SBSDebugPosition(float3 position, float scale)
{
    return frac(position * scale);
}

half3 SBSDebugScalar(half value)
{
    return half3(value, value, value);
}

half3 SBSDebugHdrColor(half3 value)
{
    half3 positive = max(value, half3(0.0, 0.0, 0.0));
    return positive / (positive + half3(1.0, 1.0, 1.0));
}

half SBSDebugWireMask(float3 barycentric, float3 derivatives, half width)
{
    float3 edge = smoothstep(
        float3(0.0, 0.0, 0.0),
        max(derivatives * width, float3(1.0e-5, 1.0e-5, 1.0e-5)),
        barycentric);
    return 1.0 - min(edge.x, min(edge.y, edge.z));
}

#endif // SABASHADER_DEBUG_CORE_INCLUDED
