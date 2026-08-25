{
    // 頂点を帯ごとに横へずらして、映像が裂けたように見せる。
    // ピクセル側の帯とは別の乱数系列なので、位置は一致しない。
    SBSCrtStyle crtMorphStyle;
    crtMorphStyle.amount = _Amount;
    crtMorphStyle.scanline = _Scanline;
    crtMorphStyle.scanlinePitch = _ScanlinePitch;
    crtMorphStyle.mask = _Mask;
    crtMorphStyle.maskPitch = _MaskPitch;
    crtMorphStyle.roll = _Roll;
    crtMorphStyle.rollSpeed = _RollSpeed;
    crtMorphStyle.noise = _Noise;
    crtMorphStyle.noiseScale = _NoiseScale;
    crtMorphStyle.aberration = _Aberration;
    crtMorphStyle.glitch = _Glitch;
    crtMorphStyle.glitchScale = _GlitchScale;
    crtMorphStyle.glitchShift = _GlitchShift;
    crtMorphStyle.glitchColor = _GlitchColor;
    crtMorphStyle.vignette = _Vignette;
    crtMorphStyle.tearing = _Tearing;
    crtMorphStyle.tearScale = _TearScale;
    crtMorphStyle.time = SCTime();

    // 帯はモデルの高さで切る。ワールドの高さで切ると、アバターが上下に
    // 動いたときに裂け目が体の上を滑る。
    half3 crtUp = half3(0.0, 1.0, 0.0);
    half crtHeight = dot(vertex.position - headBone.position, crtUp);

    // ずらす向き。視線と上方向から作るので、画面に対して水平に裂ける。
    // 真上・真下から見ているときは向きが定まらないのでワールドの X に落とす。
    half3 crtSide = cross(crtUp, vertex.V);
    half crtSideLength = length(crtSide);
    crtSide = (crtSideLength > 1.0e-4) ? (crtSide / crtSideLength) : half3(1.0, 0.0, 0.0);

    vertex.position += crtSide * SBSCrtTear(crtHeight, crtMorphStyle);
}
