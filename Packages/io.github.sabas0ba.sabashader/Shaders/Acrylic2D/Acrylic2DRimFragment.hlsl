#ifndef SABASHADER_ACRYLIC2D_RIM_FRAGMENT_INCLUDED
#define SABASHADER_ACRYLIC2D_RIM_FRAGMENT_INCLUDED

half4 frag(v2f i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    clip(_RimWidth - 1.0e-4);

    half4 color = half4(_EdgeColor.rgb * _EdgeIntensity, saturate(_Opacity + _Thickness * 0.25));
    UNITY_APPLY_FOG(i.fogCoord, color);
    return color;
}

#endif // SABASHADER_ACRYLIC2D_RIM_FRAGMENT_INCLUDED
