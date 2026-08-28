#ifndef SABASHADER_DEBUG_COMMON_INCLUDED
#define SABASHADER_DEBUG_COMMON_INCLUDED

struct SCCustomData
{
    half unused;
};

void SCVertexMorph(
    inout SCVertexData vertex,
    SCPositionAndDirection camera,
    SCPositionAndDirection head,
    SCPositionAndDirection headBone)
{
}

void SCVertexPost(
    inout SCVertexData vertex,
    SCPositionAndDirection camera,
    SCPositionAndDirection head,
    SCPositionAndDirection headBone,
    half3 L)
{
}

void SCVertexPost(
    inout SCVertexData vertex,
    SCPositionAndDirection camera,
    SCPositionAndDirection head,
    SCPositionAndDirection headBone)
{
    SCVertexPost(vertex, camera, head, headBone, half3(0.0, 0.0, 0.0));
}

void SCCustomV2FFunc(
    inout v2f output,
    SCVertexData vertex,
    SCPositionAndDirection camera,
    SCPositionAndDirection head,
    SCPositionAndDirection headBone)
{
    output.customV2f.color = vertex.color;
}

void SCPixelClip(v2f i, bool isFront, float tangentDir)
{
}

#endif // SABASHADER_DEBUG_COMMON_INCLUDED
