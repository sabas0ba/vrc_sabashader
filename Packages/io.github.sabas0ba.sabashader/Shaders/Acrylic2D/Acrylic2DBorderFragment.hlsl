#ifndef SABASHADER_ACRYLIC2D_BORDER_FRAGMENT_INCLUDED
#define SABASHADER_ACRYLIC2D_BORDER_FRAGMENT_INCLUDED

half4 frag(v2f i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    clip(_WhiteBorderWidth - 1.0e-4);

    half4 color = half4(_WhiteBorderColor.rgb, 1.0);
    UNITY_APPLY_FOG(i.fogCoord, color);
    return color;
}

#endif // SABASHADER_ACRYLIC2D_BORDER_FRAGMENT_INCLUDED
