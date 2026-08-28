#ifndef SABASHADER_DEBUG_GEOMETRY_INCLUDED
#define SABASHADER_DEBUG_GEOMETRY_INCLUDED

[maxvertexcount(3)]
void geom(triangle v2f input[3], inout TriangleStream<v2f> outputStream)
{
    v2f output = input[0];
    if (_Mode == 0) output.uv[3] = float2(1.0, 0.0);
    outputStream.Append(output);

    output = input[1];
    if (_Mode == 0) output.uv[3] = float2(0.0, 1.0);
    outputStream.Append(output);

    output = input[2];
    if (_Mode == 0) output.uv[3] = float2(0.0, 0.0);
    outputStream.Append(output);
    outputStream.RestartStrip();
}

#endif // SABASHADER_DEBUG_GEOMETRY_INCLUDED
