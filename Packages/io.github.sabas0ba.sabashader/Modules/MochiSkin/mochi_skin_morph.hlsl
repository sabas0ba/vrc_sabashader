{
    if (_Amount > 0.0 && _Depth > 0.0)
    {
        float2 mochiUV = vertex.uv[0].xy;
        if (_UVChannel == 1) mochiUV = vertex.uv[1].xy;
        if (_UVChannel == 2) mochiUV = vertex.uv[2].xy;
        if (_UVChannel == 3) mochiUV = vertex.uv[3].xy;

        half4 mochiPressure = half4(_Pressure0, _Pressure1, _Pressure2, _Pressure3);
        half mochiHeight = SBSMochiHeight4(
            mochiUV,
            _Point0,
            _Point1,
            _Point2,
            _Point3,
            mochiPressure,
            _Depth,
            _Bulge);
        vertex.position += normalize(vertex.N) * mochiHeight * saturate(_Amount);
    }
}
