#ifndef SABASHADER_MOCHISKIN_CORE_INCLUDED
#define SABASHADER_MOCHISKIN_CORE_INCLUDED

// UV上の楕円を、中央の凹み、外周の小さな盛り上がり、無変位の順でつなぐ。
// 各区間の端で勾配を0にし、頂点変位とdetail normalの継ぎ目を一致させる。
half SBSMochiSmooth01(half value)
{
    half t = saturate(value);
    return t * t * (3.0 - 2.0 * t);
}

half SBSMochiSmooth01Derivative(half value)
{
    half t = saturate(value);
    return 6.0 * t * (1.0 - t);
}

// xは高さ係数、yは正規化半径に対する高さ係数の微分。
// 高さ係数は中央で-1、外周で0になる。
half2 SBSMochiProfile(half normalizedRadius, half bulge)
{
    half radius = max(normalizedRadius, 0.0);
    half shoulder = saturate(bulge);

    if (radius < 0.55)
    {
        half t = radius / 0.55;
        return half2(
            -1.0 + SBSMochiSmooth01(t),
            SBSMochiSmooth01Derivative(t) / 0.55);
    }

    if (radius < 0.75)
    {
        half t = (radius - 0.55) / 0.20;
        return half2(
            shoulder * SBSMochiSmooth01(t),
            shoulder * SBSMochiSmooth01Derivative(t) / 0.20);
    }

    if (radius < 1.0)
    {
        half t = (radius - 0.75) / 0.25;
        return half2(
            shoulder * (1.0 - SBSMochiSmooth01(t)),
            -shoulder * SBSMochiSmooth01Derivative(t) / 0.25);
    }

    return half2(0.0, 0.0);
}

half SBSMochiPointHeight(
    float2 uv,
    float4 contactPoint,
    half pressure,
    half depth,
    half bulge)
{
    float2 radius = max(abs(contactPoint.zw), float2(1.0e-4, 1.0e-4));
    float normalizedRadius = length((uv - contactPoint.xy) / radius);
    half2 profile = SBSMochiProfile(half(normalizedRadius), bulge);
    return profile.x * saturate(pressure) * max(depth, 0.0);
}

float2 SBSMochiPointGradient(
    float2 uv,
    float4 contactPoint,
    half pressure,
    half depth,
    half bulge)
{
    float2 radius = max(abs(contactPoint.zw), float2(1.0e-4, 1.0e-4));
    float2 ellipse = (uv - contactPoint.xy) / radius;
    float normalizedRadius = length(ellipse);
    if (normalizedRadius <= 1.0e-5 || normalizedRadius >= 1.0)
        return float2(0.0, 0.0);

    half2 profile = SBSMochiProfile(half(normalizedRadius), bulge);
    float2 radiusGradient = ellipse / (normalizedRadius * radius);
    return radiusGradient * profile.y * saturate(pressure) * max(depth, 0.0);
}

half SBSMochiHeight4(
    float2 uv,
    float4 point0,
    float4 point1,
    float4 point2,
    float4 point3,
    half4 pressure,
    half depth,
    half bulge)
{
    half height = SBSMochiPointHeight(uv, point0, pressure.x, depth, bulge);
    height += SBSMochiPointHeight(uv, point1, pressure.y, depth, bulge);
    height += SBSMochiPointHeight(uv, point2, pressure.z, depth, bulge);
    height += SBSMochiPointHeight(uv, point3, pressure.w, depth, bulge);
    return height;
}

float2 SBSMochiGradient4(
    float2 uv,
    float4 point0,
    float4 point1,
    float4 point2,
    float4 point3,
    half4 pressure,
    half depth,
    half bulge)
{
    float2 gradient = SBSMochiPointGradient(uv, point0, pressure.x, depth, bulge);
    gradient += SBSMochiPointGradient(uv, point1, pressure.y, depth, bulge);
    gradient += SBSMochiPointGradient(uv, point2, pressure.z, depth, bulge);
    gradient += SBSMochiPointGradient(uv, point3, pressure.w, depth, bulge);
    return gradient;
}

half3 SBSMochiApplyNormal(half3 normal, float2 heightGradient, half strength)
{
    half2 slope = half2(heightGradient) * max(strength, 0.0);
    return normalize(half3(normal.xy - slope, max(normal.z, 1.0e-4)));
}

#endif // SABASHADER_MOCHISKIN_CORE_INCLUDED
