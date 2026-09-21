#ifndef SABASHADER_ACRYLIC2D_FRAGMENT_INCLUDED
#define SABASHADER_ACRYLIC2D_FRAGMENT_INCLUDED

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

    sd.albedoAlpha = saturate(sd.albedoAlpha);
    half printCoverage = sd.albedoAlpha.a;
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
    half grazing = 1.0 - saturate(abs(dot(N, V)));
    half3 tangent = normalize(vertex.T);
    half3 bitangent = normalize(vertex.B);
    float2 viewTangent = float2(dot(V, tangent), dot(V, bitangent));
    float2 refractionOffset = viewTangent * (grazing * _RefractionStrength * _RefractionScale);
    half4 refractedAlbedo = SCSample(_BaseTexture, sampler_BaseTexture, sd.uv + refractionOffset) * _BaseColor;
    half refractionAmount = saturate(grazing * _RefractionScale);
    half3 printedColor = lerp(sd.albedoAlpha.rgb, refractedAlbedo.rgb, refractionAmount);
    half refractedCoverage = lerp(sd.albedoAlpha.a, refractedAlbedo.a, refractionAmount);
    half diffuse = SBSFlatDiffuse(N, L, _Flatness, _ShadeThreshold);
    half surfaceLight = lerp(1.0, lerp(0.35, 1.0, diffuse), saturate((half)_SurfaceShadowEnabled));
    half fresnel = SBSFresnel(N, V, _EdgePower);
    float2 edgeStep = _EdgeWidth * _BaseTexture_ST.xy;
    half4 adjacentAlpha = half4(
        SCSample(_BaseTexture, sampler_BaseTexture, sd.uv + float2(edgeStep.x, 0.0)).a,
        SCSample(_BaseTexture, sampler_BaseTexture, sd.uv - float2(edgeStep.x, 0.0)).a,
        SCSample(_BaseTexture, sampler_BaseTexture, sd.uv + float2(0.0, edgeStep.y)).a,
        SCSample(_BaseTexture, sampler_BaseTexture, sd.uv - float2(0.0, edgeStep.y)).a) * _BaseColor.a;
    half contour = SBSAlphaContourMask(sd.albedoAlpha.a, adjacentAlpha);

    #ifdef UNITY_PASS_FORWARDADD
        half3 lighting = lightSum.color * surfaceLight;
        half3 specularLight = lightSum.color;
    #else
        half3 ambient = env * _SHLightWeight;
        half directional = lerp(0.5, 1.0, saturate(_SHLightDirectionWeight));
        half3 lighting = ambient + lightSum.color * surfaceLight * directional;
        half3 specularLight = lightSum.color * directional;
    #endif

    half3 specular = _SpecularColor.rgb
        * SBSBlinnSpecular(N, L, V, lerp(16.0, _SpecularPower, saturate(1.0 - _Roughness)))
        * specularLight;

    half coatTintWeight = lerp(0.25, 1.0 - diffuse, saturate((half)_SurfaceShadowEnabled)) * _TransmissionStrength;
    half grain = SBSPaperGrain(sd.uv * _PaperGrainScale);
    half3 paper = printedColor * (1.0 + grain * _PaperGrain) * lighting;
    half3 clearBody = _TransmissionColor.rgb * (0.18 + 0.30 * SBSThinLuminance(lighting));
    half3 body = lerp(clearBody, paper, max(printCoverage, refractedCoverage * 0.75));
    half3 coat = lerp(body, body * 0.86 + _TransmissionColor.rgb * (0.14 + coatTintWeight * 0.25), _Opacity);
    half3 internalLight = _TransmissionColor.rgb * (0.10 + _InternalLight * grazing) * _Thickness;
    half3 volumeEdge = _TransmissionColor.rgb * grazing * (0.18 + 0.42 * _Thickness);
    half3 edge = _EdgeColor.rgb * contour * (0.35 + 0.65 * fresnel) * _EdgeIntensity;
    half3 highlight = specular * _SpecularIntensity
        * (0.15 + 0.85 * saturate(contour + fresnel * 0.25));

    #ifdef UNITY_PASS_FORWARDADD
        sd.col.rgb = paper * printCoverage;
    #else
        sd.col.rgb = coat + internalLight + volumeEdge + edge + highlight;
    #endif
    sd.col.a = 1.0;

    __SC_PHASE_shade__
    __SC_PHASE_reflection__
    __SC_PHASE_add__
    __SC_PHASE_postpixel__

    #ifdef UNITY_PASS_FORWARDADD
        UNITY_APPLY_FOG_COLOR(i.fogCoord, sd.col, fixed4(0,0,0,0));
    #else
        UNITY_APPLY_FOG(i.fogCoord, sd.col);
    #endif

    return sd.col;
}

#endif // SABASHADER_ACRYLIC2D_FRAGMENT_INCLUDED
