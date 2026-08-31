SC_float(_Amount, 0, [SCRange(0,1)], "__Amount", "__AmountDesc")
SC_uint(_UVChannel, 0, [SCEnum(UV0,0,UV1,1,UV2,2,UV3,3)], "__UVChannel", "__UVChannelDesc")
SC_float(_Depth, 0.006, [SCRange(0,0.03)], "__Depth", "__DepthDesc")
SC_float(_Bulge, 0.2, [SCRange(0,1)], "__Bulge", "__BulgeDesc")
SC_float(_NormalStrength, 1, [SCRange(0,4)], "__NormalStrength", "__NormalStrengthDesc")
SC_Foldout(__Point0)
SC_float4(_Point0, (0.35,0.55,0.08,0.08), [], "__Point", "__PointDesc")
SC_float(_Pressure0, 0, [SCRange(0,1)], "__Pressure", "__PressureDesc")
SC_FoldoutEnd
SC_Foldout(__Point1)
SC_float4(_Point1, (0.65,0.55,0.08,0.08), [], "__Point", "__PointDesc")
SC_float(_Pressure1, 0, [SCRange(0,1)], "__Pressure", "__PressureDesc")
SC_FoldoutEnd
SC_Foldout(__Point2)
SC_float4(_Point2, (0.35,0.40,0.08,0.08), [], "__Point", "__PointDesc")
SC_float(_Pressure2, 0, [SCRange(0,1)], "__Pressure", "__PressureDesc")
SC_FoldoutEnd
SC_Foldout(__Point3)
SC_float4(_Point3, (0.65,0.40,0.08,0.08), [], "__Point", "__PointDesc")
SC_float(_Pressure3, 0, [SCRange(0,1)], "__Pressure", "__PressureDesc")
SC_FoldoutEnd
