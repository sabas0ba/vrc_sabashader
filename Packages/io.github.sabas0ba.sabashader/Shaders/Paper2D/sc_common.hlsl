#ifndef SABASHADER_PAPER2D_COMMON_INCLUDED
#define SABASHADER_PAPER2D_COMMON_INCLUDED

#include "../Thin2D/Thin2DCore.hlsl"
#include "../Thin2D/Thin2DVertex.hlsl"

struct SCCustomData
{
    half shadingFactor;
};

float2 SBSBaseUV(float2 uv)
{
    return uv * _BaseTexture_ST.xy + _BaseTexture_ST.zw;
}

void SCVertexMorph(inout SCVertexData vertex, SCPositionAndDirection camera, SCPositionAndDirection head, SCPositionAndDirection headBone)
{
    __SC_PHASE_morph__
    SBSCompressObjectDepth(vertex, _DepthScale);
}

void SCVertexPost(inout SCVertexData vertex, SCPositionAndDirection camera, SCPositionAndDirection head, SCPositionAndDirection headBone, half3 L)
{
    __SC_PHASE_postvertex__

    #ifdef SBS_PASS_OUTLINE
        vertex.position += normalize(vertex.N) * _OutlineWidth;
    #endif
    #ifdef SBS_PASS_BORDER
        vertex.position += normalize(vertex.N) * (_OutlineWidth + _WhiteBorderWidth);
    #endif
}

void SCVertexPost(inout SCVertexData vertex, SCPositionAndDirection camera, SCPositionAndDirection head, SCPositionAndDirection headBone)
{
    SCVertexPost(vertex, camera, head, headBone, half3(0.0, 0.0, 0.0));
}

void SCPixelClip(v2f i, bool isFront, float tangentDir)
{
    __SC_PHASE_pixelclip__

    #ifdef SBS_PASS_SHADOW
        clip((half)_CastShadowEnabled - 0.5);
    #endif

    if (_AlphaMode == 1)
    {
        half alpha = SCSample(_BaseTexture, sampler_BaseTexture, SBSBaseUV(i.uv[0].xy)).a * _BaseColor.a;
        clip(alpha - _Cutoff);
    }
}

#endif // SABASHADER_PAPER2D_COMMON_INCLUDED
