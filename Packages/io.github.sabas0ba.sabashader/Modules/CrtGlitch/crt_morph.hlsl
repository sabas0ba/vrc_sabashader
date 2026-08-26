{
    if (_Amount > 0.0 && _Glitch > 0.0 && _Tearing > 0.0)
    {
        // 頂点を帯ごとに横へずらして、映像が裂けたように見せる。
        // ピクセル側の帯とは別の乱数系列なので、位置は一致しない。
        //
        // SBSCrtTear が読むのは glitch / tearing / tearScale / time だけなので、
        // 残りは 0 のままにしてある。
        SBSCrtStyle crtMorphStyle = (SBSCrtStyle)0;
        crtMorphStyle.glitch = _Glitch;
        crtMorphStyle.tearing = _Tearing * _Amount;
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
}
