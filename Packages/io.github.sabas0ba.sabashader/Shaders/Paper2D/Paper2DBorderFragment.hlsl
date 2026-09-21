#ifndef SABASHADER_PAPER2D_BORDER_FRAGMENT_INCLUDED
#define SABASHADER_PAPER2D_BORDER_FRAGMENT_INCLUDED

half4 frag(v2f i, bool isFront : SV_IsFrontFace) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    clip(_WhiteBorderWidth - 1.0e-4);
    if (_AlphaMode == 1)
    {
        half alpha = SCSample(_BaseTexture, sampler_BaseTexture, SBSBaseUV(i.uv[0].xy)).a * _BaseColor.a;
        clip(alpha - _Cutoff);
    }

    half4 color = half4(_WhiteBorderColor.rgb, 1.0);
    UNITY_APPLY_FOG(i.fogCoord, color);
    return color;
}

#endif // SABASHADER_PAPER2D_BORDER_FRAGMENT_INCLUDED
