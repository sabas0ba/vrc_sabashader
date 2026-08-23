#ifndef SABASHADER_ILLUST2D_FRAGMENT_INCLUDED
#define SABASHADER_ILLUST2D_FRAGMENT_INCLUDED

// ForwardBase / ForwardAdd 用のピクセルシェーダー。
// birp_forward.hlsl -> Illust2DLighting.hlsl -> birp_lighting.hlsl の順に
// include された後に読み込まれる想定。

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
    sd.mask = SCSample(_SharedMask, sampler_BaseTexture, sd.uv);
    sd.roughness = _Roughness;
    sd.normalMapWithRoughness = _NormalMapWithRoughness != 0;
    sd.N = SCUnpackNormalAndRoughness(SCSample(_NormalMap, sampler_BaseTexture, sd.uv), _NormalScale, sd.roughness, sd.normalMapWithRoughness);
    sd.N_detail = sd.N;
    sd.maskTexture = _SharedMask;
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
    sd.col = sd.albedoAlpha;

    sd.N = normalize(mul(sd.N, vertex.TBN));
    sd.N_detail = normalize(mul(sd.N_detail, vertex.TBN));
    sd.T = normalize(vertex.T - sd.N_detail * dot(sd.N_detail, vertex.T));
    sd.B = normalize(cross(sd.N_detail, sd.T) * vertex.crossDirection * unity_WorldTransformParams.w);

    // 裏面は法線を反転して両面表示でも塗りが破綻しないようにする
    if (!vertex.isFront)
    {
        sd.N = -sd.N;
        sd.N_detail = -sd.N_detail;
    }

    SCLightData lightSum = (SCLightData)0;
    half3 env = 0;
    SCCalculateAllLights(lightSum, env, sd, cd, vertex, i);

    sd.L = SBSResolveLightDirection(lightSum.direction);
    sd.lightColor = lightSum.color;

    __SC_PHASE_modifylight__

    SBSStyle style = SBSGetStyle();

    SBSSurface surf;
    surf.albedo = sd.albedoAlpha.rgb;
    surf.N = normalize(sd.N);
    surf.L = sd.L;
    surf.V = normalize(vertex.V);
    surf.lightColor = sd.lightColor;
    surf.ambientColor = env;
    surf.attenuation = sd.shadow;
    surf.shadeMask = SBSMaskChannel(sd.mask, _ShadeMaskChannel);
    surf.specularMask = SBSMaskChannel(sd.mask, _SpecularMaskChannel);
    surf.rimMask = SBSMaskChannel(sd.mask, _RimMaskChannel);

    cd.shadingFactor = SBSShadingFactor(surf, style);
    sd.col.rgb = SBSShadedAlbedo(surf, style);

    __SC_PHASE_shade__

    __SC_PHASE_reflection__

    sd.add += SBSSpecularTerm(surf, style);
    sd.add += SBSRimTerm(surf, style);

    #ifndef UNITY_PASS_FORWARDADD
        sd.postadd += SCSample(_EmissionMap, sampler_BaseTexture, sd.uv).rgb * _EmissionColor.rgb;
    #endif

    __SC_PHASE_add__

    #ifdef UNITY_PASS_FORWARDADD
        half3 illum = sd.lightColor;
        sd.col.rgb = (sd.col.rgb + sd.add) * illum + sd.postadd;
    #else
        half3 illum = SBSIlluminate(surf, style);
        sd.col.rgb = (sd.col.rgb + sd.add) * illum + sd.postadd;
        sd.col.rgb = SBSGrade(sd.col.rgb, style);
    #endif

    sd.col.a = sd.albedoAlpha.a;

    __SC_PHASE_postpixel__

    #ifdef UNITY_PASS_FORWARDADD
        UNITY_APPLY_FOG_COLOR(i.fogCoord, sd.col, fixed4(0,0,0,0));
    #else
        UNITY_APPLY_FOG(i.fogCoord, sd.col);
    #endif

    return sd.col;
}

#endif // SABASHADER_ILLUST2D_FRAGMENT_INCLUDED
