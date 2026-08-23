{
    // 積もりの厚みを頂点の押し出しで作る。アウトラインのパスが
    // vertex.position を動かしているのと同じやり方。
    //
    // ここではマスクテクスチャを引けないので、被覆率は面の向きと
    // 頂点カラーだけで決める。ピクセル側とはマスクの出どころが違うため、
    // マスクテクスチャで細かく抜くと厚みと見た目が少しずれる。
    SBSOverlayStyle morphStyle;
    morphStyle.amount = _Amount;
    morphStyle.upBias = _UpBias;
    morphStyle.border = _Border;
    morphStyle.blur = _Blur;
    morphStyle.darken = _Darken;
    morphStyle.flatten = _Flatten;
    morphStyle.thickness = _Thickness;
    morphStyle.droplet = _Droplet;
    morphStyle.dropletScale = _DropletScale;
    morphStyle.dropletBump = _DropletBump;
    morphStyle.streak = _Streak;
    morphStyle.streakScale = _StreakScale;
    morphStyle.streakSpeed = _StreakSpeed;
    morphStyle.time = SCTime();

    half3 morphUp = half3(0.0, 1.0, 0.0);
    half morphMask = (_VertexMaskChannel < 4) ? vertex.color[_VertexMaskChannel] : 1.0;
    half morphAmount = SBSOverlayDisplacement(vertex.N, morphUp, morphMask, morphStyle);

    // 法線方向に押し出しつつ、上向きへ少し寄せると積もりらしくなる。
    half3 morphDirection = normalize(lerp(normalize(vertex.N), morphUp, saturate(_Flatten) * 0.5));
    vertex.position += morphDirection * morphAmount;
}
