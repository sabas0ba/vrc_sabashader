#ifndef SABASHADER_PAPER2D_OUTLINE_FRAGMENT_INCLUDED
#define SABASHADER_PAPER2D_OUTLINE_FRAGMENT_INCLUDED

half4 frag(v2f i, bool isFront : SV_IsFrontFace) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    clip(_OutlineWidth - 1.0e-4);
    SCPositionAndDirection camera = SCGetCameraData();
    SCPositionAndDirection head = SCGetHeadData();
    SCPositionAndDirection headBone = SCGetHeadBoneData();
    SCVertexData vertex = FromPixelInput(i, camera, head, headBone, unity_WorldTransformParams.w, isFront);
    half alpha = SCSample(_BaseTexture, sampler_BaseTexture, SBSBaseUV(vertex.uv[0].xy)).a * _BaseColor.a;
    if (_AlphaMode == 1) clip(alpha - _Cutoff);

    half4 color = half4(_OutlineColor.rgb, 1.0);
    UNITY_APPLY_FOG(i.fogCoord, color);
    return color;
}

#endif // SABASHADER_PAPER2D_OUTLINE_FRAGMENT_INCLUDED
