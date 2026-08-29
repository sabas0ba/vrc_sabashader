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

half SBSBankActivity(half progress)
{
    progress = saturate(progress);
    return 4.0 * progress * (1.0 - progress);
}

half3 SBSBankShatterDirection(half3 objectPosition, SBSBankStyle st)
{
    half3 cell = floor(objectPosition * max(st.blockScale, 1.0e-3));
    half3 direction = half3(
        SBSBankHash3(cell + half3(17.0, 3.0, 5.0)) * 2.0 - 1.0,
        SBSBankHash3(cell + half3(7.0, 19.0, 11.0)) * 2.0 - 1.0,
        SBSBankHash3(cell + half3(13.0, 2.0, 23.0)) * 2.0 - 1.0);
    return normalize(direction + half3(1.0e-3, 1.0e-3, 1.0e-3));
}

half SBSBankFlameField(half3 objectPosition, SBSBankStyle st)
{
    half scale = max(st.noiseScale, 1.0e-3);
    half3 upward = normalize(st.direction);
    half3 flow = objectPosition * scale - upward * st.time * st.patternSpeed * 0.45;
    half broad = SBSBankNoise3(flow);
    half detail = SBSBankNoise3(flow * 1.91 + half3(7.0, 13.0, 3.0));
    half flame = lerp(broad, detail, 0.38);
    half turbulence = (flame - 0.46) * st.noiseAmount * (0.7 + SBSBankActivity(st.progress));
    return st.visibilityProgress - SBSBankHeight(objectPosition, st) + turbulence;
}

half SBSBankGlitchAmount(half3 objectPosition, SBSBankStyle st)
{
    half bandScale = max(st.blockScale, 1.0e-3);
    half band = floor(objectPosition.y * bandScale + st.time * st.patternSpeed * 4.0);
    half offset = SBSBankHash3(half3(band, band * 0.37, band * 1.71)) * 2.0 - 1.0;
    return offset * SBSBankActivity(st.progress);
}

half SBSBankMeltField(half3 objectPosition, SBSBankStyle st)
{
    half height = SBSBankHeight(objectPosition, st);
    if (st.role < 0.5)
        return st.visibilityProgress - height;

    half scale = max(st.noiseScale, 1.0e-3);
    half3 dripPosition = objectPosition * half3(scale * 0.42, scale * 0.1, scale * 0.42);
    dripPosition.y += st.time * st.patternSpeed * 0.18;
    half drip = SBSBankNoise3(dripPosition);
    half warp = (drip - 0.44) * st.noiseAmount * (0.6 + SBSBankActivity(st.progress));
    return st.visibilityProgress - height + warp;
}

half SBSBankField(half3 objectPosition, SBSBankStyle st)
{
    half progress = saturate(st.visibilityProgress);
    half envelope = SBSBankActivity(progress);
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

    if (st.style < 4.5)
    {
        half3 shadowCell = floor(objectPosition * max(st.blockScale * 0.5, 1.0e-3));
        half shadow = lerp(noise, SBSBankHash3(shadowCell), 0.3);
        return progress - shadow;
    }
    if (st.style < 5.5)
        return SBSBankFlameField(objectPosition, st);
    if (st.style < 6.5)
    {
        half3 shardCell = floor(objectPosition * max(st.blockScale, 1.0e-3));
        return progress - SBSBankHash3(shardCell);
    }
    if (st.style < 7.5)
    {
        half glitch = SBSBankGlitchAmount(objectPosition, st);
        half3 shifted = objectPosition + half3(glitch * st.noiseAmount, 0.0, 0.0);
        half fine = SBSBankNoise3(shifted * scale);
        half3 block = floor(shifted * max(st.blockScale, 1.0e-3));
        half coarse = SBSBankHash3(block);
        return progress - lerp(fine, coarse, envelope * 0.82);
    }
    return SBSBankMeltField(objectPosition, st);
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
    half envelope = SBSBankActivity(progress);

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
    if (st.style < 4.5)
        return -objectNormal * st.displacement * edge * (0.25 + seed * 0.5);
    if (st.style < 5.5)
    {
        half flutter = SBSBankNoise3(objectPosition * scale - direction * st.time * st.patternSpeed);
        return (direction * (0.35 + flutter) + objectNormal * (seed - 0.5) * 0.55)
            * st.displacement * saturate(edge + envelope * 0.25);
    }
    if (st.style < 6.5)
    {
        half3 shardDirection = SBSBankShatterDirection(objectPosition, st);
        return shardDirection * st.displacement * (0.2 + envelope * 1.8)
            * saturate(edge + envelope * 0.65);
    }
    if (st.style < 7.5)
    {
        half glitch = SBSBankGlitchAmount(objectPosition, st);
        return half3(glitch, (seed - 0.5) * 0.15, -glitch * 0.28)
            * st.displacement;
    }
    if (st.role < 0.5)
        return half3(0.0, 0.0, 0.0);
    return (-direction * (0.55 + seed) + objectNormal * (seed - 0.5) * 0.18)
        * st.displacement * envelope;
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

    if (st.style < 4.5)
    {
        half shadow = SBSBankNoise3(objectPosition * scale + st.direction * phase * 0.1);
        return saturate(shadow * shadow + rim * 0.55);
    }
    if (st.style < 5.5)
    {
        half3 flow = objectPosition * scale - normalize(st.direction) * phase * 0.6;
        half broad = SBSBankNoise3(flow);
        half detail = SBSBankNoise3(flow * 2.13 + half3(11.0, 5.0, 17.0));
        half flame = saturate((broad * 0.7 + detail * 0.3 - 0.38) * 2.4);
        return saturate(flame + rim * 0.65);
    }
    if (st.style < 6.5)
    {
        half3 cell = abs(frac(objectPosition * scale) - 0.5);
        half shardEdge = 1.0 - saturate(min(cell.x, min(cell.y, cell.z)) * 13.0);
        return saturate(shardEdge + rim * 0.3);
    }
    if (st.style < 7.5)
    {
        half band = floor(objectPosition.y * scale * 2.0 + phase * 5.0);
        half scanline = step(0.72, SBSBankHash3(half3(band, band * 0.31, 4.0)));
        half channel = 1.0 - saturate(abs(frac(objectPosition.x * scale + phase) - 0.5) * 8.0);
        return saturate(scanline * 0.7 + channel + rim * 0.2);
    }
    if (st.role < 0.5)
        return 0.0;
    half3 liquidPosition = objectPosition * half3(scale * 0.55, scale * 0.12, scale * 0.55);
    liquidPosition.y += phase * 0.25;
    half liquid = SBSBankNoise3(liquidPosition);
    return saturate((liquid - 0.35) * 1.8 + rim * 0.15);
}

half3 SBSBankSurfaceColor(
    half3 base,
    half3 objectPosition,
    half3 normal,
    half3 viewDirection,
    SBSBankStyle st)
{
    half activity = SBSBankActivity(st.progress);
    half pattern = SBSBankPattern(objectPosition, normal, viewDirection, st) * activity;
    half edge = SBSBankEdge(objectPosition, st);
    if (st.style >= 7.5 && st.role < 0.5)
    {
        pattern = 0.0;
        edge = 0.0;
    }
    half3 result = base;
    if (st.role > 1.5)
        result = st.coverColor.rgb;
    result += st.patternColor.rgb * st.patternColor.a * st.patternEmission * pattern;
    result += st.edgeColor * edge;
    return result;
}

#endif // SABASHADER_TRANSFORMATION_BANK_CORE_INCLUDED
