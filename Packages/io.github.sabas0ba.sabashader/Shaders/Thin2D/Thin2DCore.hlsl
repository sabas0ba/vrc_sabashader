#ifndef SABASHADER_THIN2D_CORE_INCLUDED
#define SABASHADER_THIN2D_CORE_INCLUDED

half SBSThinLuminance(half3 color)
{
    return dot(color, half3(0.2126, 0.7152, 0.0722));
}

half SBSFresnel(half3 N, half3 V, half power)
{
    half facing = saturate(dot(normalize(N), normalize(V)));
    return pow(saturate(1.0 - facing), max(power, 0.001));
}

half SBSBlinnSpecular(half3 N, half3 L, half3 V, half power)
{
    half3 H = normalize(normalize(L) + normalize(V));
    return pow(saturate(dot(normalize(N), H)), max(power, 1.0));
}

half SBSPaperGrain(float2 uv)
{
    float2 cell = floor(uv);
    float value = sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453;
    return frac(value) * 2.0 - 1.0;
}

half SBSAlphaContourMask(half centerAlpha, half4 adjacentAlpha)
{
    half neighboringAlpha = min(min(adjacentAlpha.x, adjacentAlpha.y), min(adjacentAlpha.z, adjacentAlpha.w));
    return saturate(centerAlpha - neighboringAlpha) * step(1.0e-4, centerAlpha);
}

half SBSFlatDiffuse(half3 N, half3 L, half flatness, half threshold)
{
    half diffuse = saturate(dot(normalize(N), normalize(L)));
    half band = smoothstep(threshold - 0.025, threshold + 0.025, diffuse);
    return lerp(diffuse, band, flatness);
}

#endif // SABASHADER_THIN2D_CORE_INCLUDED
