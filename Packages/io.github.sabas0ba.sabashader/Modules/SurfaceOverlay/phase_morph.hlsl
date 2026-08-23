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
    morphStyle.dropletSize = _DropletSize;
    morphStyle.dropletVariance = _DropletVariance;
    morphStyle.mobility = _Mobility;
    morphStyle.streak = _Streak;
    morphStyle.streakSpeed = _StreakSpeed;
    morphStyle.time = SCTime();

    half3 morphUp = half3(0.0, 1.0, 0.0);
    half morphMask = (_VertexMaskChannel < 4) ? vertex.color[_VertexMaskChannel] : 1.0;
    half morphAmount = SBSOverlayDisplacement(vertex.N, morphUp, morphMask, morphStyle);

    // 押し出す向き。1 で真上、0 で面の法線。
    //
    // 雪は重力で積もるので、既定では真上寄りにしてある。法線方向に押し出すと、
    // 傾いた面では厚みが面から生えたように見える。
    //
    // なお **頂点変位だけでは積もりの縁を丸められない**。丸めるには
    // ジオメトリを足す必要があるが、Shader Core のモジュールはパスを
    // 追加できず、テッセレーションのフックも無い。縁をなだらかにしたい
    // 場合は、頂点カラー（_VertexMaskChannel）で厚みを落とすか、
    // モデル側に縁のジオメトリを用意してください。
    half3 morphDirection = normalize(lerp(normalize(vertex.N), morphUp, saturate(_ThicknessUp)));
    vertex.position += morphDirection * morphAmount;
}
