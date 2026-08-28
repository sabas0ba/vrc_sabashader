#ifndef SABASHADER_DEBUG_FRAGMENT_INCLUDED
#define SABASHADER_DEBUG_FRAGMENT_INCLUDED

#include "DebugCore.hlsl"

half4 frag(v2f i, bool isFront : SV_IsFrontFace) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    SCPositionAndDirection camera = SCGetCameraData();
    SCPositionAndDirection head = SCGetHeadData();
    SCPositionAndDirection headBone = SCGetHeadBoneData();
    SCVertexData vertex = FromPixelInput(
        i, camera, head, headBone, unity_WorldTransformParams.w, isFront);

    if (_Mode == 0)
    {
        float3 barycentric = float3(i.uv[3].x, i.uv[3].y, 1.0 - i.uv[3].x - i.uv[3].y);
        half wire = SBSDebugWireMask(barycentric, fwidth(barycentric), _WireWidth);
        return half4(lerp(_BackgroundColor.rgb, _WireColor.rgb, wire), 1.0);
    }

    if (_Mode == 1) return half4(i.customV2f.color.rgb, 1.0);
    if (_Mode == 2) return half4(SBSDebugScalar(i.customV2f.color.a), 1.0);
    if (_Mode == 3) return half4(SBSDebugUV(vertex.uv[0].xy, _CoordinateScale), 1.0);
    if (_Mode == 4) return half4(SBSDebugUV(vertex.uv[1].xy, _CoordinateScale), 1.0);
    if (_Mode == 5) return half4(SBSDebugUV(vertex.uv[2].xy, _CoordinateScale), 1.0);
    if (_Mode == 6) return half4(SBSDebugUV(vertex.uv[3].xy, _CoordinateScale), 1.0);
    if (_Mode == 7) return half4(SBSDebugPosition(vertex.position, _CoordinateScale), 1.0);

    float3 objectPosition = mul(SC_W2O(), float4(vertex.position, 1.0)).xyz;
    if (_Mode == 8) return half4(SBSDebugPosition(objectPosition, _CoordinateScale), 1.0);
    if (_Mode == 9) return half4(SBSDebugSignedVector(normalize(vertex.N)), 1.0);
    if (_Mode == 10) return half4(SBSDebugSignedVector(normalize(vertex.T)), 1.0);
    if (_Mode == 11) return half4(SBSDebugSignedVector(normalize(vertex.B)), 1.0);
    if (_Mode == 12) return isFront ? half4(0.0, 1.0, 0.0, 1.0) : half4(1.0, 0.0, 1.0, 1.0);

    half3 lightDirection = normalize(
        _WorldSpaceLightPos0.xyz - vertex.position * _WorldSpaceLightPos0.w);
    if (_Mode == 13) return half4(SBSDebugSignedVector(lightDirection), 1.0);
    if (_Mode == 14) return half4(SBSDebugHdrColor(_LightColor0.rgb), 1.0);

    UNITY_LIGHT_ATTENUATION(lightAttenuation, i, vertex.position);
    if (_Mode == 15) return half4(SBSDebugScalar(lightAttenuation), 1.0);
    if (_Mode == 16) return half4(SBSDebugSignedVector(normalize(vertex.V)), 1.0);

    half facing = saturate(dot(normalize(vertex.N), normalize(vertex.V)));
    return half4(SBSDebugScalar(facing), 1.0);
}

#endif // SABASHADER_DEBUG_FRAGMENT_INCLUDED
