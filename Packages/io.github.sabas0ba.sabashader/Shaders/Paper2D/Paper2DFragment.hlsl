#ifndef SABASHADER_PAPER2D_FRAGMENT_INCLUDED
#define SABASHADER_PAPER2D_FRAGMENT_INCLUDED

half4 frag(v2f i, bool isFront : SV_IsFrontFace) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    SCPositionAndDirection camera = SCGetCameraData();
    SCPositionAndDirection head = SCGetHeadData();
    SCPositionAndDirection headBone = SCGetHeadBoneData();
    SCVertexData vertex = FromPixelInput(i, camera, head, headBone, unity_WorldTransformParams.w, isFront);

    SCCustomData cd = (SCCustomData)0;

    SCShadingData sd;
    sd.uv = SBSBaseUV(vertex.uv[0].xy);
    sd.albedoAlpha = SCSample(_BaseTexture, sampler_BaseTexture, sd.uv) * _BaseColor;
    sd.mask = 1.0;
    sd.roughness = _Roughness;
    sd.normalMapWithRoughness = false;
    sd.N = SCUnpackNormalAndRoughness(SCSample(_NormalMap, sampler_BaseTexture, sd.uv), _NormalScale, sd.roughness, false);
    sd.N_detail = sd.N;
    sd.maskTexture = _BaseTexture;
    sd.gradientsTexture = _SharedGradients;
    sd.T = 0;
    sd.B = 0;
    sd.L = 0;
    sd.lightColor = 0;
    sd.shadow = 1;
    sd.add = 0;
    sd.postadd = 0;
    sd.col = 0;

    __SC_PHASE_base__

    if (_AlphaMode == 1) clip(sd.albedoAlpha.a - _Cutoff);

    sd.albedoAlpha = saturate(sd.albedoAlpha);
    sd.N = normalize(mul(sd.N, vertex.TBN));

    if (!vertex.isFront) sd.N = -sd.N;

    SCLightData lightSum = (SCLightData)0;
    half3 env = 0;
    SCCalculateAllLights(lightSum, env, sd, cd, vertex, i);

    sd.L = SBSResolveLightDirection(lightSum.direction);
    sd.lightColor = lightSum.color;

    __SC_PHASE_modifylight__

    half3 N = normalize(sd.N);
    half3 L = normalize(sd.L);
    half3 V = normalize(vertex.V);
    half diffuse = SBSFlatDiffuse(N, L, _Flatness, _ShadeThreshold);
    half surfaceLight = lerp(1.0, diffuse * sd.shadow, saturate((half)_SurfaceShadowEnabled));

    #ifdef UNITY_PASS_FORWARDADD
        half3 lighting = lightSum.color * surfaceLight;
    #else
        half3 ambient = env * _SHLightWeight;
        half directional = lerp(0.5, 1.0, saturate(_SHLightDirectionWeight));
        half3 lighting = ambient + lightSum.color * surfaceLight * directional;
    #endif

    half grain = SBSPaperGrain(sd.uv * _PaperGrainScale);
    half3 paperColor = sd.albedoAlpha.rgb * (1.0 + grain * _PaperGrain);
    half faceBack = vertex.isFront ? 0.0 : 1.0;
    half3 faceColor = lerp(half3(1.0, 1.0, 1.0), _BackfaceColor.rgb, faceBack * _BackfaceStrength);
    half fresnel = SBSFresnel(N, V, _EdgePower);
    float2 edgeStep = _EdgeWidth * _BaseTexture_ST.xy;
    half4 adjacentAlpha = half4(
        SCSample(_BaseTexture, sampler_BaseTexture, sd.uv + float2(edgeStep.x, 0.0)).a,
        SCSample(_BaseTexture, sampler_BaseTexture, sd.uv - float2(edgeStep.x, 0.0)).a,
        SCSample(_BaseTexture, sampler_BaseTexture, sd.uv + float2(0.0, edgeStep.y)).a,
        SCSample(_BaseTexture, sampler_BaseTexture, sd.uv - float2(0.0, edgeStep.y)).a) * _BaseColor.a;
    half contour = SBSAlphaContourMask(sd.albedoAlpha.a, adjacentAlpha);
    half specular = SBSBlinnSpecular(N, L, V, lerp(8.0, _SpecularPower, saturate(1.0 - _Roughness)));

    #ifdef UNITY_PASS_FORWARDADD
        sd.col.rgb = paperColor * lighting * faceColor;
    #else
        sd.col.rgb = paperColor * lighting * faceColor;
        sd.col.rgb += _EdgeColor.rgb * contour * (0.35 + 0.65 * fresnel) * _EdgeIntensity;
        sd.col.rgb += _SpecularColor.rgb * specular * _SpecularIntensity;
    #endif

    __SC_PHASE_shade__
    __SC_PHASE_reflection__
    __SC_PHASE_add__

    sd.col.a = sd.albedoAlpha.a;

    __SC_PHASE_postpixel__

    #ifdef UNITY_PASS_FORWARDADD
        UNITY_APPLY_FOG_COLOR(i.fogCoord, sd.col, fixed4(0,0,0,0));
    #else
        UNITY_APPLY_FOG(i.fogCoord, sd.col);
    #endif

    return sd.col;
}

#endif // SABASHADER_PAPER2D_FRAGMENT_INCLUDED
