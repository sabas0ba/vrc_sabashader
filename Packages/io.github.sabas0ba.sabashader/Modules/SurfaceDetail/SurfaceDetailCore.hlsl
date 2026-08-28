#ifndef SABASHADER_SURFACEDETAIL_CORE_INCLUDED
#define SABASHADER_SURFACEDETAIL_CORE_INCLUDED

// 肌のporeまたは布の織りを手続き生成し、micro normal、色の微差、
// specular breakupとgrazing sheenへ分けて適用する数式。
struct SBSSurfaceDetailStyle
{
    half amount;
    half mode;
    half scale;
    half textureStrength;
    half albedoVariation;
    half normalStrength;
    half roughnessVariation;
    half pore;
    half weave;
    half sheen;
    half3 sheenColor;
};

half SBSSurfaceDetailHash(half2 p)
{
    half2 q = frac(half2(
        p.x * 0.3183099 + p.y * 0.6931472,
        p.x * 0.4142136 + p.y * 0.7320508));
    q = frac(half2(q.x * (q.y + 47.17), q.y * (q.x + 47.17)));
    return frac(q.x + q.y * 0.6180340);
}

half SBSSurfaceDetailNoise(half2 p)
{
    half2 cell = floor(p);
    half2 local = frac(p);
    half2 curve = local * local * (half2(3.0, 3.0) - local * 2.0);

    half a = SBSSurfaceDetailHash(cell);
    half b = SBSSurfaceDetailHash(cell + half2(1.0, 0.0));
    half c = SBSSurfaceDetailHash(cell + half2(0.0, 1.0));
    half d = SBSSurfaceDetailHash(cell + half2(1.0, 1.0));
    return lerp(lerp(a, b, curve.x), lerp(c, d, curve.x), curve.y);
}

half SBSSurfaceDetailSkinHeight(half2 uv, SBSSurfaceDetailStyle st)
{
    half2 p = uv * max(st.scale, 1.0);
    half2 cell = floor(p);
    half2 jitter = half2(
        SBSSurfaceDetailHash(cell + half2(13.0, 7.0)),
        SBSSurfaceDetailHash(cell + half2(3.0, 29.0))) * 0.6 + half2(0.2, 0.2);
    half distanceToPore = length(frac(p) - jitter);
    half poreShape = 1.0 - smoothstep(0.04, 0.22, distanceToPore);
    half grain = SBSSurfaceDetailNoise(p * 0.28) * 2.0 - 1.0;
    return grain * 0.35 - poreShape * saturate(st.pore);
}

half SBSSurfaceDetailFabricHeight(half2 uv, SBSSurfaceDetailStyle st)
{
    half2 p = uv * max(st.scale, 1.0);
    half2 local = abs(frac(p) - half2(0.5, 0.5)) * 2.0;
    half warp = pow(saturate(1.0 - local.x), 2.0);
    half weft = pow(saturate(1.0 - local.y), 2.0);
    half parity = fmod(floor(p.x) + floor(p.y), 2.0);
    half thread = lerp(warp * 0.75 + weft * 0.35, warp * 0.35 + weft * 0.75, parity);
    half fiber = SBSSurfaceDetailNoise(p * 0.5) * 0.18;
    return (thread + fiber - 0.5) * saturate(st.weave);
}

half SBSSurfaceDetailHeight(half2 uv, SBSSurfaceDetailStyle st)
{
    if (st.mode < 0.5)
        return SBSSurfaceDetailSkinHeight(uv, st);
    return SBSSurfaceDetailFabricHeight(uv, st);
}

half3 SBSSurfaceDetailNormal(half2 uv, SBSSurfaceDetailStyle st)
{
    half stepSize = 0.35 / max(st.scale, 1.0);
    half center = SBSSurfaceDetailHeight(uv, st);
    half dx = SBSSurfaceDetailHeight(uv + half2(stepSize, 0.0), st) - center;
    half dy = SBSSurfaceDetailHeight(uv + half2(0.0, stepSize), st) - center;
    half strength = max(st.normalStrength, 0.0) * 3.0;
    return normalize(half3(-dx * strength, -dy * strength, 1.0));
}

half3 SBSSurfaceDetailBlendNormal(
    half3 baseNormal,
    half3 proceduralNormal,
    half2 textureNormal,
    half mask,
    SBSSurfaceDetailStyle st)
{
    half amount = saturate(st.amount) * saturate(mask);
    half2 detailXY = proceduralNormal.xy + textureNormal * max(st.textureStrength, 0.0);
    half3 combined = half3(
        baseNormal.x + detailXY.x * amount,
        baseNormal.y + detailXY.y * amount,
        max(baseNormal.z * proceduralNormal.z, 1.0e-4));
    return normalize(combined);
}

half3 SBSSurfaceDetailAlbedo(
    half3 albedo,
    half2 uv,
    half3 textureColor,
    half mask,
    SBSSurfaceDetailStyle st)
{
    half height = SBSSurfaceDetailHeight(uv, st);
    half textureValue = dot(textureColor, half3(0.299, 0.587, 0.114)) - 0.5;
    half variation = height * 0.65 + textureValue * max(st.textureStrength, 0.0);
    half multiplier = 1.0 + variation * max(st.albedoVariation, 0.0) * saturate(st.amount) * saturate(mask);
    return max(albedo * multiplier, half3(0.0, 0.0, 0.0));
}

half SBSSurfaceDetailRoughness(half roughness, half2 uv, half mask, SBSSurfaceDetailStyle st)
{
    half variation = SBSSurfaceDetailHeight(uv, st) * st.roughnessVariation * 0.2;
    return saturate(roughness + variation * saturate(st.amount) * saturate(mask));
}

half3 SBSSurfaceDetailSpecular(
    half3 N,
    half3 L,
    half3 V,
    half2 uv,
    half mask,
    SBSSurfaceDetailStyle st)
{
    half amount = saturate(st.amount) * saturate(mask) * max(st.sheen, 0.0);
    half height = SBSSurfaceDetailHeight(uv, st);
    half breakup = lerp(1.0, saturate(height * 0.5 + 0.65), saturate(st.roughnessVariation));
    half ndl = saturate(dot(normalize(N), normalize(L)));
    half ndv = saturate(dot(normalize(N), normalize(V)));

    half response;
    if (st.mode < 0.5)
    {
        half3 H = normalize(normalize(L) + normalize(V));
        response = pow(saturate(dot(normalize(N), H)), 28.0) * ndl * breakup * 0.35;
    }
    else
    {
        response = pow(1.0 - ndv, 2.5) * (0.25 + 0.75 * ndl) * (0.7 + 0.3 * breakup);
    }

    return st.sheenColor * response * amount;
}

#endif // SABASHADER_SURFACEDETAIL_CORE_INCLUDED
