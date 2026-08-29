#ifndef SABASHADER_TRANSFORMATION_BANK_CORE_INCLUDED
#define SABASHADER_TRANSFORMATION_BANK_CORE_INCLUDED

// progress はバンク全体、visibilityProgress はRoleから導出したmesh単位の表示率。
struct SBSBankStyle
{
    half progress;
    half visibilityProgress;
    half role;
    half style;
    half3 direction;
    half boundsMin;
    half boundsMax;
    half noiseScale;
    half noiseAmount;
    half edgeWidth;
    half3 edgeColor;
    half displacement;
    half blockScale;
    half4 coverColor;
    half4 patternColor;
    half patternScale;
    half patternSpeed;
    half patternEmission;
    half time;
};

half SBSBankHash3(half3 p)
{
    half3 q = frac(half3(
        p.x * 0.1031 + p.y * 0.11369 + p.z * 0.13787,
        p.x * 0.1099 + p.y * 0.12317 + p.z * 0.09991,
        p.x * 0.0973 + p.y * 0.13121 + p.z * 0.11939));
    q = frac(q * (q.yzx + half3(43.71, 43.71, 43.71)));
    return frac((q.x + q.y) * q.z);
}

half SBSBankNoise3(half3 p)
{
    half3 cell = floor(p);
    half3 local = frac(p);
    half3 curve = local * local * (half3(3.0, 3.0, 3.0) - local * 2.0);

    half n000 = SBSBankHash3(cell);
    half n100 = SBSBankHash3(cell + half3(1.0, 0.0, 0.0));
    half n010 = SBSBankHash3(cell + half3(0.0, 1.0, 0.0));
    half n110 = SBSBankHash3(cell + half3(1.0, 1.0, 0.0));
    half n001 = SBSBankHash3(cell + half3(0.0, 0.0, 1.0));
    half n101 = SBSBankHash3(cell + half3(1.0, 0.0, 1.0));
    half n011 = SBSBankHash3(cell + half3(0.0, 1.0, 1.0));
    half n111 = SBSBankHash3(cell + half3(1.0, 1.0, 1.0));

    half low = lerp(lerp(n000, n100, curve.x), lerp(n010, n110, curve.x), curve.y);
    half high = lerp(lerp(n001, n101, curve.x), lerp(n011, n111, curve.x), curve.y);
    return lerp(low, high, curve.z);
}

half SBSBankOrderedSmoothstep(half start, half end, half value)
{
    half low = min(start, end);
    half high = max(start, end);
    half amount = smoothstep(low, max(high, low + 1.0e-4), value);
    return start <= end ? amount : 1.0 - amount;
}

half SBSBankRoleProgress(
    half progress,
    half role,
    half4 incomingOutgoingWindow,
    half4 coverWindow)
{
    progress = saturate(progress);
    if (role < 0.5)
        return SBSBankOrderedSmoothstep(
            incomingOutgoingWindow.x,
            incomingOutgoingWindow.y,
            progress);
    if (role < 1.5)
        return 1.0 - SBSBankOrderedSmoothstep(
            incomingOutgoingWindow.z,
            incomingOutgoingWindow.w,
            progress);

    half appear = SBSBankOrderedSmoothstep(coverWindow.x, coverWindow.y, progress);
    half disappear = 1.0 - SBSBankOrderedSmoothstep(coverWindow.z, coverWindow.w, progress);
    return appear * disappear;
}

half SBSBankHeight(half3 objectPosition, SBSBankStyle st)
{
    half range = max(st.boundsMax - st.boundsMin, 1.0e-4);
    half height = dot(objectPosition, normalize(st.direction));
    return saturate((height - st.boundsMin) / range);
}

half SBSBankField(half3 objectPosition, SBSBankStyle st)
{
    half progress = saturate(st.visibilityProgress);
    half envelope = 4.0 * progress * (1.0 - progress);
    half scale = max(st.noiseScale, 1.0e-3);
    half noise = SBSBankNoise3(objectPosition * scale);

    if (st.style < 0.5)
    {
        return progress - SBSBankHeight(objectPosition, st)
            + (noise - 0.5) * st.noiseAmount * envelope;
    }
    if (st.style < 1.5)
    {
        half3 block = floor(objectPosition * max(st.blockScale, 1.0e-3));
        return progress - SBSBankHash3(block);
    }
    if (st.style < 2.5)
    {
        half drift = st.time * st.patternSpeed * 0.08;
        return progress - SBSBankNoise3(
            objectPosition * scale + st.direction * drift);
    }
    if (st.style < 3.5)
    {
        return progress - SBSBankHeight(objectPosition, st)
            + (noise - 0.5) * st.noiseAmount * 1.4 * envelope;
    }

    half3 shadowCell = floor(objectPosition * max(st.blockScale * 0.5, 1.0e-3));
    half shadow = lerp(noise, SBSBankHash3(shadowCell), 0.3);
    return progress - shadow;
}

half SBSBankVisibility(half3 objectPosition, SBSBankStyle st)
{
    if (st.visibilityProgress <= 0.0) return 0.0;
    if (st.visibilityProgress >= 1.0) return 1.0;
    return step(0.0, SBSBankField(objectPosition, st));
}

half SBSBankEdge(half3 objectPosition, SBSBankStyle st)
{
    if (st.visibilityProgress <= 0.0 || st.visibilityProgress >= 1.0) return 0.0;
    half width = max(st.edgeWidth, 1.0e-4);
    return 1.0 - saturate(abs(SBSBankField(objectPosition, st)) / width);
}

half3 SBSBankMorphOffset(half3 objectPosition, half3 objectNormal, SBSBankStyle st)
{
    half progress = saturate(st.visibilityProgress);
    if (progress <= 0.0 || progress >= 1.0) return half3(0.0, 0.0, 0.0);

    half edge = SBSBankEdge(objectPosition, st);
    half scale = max(st.noiseScale, 1.0e-3);
    half seed = SBSBankHash3(floor(objectPosition * scale));
    half3 direction = normalize(st.direction);

    if (st.style < 0.5)
        return (direction * (0.2 + seed * 0.3) + objectNormal * (seed - 0.5) * 0.2)
            * st.displacement * edge;
    if (st.style < 1.5)
    {
        half3 block = floor(objectPosition * max(st.blockScale, 1.0e-3));
        half3 randomDirection = half3(
            SBSBankHash3(block + half3(17.0, 3.0, 5.0)) * 2.0 - 1.0,
            SBSBankHash3(block + half3(7.0, 19.0, 11.0)) * 2.0 - 1.0,
            SBSBankHash3(block + half3(13.0, 2.0, 23.0)) * 2.0 - 1.0);
        return randomDirection * st.displacement * edge;
    }
    if (st.style < 2.5)
        return objectNormal * st.displacement * edge * (0.25 + seed * 0.35);
    if (st.style < 3.5)
        return -direction * st.displacement * edge * (0.3 + seed * 0.7);
    return -objectNormal * st.displacement * edge * (0.25 + seed * 0.5);
}

half SBSBankPattern(half3 objectPosition, half3 normal, half3 viewDirection, SBSBankStyle st)
{
    half scale = max(st.patternScale, 1.0e-3);
    half phase = st.time * st.patternSpeed;
    half rim = pow(1.0 - saturate(abs(dot(normalize(normal), normalize(viewDirection)))), 3.0);

    if (st.style < 0.5)
    {
        half ring = 1.0 - saturate(abs(frac(length(objectPosition.xz) * scale - phase * 0.15) - 0.5) * 12.0);
        half rune = 1.0 - saturate(abs(sin((objectPosition.x + objectPosition.z) * scale * 2.0 + phase)) * 7.0);
        return saturate(max(ring, rune) * 0.8 + rim * 0.35);
    }
    if (st.style < 1.5)
    {
        half3 cell = abs(frac(objectPosition * scale + phase * 0.05) - 0.5);
        half grid = 1.0 - saturate(min(cell.x, min(cell.y, cell.z)) * 18.0);
        return saturate(grid + rim * 0.25);
    }
    if (st.style < 2.5)
    {
        half3 starCell = floor(objectPosition * scale + phase * 0.08);
        half star = step(0.9, SBSBankHash3(starCell));
        return saturate(star + rim * 0.75);
    }
    if (st.style < 3.5)
    {
        half rock = SBSBankNoise3(objectPosition * scale);
        half crack = 1.0 - saturate(abs(rock - 0.5) * 14.0);
        return saturate(crack + rim * 0.2);
    }

    half shadow = SBSBankNoise3(objectPosition * scale + st.direction * phase * 0.1);
    return saturate(shadow * shadow + rim * 0.55);
}

half3 SBSBankSurfaceColor(
    half3 base,
    half3 objectPosition,
    half3 normal,
    half3 viewDirection,
    SBSBankStyle st)
{
    half activity = 4.0 * saturate(st.progress) * (1.0 - saturate(st.progress));
    half pattern = SBSBankPattern(objectPosition, normal, viewDirection, st) * activity;
    half3 result = base;
    if (st.role > 1.5)
        result = st.coverColor.rgb;
    result += st.patternColor.rgb * st.patternColor.a * st.patternEmission * pattern;
    result += st.edgeColor * SBSBankEdge(objectPosition, st);
    return result;
}

#endif // SABASHADER_TRANSFORMATION_BANK_CORE_INCLUDED
