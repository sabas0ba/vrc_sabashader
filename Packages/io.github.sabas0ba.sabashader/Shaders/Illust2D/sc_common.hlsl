#ifndef SABASHADER_ILLUST2D_COMMON_INCLUDED
#define SABASHADER_ILLUST2D_COMMON_INCLUDED

// Shader Core の BIRP パス（birp_forward / birp_forwardadd / birp_shadowcaster）が
// #include "sc_common.hlsl" で読み込む共通部分。
// ここで SCCustomData / SCVertexMorph / SCVertexPost / SCPixelClip を定義する必要がある。

#include "Illust2DCore.hlsl"

// 頂点・ピクセル間、およびモジュール間で共有する追加データ
struct SCCustomData
{
    half shadingFactor; // 影の落ち具合 (1 = 完全に光が当たっている)
    half outlineMask;   // アウトライン幅マスク
};

// -----------------------------------------------------------------------------
// プロパティを使うヘルパー
// -----------------------------------------------------------------------------

float2 SBSBaseUV(float2 uv)
{
    return uv * _BaseTexture_ST.xy + _BaseTexture_ST.zw;
}

// channel が 0-3 ならその成分、4 以上なら 1.0 を返す
half SBSMaskChannel(half4 mask, uint channel)
{
    return (channel < 4) ? mask[channel] : 1.0;
}

// マテリアルのスタイル設定をコア構造体に詰め替える
SBSStyle SBSGetStyle()
{
    SBSStyle st;

    st.shadeBorder1 = _ShadeBorder1;
    st.shadeBlur1 = _ShadeBlur1;
    st.shade1Color = _Shade1Color.rgb;
    st.shade1HueShift = _Shade1HueShift;
    st.shade1Saturation = _Shade1Saturation;
    st.shade1Value = _Shade1Value;

    st.shadeBorder2 = min(_ShadeBorder2, _ShadeBorder1);
    st.shadeBlur2 = _ShadeBlur2;
    st.shade2Color = _Shade2Color.rgb;
    st.shade2HueShift = _Shade2HueShift;
    st.shade2Saturation = _Shade2Saturation;
    st.shade2Value = _Shade2Value;

    st.shadeSteps = _ShadeSteps;
    st.shadowStrength = _ShadowStrength;

    st.specularColor = _SpecularColor.rgb;
    st.specularBorder = _SpecularBorder;
    st.specularBlur = _SpecularBlur;
    st.specularSmoothness = _SpecularSmoothness;

    st.rimColor = _RimColor.rgb;
    st.rimBorder = _RimBorder;
    st.rimBlur = _RimBlur;
    st.rimLightAlign = _RimLightAlign;

    st.lightMinLimit = _LightMinLimit;
    st.lightMaxLimit = _LightMaxLimit;
    st.monochromeLighting = _MonochromeLighting;
    st.asUnlit = _AsUnlit;

    st.saturation = _Saturation;
    st.contrast = _Contrast;

    return st;
}

// -----------------------------------------------------------------------------
// Shader Core が要求するフック
// -----------------------------------------------------------------------------

void SCVertexMorph(inout SCVertexData vertex, SCPositionAndDirection camera, SCPositionAndDirection head, SCPositionAndDirection headBone)
{
    __SC_PHASE_morph__
}

void SCVertexPost(inout SCVertexData vertex, SCPositionAndDirection camera, SCPositionAndDirection head, SCPositionAndDirection headBone, half3 L)
{
    __SC_PHASE_postvertex__

    #ifdef SBS_PASS_OUTLINE
        half vertexMask = SBSMaskChannel(vertex.color, _OutlineVertexColorChannel);
        half width = SBSOutlineWidth(_OutlineWidth * 0.01, vertexMask, vertex.cameraDepth, _OutlineFixedWidth);
        if (_OutlineEnabled == 0) width = 0.0;
        vertex.position += normalize(vertex.N) * width;
    #endif
}

void SCVertexPost(inout SCVertexData vertex, SCPositionAndDirection camera, SCPositionAndDirection head, SCPositionAndDirection headBone)
{
    SCVertexPost(vertex, camera, head, headBone, half3(0.0, 0.0, 0.0));
}

void SCPixelClip(v2f i, bool isFront, float tangentDir)
{
    if (_AlphaMode == 1)
    {
        half alpha = SCSample(_BaseTexture, sampler_BaseTexture, SBSBaseUV(i.uv[0].xy)).a * _BaseColor.a;
        clip(alpha - _Cutoff);
    }
}

#endif // SABASHADER_ILLUST2D_COMMON_INCLUDED
