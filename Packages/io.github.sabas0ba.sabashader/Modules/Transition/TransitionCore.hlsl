#ifndef SABASHADER_TRANSITION_CORE_INCLUDED
#define SABASHADER_TRANSITION_CORE_INCLUDED

// Progressは0が初期状態、1が完全なsolid／表示状態。
// clip判定、edge、vertex offsetを同じobject-space fieldから導出する。
struct SBSTransitionStyle
{
    half progress;
    half mode;
    half3 direction;
    half boundsMin;
    half boundsMax;
    half noiseScale;
    half noiseAmount;
    half edgeWidth;
    half3 edgeColor;
    half displacement;
    half blockScale;
    half liquidAmplitude;
    half liquidFrequency;
    half liquidSpeed;
    half liquidWobble;
    half liquidPuddle;
    half liquidPuddleHeight;
    half liquidPuddleSpread;
    half4 liquidTint;
    half time;
};

half SBSTransitionHash3(half3 p)
{
    half3 q = frac(half3(
        p.x * 0.1031 + p.y * 0.11369 + p.z * 0.13787,
        p.x * 0.1099 + p.y * 0.12317 + p.z * 0.09991,
        p.x * 0.0973 + p.y * 0.13121 + p.z * 0.11939));
    q = frac(q * (q.yzx + half3(43.71, 43.71, 43.71)));
    return frac((q.x + q.y) * q.z);
}

half SBSTransitionNoise3(half3 p)
{
    half3 cell = floor(p);
    half3 local = frac(p);
    half3 curve = local * local * (half3(3.0, 3.0, 3.0) - local * 2.0);

    half n000 = SBSTransitionHash3(cell);
    half n100 = SBSTransitionHash3(cell + half3(1.0, 0.0, 0.0));
    half n010 = SBSTransitionHash3(cell + half3(0.0, 1.0, 0.0));
    half n110 = SBSTransitionHash3(cell + half3(1.0, 1.0, 0.0));
    half n001 = SBSTransitionHash3(cell + half3(0.0, 0.0, 1.0));
    half n101 = SBSTransitionHash3(cell + half3(1.0, 0.0, 1.0));
    half n011 = SBSTransitionHash3(cell + half3(0.0, 1.0, 1.0));
    half n111 = SBSTransitionHash3(cell + half3(1.0, 1.0, 1.0));

    half low = lerp(lerp(n000, n100, curve.x), lerp(n010, n110, curve.x), curve.y);
    half high = lerp(lerp(n001, n101, curve.x), lerp(n011, n111, curve.x), curve.y);
    return lerp(low, high, curve.z);
}

half SBSTransitionHeight(half3 objectPosition, SBSTransitionStyle st)
{
    half range = max(st.boundsMax - st.boundsMin, 1.0e-4);
    half height = dot(objectPosition, normalize(st.direction));
    return saturate((height - st.boundsMin) / range);
}

half SBSTransitionField(half3 objectPosition, SBSTransitionStyle st)
{
    half progress = saturate(st.progress);
    if (st.mode < 0.5)
    {
        half height = SBSTransitionHeight(objectPosition, st);
        half envelope = 4.0 * progress * (1.0 - progress);
        half noise = (SBSTransitionNoise3(objectPosition * max(st.noiseScale, 1.0e-3)) - 0.5)
            * max(st.noiseAmount, 0.0) * envelope;
        return progress - height + noise;
    }

    half3 block = floor(objectPosition * max(st.blockScale, 1.0e-3));
    return progress - SBSTransitionHash3(block);
}

half SBSTransitionVisibility(half3 objectPosition, SBSTransitionStyle st)
{
    if (st.mode > 1.5) return 1.0;
    if (st.progress <= 0.0) return 0.0;
    if (st.progress >= 1.0) return 1.0;
    return step(0.0, SBSTransitionField(objectPosition, st));
}

half SBSTransitionEdge(half3 objectPosition, SBSTransitionStyle st)
{
    half progress = saturate(st.progress);
    if (st.mode > 1.5 || progress <= 0.0 || progress >= 1.0) return 0.0;
    half width = max(st.edgeWidth, 1.0e-4);
    return 1.0 - saturate(abs(SBSTransitionField(objectPosition, st)) / width);
}

half3 SBSTransitionMorphOffset(half3 objectPosition, half3 objectNormal, SBSTransitionStyle st)
{
    half progress = saturate(st.progress);
    half3 direction = normalize(st.direction);

    if (st.mode < 0.5)
    {
        half seed = SBSTransitionHash3(floor(objectPosition * max(st.noiseScale, 1.0e-3)));
        half lift = (1.0 - progress) * max(st.displacement, 0.0) * (0.25 + seed * 0.75);
        half3 scatter = objectNormal * ((seed - 0.5) * max(st.displacement, 0.0) * 0.25);
        return direction * lift + scatter;
    }

    if (st.mode < 1.5)
    {
        half3 block = floor(objectPosition * max(st.blockScale, 1.0e-3));
        half seed = SBSTransitionHash3(block);
        half transitionActivity = 1.0 - saturate(abs(progress - seed) / max(st.edgeWidth * 2.0, 0.02));
        half3 randomDirection = half3(
            SBSTransitionHash3(block + half3(17.0, 3.0, 5.0)) * 2.0 - 1.0,
            SBSTransitionHash3(block + half3(7.0, 19.0, 11.0)) * 2.0 - 1.0,
            SBSTransitionHash3(block + half3(13.0, 2.0, 23.0)) * 2.0 - 1.0);
        return randomDirection * max(st.displacement, 0.0) * transitionActivity;
    }

    half liquid = 1.0 - progress;
    half frequency = max(st.liquidFrequency, 1.0e-3);
    half phase = st.time * st.liquidSpeed;
    half waveA = sin(dot(objectPosition, half3(0.73, 1.17, 0.41)) * frequency + phase);
    half waveB = sin(dot(objectPosition, half3(-1.31, 0.47, 0.89)) * frequency * 1.73 - phase * 1.37);
    half waveC = sin(dot(objectPosition, half3(0.37, -0.83, 1.43)) * frequency * 2.41 + phase * 0.71);
    half wobble = max(st.liquidWobble, 0.0);
    half complexWave = (waveA + waveB * 0.55 + waveC * 0.3) / 1.85;
    half waveShape = lerp(waveA, complexWave, saturate(wobble)) * (1.0 + max(wobble - 1.0, 0.0));
    half amplitude = max(st.liquidAmplitude, 0.0);
    half3 waveOffset = objectNormal * waveShape * amplitude * liquid;
    waveOffset -= direction * amplitude * liquid * 0.2;

    half range = max(st.boundsMax - st.boundsMin, 1.0e-4);
    half height = dot(objectPosition, direction);
    half normalizedHeight = saturate((height - st.boundsMin) / range);
    half targetHeight = st.boundsMin + normalizedHeight * range * saturate(st.liquidPuddleHeight);
    half3 planarPosition = objectPosition - direction * height;
    half3 puddleOffset = direction * (targetHeight - height);
    puddleOffset += planarPosition * max(st.liquidPuddleSpread, 0.0);
    half puddleBlend = saturate(st.liquidPuddle) * liquid * liquid * (3.0 - 2.0 * liquid);
    return waveOffset + puddleOffset * puddleBlend;
}

half3 SBSTransitionLiquidAlbedo(half3 albedo, SBSTransitionStyle st)
{
    if (st.mode < 1.5) return albedo;
    half liquid = 1.0 - saturate(st.progress);
    half opacity = liquid * saturate(st.liquidTint.a);
    return lerp(albedo, albedo * st.liquidTint.rgb, opacity);
}

half3 SBSTransitionEdgeColor(half3 base, half3 objectPosition, SBSTransitionStyle st)
{
    return base + st.edgeColor * SBSTransitionEdge(objectPosition, st);
}

#endif // SABASHADER_TRANSITION_CORE_INCLUDED
