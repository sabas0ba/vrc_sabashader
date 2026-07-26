#ifndef SABASHADER_ILLUST2D_OUTLINE_FRAGMENT_INCLUDED
#define SABASHADER_ILLUST2D_OUTLINE_FRAGMENT_INCLUDED

// 反転ハル方式のアウトライン用ピクセルシェーダー。
// 頂点側の押し出しは sc_common.hlsl の SCVertexPost が SBS_PASS_OUTLINE 定義時に行う。
// ライティングはフルの合成を通さず、L0 相当の環境光 + メインライトだけを使う。

half4 frag(v2f i, bool isFront : SV_IsFrontFace) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    if (_OutlineEnabled == 0) discard;

    SCPositionAndDirection camera = SCGetCameraData();
    SCPositionAndDirection head = SCGetHeadData();
    SCPositionAndDirection headBone = SCGetHeadBoneData();
    SCVertexData vertex = FromPixelInput(i, camera, head, headBone, unity_WorldTransformParams.w, isFront);

    float2 uv = SBSBaseUV(vertex.uv[0].xy);
    half4 albedo = saturate(SCSample(_BaseTexture, sampler_BaseTexture, uv) * _BaseColor);

    if (_AlphaMode == 1) clip(albedo.a - _Cutoff);

    SBSStyle style = SBSGetStyle();

    half3 outlineCol = SBSOutlineColor(
        albedo.rgb,
        _OutlineColor.rgb,
        _OutlineAlbedoBlend,
        _OutlineHueShift,
        _OutlineSaturation,
        _OutlineValue);

    half3 ambient = max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)).rgb, half3(0.0, 0.0, 0.0));
    half3 illum = SBSLimitLight(_LightColor0.rgb + ambient, style);

    half4 col = half4(SBSGrade(outlineCol * illum, style), albedo.a);

    UNITY_APPLY_FOG(i.fogCoord, col);
    return col;
}

#endif // SABASHADER_ILLUST2D_OUTLINE_FRAGMENT_INCLUDED
