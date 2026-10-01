#ifndef SABASHADER_MOCHISKIN_CORE_INCLUDED
#define SABASHADER_MOCHISKIN_CORE_INCLUDED

// 接触輪郭をcompact supportの高さ場にし、範囲外の頂点と法線を変更しない。
half SBSMochiSmooth01(half value)
{
    half t = saturate(value);
    return t * t * (3.0 - 2.0 * t);
}

half SBSMochiActivatedPressure(half pressure, half threshold, half softness)
{
    half start = saturate(threshold);
    half normalized = saturate((saturate(pressure) - start) / max(1.0 - start, 1.0e-4));
    return lerp(normalized, SBSMochiSmooth01(normalized), saturate(softness));
}

float SBSMochiShapeRadius(
    float2 uv,
    float4 contactPoint,
    float4 contactShape,
    half irregularity,
    half irregularityScale)
{
    float2 pointRadius = max(abs(contactPoint.zw), float2(1.0e-4, 1.0e-4));
    float2 local = uv - contactPoint.xy;
    float angle = contactShape.x * 0.01745329252;
    float sine = sin(angle);
    float cosine = cos(angle);
    local = float2(
        cosine * local.x + sine * local.y,
        -sine * local.x + cosine * local.y);
    local /= pointRadius;

    float exponent = clamp(abs(contactShape.y), 1.0, 16.0);
    float shaped = pow(
        pow(abs(local.x), exponent) + pow(abs(local.y), exponent),
        1.0 / exponent);

    // 異なる方向の低周波を合成し、円形・矩形の規則的な輪郭だけを崩す。
    float scale = max(irregularityScale, 1.0);
    float seed = contactShape.z * 19.19 + 0.37;
    float wave = sin((local.x * 1.37 + local.y * 0.73) * scale + seed) * 0.62;
    wave += sin((local.x * -0.61 + local.y * 1.71) * scale * 1.63 - seed * 1.47) * 0.38;
    float contour = max(1.0 + wave * saturate(irregularity), 0.55);
    return shaped / contour;
}

half SBSMochiProfile(
    half normalizedRadius,
    half bulge,
    half indentSpread,
    half edgeSoftness)
{
    half radius = max(normalizedRadius, 0.0);
    if (radius >= 1.0)
        return 0.0;

    half spread = clamp(indentSpread, 0.15, 0.85);
    half softness = saturate(edgeSoftness);
    half transition = lerp(0.025, 0.22, softness);

    half indentation = 1.0 - SBSMochiSmooth01(saturate(radius / spread));
    half rimStart = max(spread - transition, 0.02);
    half rimRange = max(1.0 - rimStart, 1.0e-4);
    half rimT = saturate((radius - rimStart) / rimRange);
    half rim = 4.0 * rimT * (1.0 - rimT);
    half rimGate = SBSMochiSmooth01(saturate((radius - rimStart) / max(transition * 2.0, 1.0e-4)));
    half edgeStart = lerp(0.94, 0.72, softness);
    half edgeFade = 1.0 - SBSMochiSmooth01(saturate((radius - edgeStart) / max(1.0 - edgeStart, 1.0e-4)));

    return -indentation + saturate(bulge) * rim * rimGate * edgeFade;
}

half SBSMochiPointHeight(
    float2 uv,
    float4 contactPoint,
    float4 contactShape,
    half pressure,
    half depth,
    half bulge,
    half indentSpread,
    half edgeSoftness,
    half irregularity,
    half irregularityScale,
    half contactThreshold,
    half contactSoftness)
{
    float normalizedRadius = SBSMochiShapeRadius(
        uv, contactPoint, contactShape, irregularity, irregularityScale);
    half profile = SBSMochiProfile(
        half(normalizedRadius), bulge, indentSpread, edgeSoftness);
    half activePressure = SBSMochiActivatedPressure(pressure, contactThreshold, contactSoftness);
    return profile * activePressure * max(depth, 0.0);
}

float2 SBSMochiPointGradient(
    float2 uv,
    float4 contactPoint,
    float4 contactShape,
    half pressure,
    half depth,
    half bulge,
    half indentSpread,
    half edgeSoftness,
    half irregularity,
    half irregularityScale,
    half contactThreshold,
    half contactSoftness)
{
    float epsilon = 7.5e-4;
    half left = SBSMochiPointHeight(
        uv - float2(epsilon, 0.0), contactPoint, contactShape, pressure, depth, bulge,
        indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    half right = SBSMochiPointHeight(
        uv + float2(epsilon, 0.0), contactPoint, contactShape, pressure, depth, bulge,
        indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    half down = SBSMochiPointHeight(
        uv - float2(0.0, epsilon), contactPoint, contactShape, pressure, depth, bulge,
        indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    half up = SBSMochiPointHeight(
        uv + float2(0.0, epsilon), contactPoint, contactShape, pressure, depth, bulge,
        indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    return float2(right - left, up - down) / (2.0 * epsilon);
}

half SBSMochiHeight4(
    float2 uv,
    float4 point0,
    float4 point1,
    float4 point2,
    float4 point3,
    float4 shape0,
    float4 shape1,
    float4 shape2,
    float4 shape3,
    half4 pressure,
    half depth,
    half bulge,
    half indentSpread,
    half edgeSoftness,
    half irregularity,
    half irregularityScale,
    half contactThreshold,
    half contactSoftness)
{
    half height = SBSMochiPointHeight(uv, point0, shape0, pressure.x, depth, bulge, indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    height += SBSMochiPointHeight(uv, point1, shape1, pressure.y, depth, bulge, indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    height += SBSMochiPointHeight(uv, point2, shape2, pressure.z, depth, bulge, indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    height += SBSMochiPointHeight(uv, point3, shape3, pressure.w, depth, bulge, indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    return height;
}

float2 SBSMochiGradient4(
    float2 uv,
    float4 point0,
    float4 point1,
    float4 point2,
    float4 point3,
    float4 shape0,
    float4 shape1,
    float4 shape2,
    float4 shape3,
    half4 pressure,
    half depth,
    half bulge,
    half indentSpread,
    half edgeSoftness,
    half irregularity,
    half irregularityScale,
    half contactThreshold,
    half contactSoftness)
{
    float2 gradient = SBSMochiPointGradient(uv, point0, shape0, pressure.x, depth, bulge, indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    gradient += SBSMochiPointGradient(uv, point1, shape1, pressure.y, depth, bulge, indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    gradient += SBSMochiPointGradient(uv, point2, shape2, pressure.z, depth, bulge, indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    gradient += SBSMochiPointGradient(uv, point3, shape3, pressure.w, depth, bulge, indentSpread, edgeSoftness, irregularity, irregularityScale, contactThreshold, contactSoftness);
    return gradient;
}

half3 SBSMochiApplyNormal(half3 normal, float2 heightGradient, half strength)
{
    half2 slope = half2(heightGradient) * max(strength, 0.0);
    return normalize(half3(normal.xy - slope, max(normal.z, 1.0e-4)));
}

#endif // SABASHADER_MOCHISKIN_CORE_INCLUDED
