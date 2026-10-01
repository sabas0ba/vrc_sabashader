#ifndef SABASHADER_THIN2D_VERTEX_INCLUDED
#define SABASHADER_THIN2D_VERTEX_INCLUDED

void SBSCompressObjectDepth(inout SCVertexData vertex, float depthScale)
{
    if (depthScale >= 0.999) return;
    float3 objectPosition = mul(SC_W2O(), float4(vertex.position, 1.0)).xyz;
    objectPosition.z *= max(depthScale, 0.02);
    vertex.position = mul(SC_O2W(), float4(objectPosition, 1.0)).xyz;
}

#endif // SABASHADER_THIN2D_VERTEX_INCLUDED
