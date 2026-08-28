#ifndef SABASHADER_SPATIALINTERIOR_CORE_INCLUDED
#define SABASHADER_SPATIALINTERIOR_CORE_INCLUDED

// mesh表面を窓として、object-space位置と視線からparallax付きの星・nebulaを
// 手続き生成する。隣接pixelやGrabPassを必要としないためavatarにも使用できる。
struct SBSSpatialStyle
{
    half amount;
    half preset;
    half side;
    half region;
    half3 colorA;
    half3 colorB;
    half emission;
    half scale;
    half depth;
    half parallax;
    half starDensity;
    half starSize;
    half nebula;
    half nebulaScale;
    half time;
    half2 riftCenter;
    half2 riftSize;
    half riftNoise;
    half edgeWidth;
    half3 edgeColor;
    half additivePass;
};

half SBSSpatialHash3(half3 p)
{
    half3 q = frac(half3(
        p.x * 0.1031 + p.y * 0.11369 + p.z * 0.13787,
        p.x * 0.1099 + p.y * 0.12317 + p.z * 0.09991,
        p.x * 0.0973 + p.y * 0.13121 + p.z * 0.11939));
    q = frac(q * (q.yzx + half3(31.32, 31.32, 31.32)));
    return frac((q.x + q.y) * q.z);
}

half SBSSpatialNoise3(half3 p)
{
    half3 cell = floor(p);
    half3 local = frac(p);
    half3 curve = local * local * (half3(3.0, 3.0, 3.0) - local * 2.0);

    half n000 = SBSSpatialHash3(cell);
    half n100 = SBSSpatialHash3(cell + half3(1.0, 0.0, 0.0));
    half n010 = SBSSpatialHash3(cell + half3(0.0, 1.0, 0.0));
    half n110 = SBSSpatialHash3(cell + half3(1.0, 1.0, 0.0));
    half n001 = SBSSpatialHash3(cell + half3(0.0, 0.0, 1.0));
    half n101 = SBSSpatialHash3(cell + half3(1.0, 0.0, 1.0));
    half n011 = SBSSpatialHash3(cell + half3(0.0, 1.0, 1.0));
    half n111 = SBSSpatialHash3(cell + half3(1.0, 1.0, 1.0));

    half low = lerp(lerp(n000, n100, curve.x), lerp(n010, n110, curve.x), curve.y);
    half high = lerp(lerp(n001, n101, curve.x), lerp(n011, n111, curve.x), curve.y);
    return lerp(low, high, curve.z);
}

half SBSSpatialSideMask(half isFront, half side)
{
    if (side < 0.5) return saturate(isFront);
    if (side < 1.5) return 1.0 - saturate(isFront);
    return 1.0;
}

half2 SBSSpatialRegion(half2 uv, SBSSpatialStyle st)
{
    if (st.region < 0.5)
        return half2(1.0, 0.0);

    half2 size = max(abs(st.riftSize), half2(1.0e-4, 1.0e-4));
    half2 p = (uv - st.riftCenter) / (size * 0.5);
    half boundaryNoise = SBSSpatialNoise3(half3(p * 1.7, st.time * 0.15)) - 0.5;
    half distanceToCenter = length(p) + boundaryNoise * max(st.riftNoise, 0.0);
    half width = max(st.edgeWidth, 1.0e-4);
    half coverage = 1.0 - smoothstep(1.0 - width, 1.0 + width, distanceToCenter);
    half edge = 1.0 - saturate(abs(distanceToCenter - 1.0) / width);
    return half2(coverage, edge);
}

half3 SBSSpatialFieldPosition(half3 objectPosition, half3 objectView, SBSSpatialStyle st)
{
    half3 view = normalize(objectView);
    half3 p = objectPosition * max(st.scale, 1.0e-3);
    p += view * max(st.depth, 0.0) * max(st.parallax, 0.0);
    p += half3(0.0, st.time, st.time * 0.37);
    return p;
}

half SBSSpatialStar(half3 p, half density, half size, out half seed)
{
    half3 cell = floor(p);
    half3 local = frac(p) - half3(0.5, 0.5, 0.5);
    seed = SBSSpatialHash3(cell);
    half gate = step(1.0 - saturate(density) * 0.42, seed);
    half radius = max(size, 0.01);
    return gate * pow(saturate(1.0 - length(local) / radius), 4.0) * (0.8 + seed * 2.2);
}

half3 SBSSpatialUniverse(half3 p, SBSSpatialStyle st)
{
    half seed;
    half star = SBSSpatialStar(p, st.starDensity, st.starSize, seed);

    half nebulaScale = max(st.nebulaScale, 1.0e-3);
    half cloudA = SBSSpatialNoise3(p * nebulaScale + half3(7.0, 3.0, 11.0));
    half cloudB = SBSSpatialNoise3(p * nebulaScale * 2.03 + half3(19.0, 5.0, 2.0));
    half cloud = saturate((cloudA * 0.72 + cloudB * 0.28 - 0.32) * 1.7) * max(st.nebula, 0.0);

    half3 nebulaColor = lerp(st.colorA, st.colorB, saturate(cloudA * 0.8 + cloudB * 0.2));
    half3 background = st.colorA * (0.12 + 0.28 * cloudA);
    return background + nebulaColor * cloud + half3(star, star, star);
}

half3 SBSSpatialStarfield(half3 p, SBSSpatialStyle st)
{
    half seedA;
    half seedB;
    half starA = SBSSpatialStar(p * 1.15, saturate(st.starDensity * 1.5), st.starSize * 0.72, seedA);
    half starB = SBSSpatialStar(
        p * 2.37 + half3(17.0, 31.0, 11.0),
        saturate(st.starDensity * 0.85),
        st.starSize * 0.45,
        seedB);
    half band = SBSSpatialNoise3(p * max(st.nebulaScale, 0.01) * 0.24 + half3(3.0, 19.0, 7.0));
    band = pow(saturate(band - 0.42), 2.0) * max(st.nebula, 0.0) * 0.45;
    half3 starColorA = lerp(half3(0.55, 0.72, 1.0), half3(1.0, 0.92, 0.7), seedA);
    half3 starColorB = lerp(half3(0.32, 0.58, 1.0), half3(0.85, 0.48, 1.0), seedB);
    half3 background = half3(0.006, 0.01, 0.035) + half3(0.06, 0.08, 0.16) * band;
    return background + starColorA * starA * 1.8 + starColorB * starB;
}

half3 SBSSpatialCyber(half3 p, SBSSpatialStyle st)
{
    half3 q = p * 0.72;
    half3 cell = floor(q);
    half3 local = abs(frac(q) - half3(0.5, 0.5, 0.5));
    half3 boundary = smoothstep(half3(0.39, 0.39, 0.39), half3(0.49, 0.49, 0.49), local);
    half grid = max(boundary.x * boundary.y, max(boundary.y * boundary.z, boundary.z * boundary.x));
    half seed = SBSSpatialHash3(cell);
    half signalGate = step(0.72, seed);
    half pulse = 0.35 + 0.65 * (sin(seed * 19.0 + st.time * 4.0) * 0.5 + 0.5);
    half data = signalGate * pulse * pow(saturate(1.0 - min(local.x, local.y) * 5.0), 4.0);
    half haze = SBSSpatialNoise3(q * 0.37 + half3(5.0, 13.0, st.time * 0.5));
    half3 neon = lerp(half3(0.0, 0.78, 1.0), half3(1.0, 0.04, 0.72), seed);
    half3 background = half3(0.001, 0.008, 0.022) + half3(0.0, 0.025, 0.055) * haze;
    return background + neon * (grid * 0.72 + data * 1.4);
}

half3 SBSSpatialMud(half3 p, SBSSpatialStyle st)
{
    half3 q = p * max(st.nebulaScale, 0.01) * 0.48;
    q += half3(
        sin(q.y * 0.7 + st.time) * 0.32,
        sin(q.z * 0.63 - st.time * 0.8) * 0.28,
        sin(q.x * 0.51 + st.time * 0.6) * 0.24);
    half coarse = SBSSpatialNoise3(q);
    half fine = SBSSpatialNoise3(q * 2.41 + half3(9.0, 2.0, 17.0));
    half body = saturate(coarse * 0.76 + fine * 0.24);
    half vein = smoothstep(0.46, 0.62, abs(coarse - fine) * 1.7);
    half wet = pow(saturate(fine * 1.12), 9.0);
    half3 darkMud = half3(0.018, 0.008, 0.003);
    half3 lightMud = half3(0.24, 0.075, 0.018);
    half3 color = lerp(darkMud, lightMud, body) * (0.65 + max(st.nebula, 0.0) * 0.35);
    color *= 1.0 - vein * 0.52;
    color += half3(0.72, 0.46, 0.19) * wet * 0.32;
    return color;
}

half3 SBSSpatialField(half3 objectPosition, half3 objectView, SBSSpatialStyle st)
{
    half3 p = SBSSpatialFieldPosition(objectPosition, objectView, st);
    if (st.preset < 0.5) return SBSSpatialUniverse(p, st);
    if (st.preset < 1.5) return SBSSpatialStarfield(p, st);
    if (st.preset < 2.5) return SBSSpatialCyber(p, st);
    return SBSSpatialMud(p, st);
}

half3 SBSSpatialApply(
    half3 base,
    half3 objectPosition,
    half3 objectView,
    half2 uv,
    half isFront,
    half mask,
    SBSSpatialStyle st)
{
    half2 region = SBSSpatialRegion(uv, st);
    half opacity = saturate(st.amount) * SBSSpatialSideMask(isFront, st.side) * region.x * saturate(mask);

    if (st.additivePass > 0.5)
        return base * (1.0 - opacity);

    half3 field = SBSSpatialField(objectPosition, objectView, st) * max(st.emission, 0.0);
    half3 result = lerp(base, field, opacity);
    half edge = region.y * saturate(st.amount) * SBSSpatialSideMask(isFront, st.side) * saturate(mask);
    return result + st.edgeColor * edge * max(st.emission, 0.0);
}

#endif // SABASHADER_SPATIALINTERIOR_CORE_INCLUDED
