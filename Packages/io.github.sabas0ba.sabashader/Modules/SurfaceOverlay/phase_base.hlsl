{
    // base フェーズでは sd.N はまだ tangent 空間。面の向きの判定には
    // ワールド空間の vertex.N を使い、法線を寝かせるときは
    // ワールドの上方向を tangent 空間へ持ち込む。
    half3 worldUp = half3(0.0, 1.0, 0.0);

    SBSOverlayStyle overlayStyle;
    overlayStyle.amount = _Amount;
    overlayStyle.upBias = _UpBias;
    overlayStyle.border = _Border;
    overlayStyle.blur = _Blur;
    overlayStyle.darken = _Darken;
    overlayStyle.flatten = _Flatten;
    overlayStyle.thickness = _Thickness;
    overlayStyle.droplet = _Droplet;
    overlayStyle.dropletScale = _DropletScale;
    overlayStyle.dropletBump = _DropletBump;
    overlayStyle.dropletSize = _DropletSize;
    overlayStyle.dropletVariance = _DropletVariance;
    overlayStyle.mobility = _Mobility;
    overlayStyle.streak = _Streak;
    overlayStyle.streakSpeed = _StreakSpeed;
    overlayStyle.time = SCTime();

    half overlayMask = (_MaskChannel < 4) ? sd.mask[_MaskChannel] : 1.0;
    float2 overlayUV = sd.uv * _Texture_ST.xy + _Texture_ST.zw;

    // 垂れる向きを重力に合わせる。
    //
    // ワールド座標をそのまま使うとモデルが動いたときに模様が滑るので、
    // 模様は UV に固定したまま、**流れる向きだけ**を重力から取る。
    // ワールドの下方向を接空間へ落とすと、UV 上でどちらが下かが分かる。
    half3 downTangent = mul(vertex.TBN, -worldUp);
    half2 flowDirection = downTangent.xy;
    half flowLength = length(flowDirection);
    flowDirection = (flowLength > 1.0e-4) ? (flowDirection / flowLength) : half2(0.0, -1.0);

    // x = 流れに直交する向き、y = 流れに沿う向き
    float2 overlayCoord = float2(
        dot(overlayUV, half2(flowDirection.y, -flowDirection.x)),
        dot(overlayUV, flowDirection));

    half overlayCoverage = SBSOverlayCoverage(
        vertex.N, worldUp, overlayMask, overlayCoord, overlayStyle);

    // サンプラーは Shader Core が用意しているものを使う。モジュールが
    // 自前で宣言すると uniqueID が前置きされ、Unity のインライン
    // sampler の命名規約から外れてしまう。
    half4 overlaySample = SCSampleRepeat(_Texture, overlayUV);
    half3 overlayColor = overlaySample.rgb * _Color.rgb;

    // 色の置き換え量はテクスチャと色のアルファで決める。
    // 雨や汗はアルファを 0 にして、沈みだけを効かせる使い方になる。
    half overlayTint = overlaySample.a * _Color.a;
    sd.albedoAlpha.rgb = SBSOverlayAlbedo(
        sd.albedoAlpha.rgb, overlayColor, overlayTint, overlayCoverage, overlayStyle);

    half3 upTangent = mul(vertex.TBN, worldUp);
    sd.N = SBSOverlayNormal(sd.N, upTangent, overlayCoverage, overlayStyle);
    sd.N_detail = SBSOverlayNormal(sd.N_detail, upTangent, overlayCoverage, overlayStyle);

    // 粒の盛り上がりで法線を歪める。base フェーズの sd.N は接空間なので、
    // xy をずらすだけで粒の向きになる。ここが濡れの見え方を決める。
    sd.N = SBSOverlayDropletNormal(sd.N, overlayCoord, overlayStyle);
    sd.N_detail = SBSOverlayDropletNormal(sd.N_detail, overlayCoord, overlayStyle);
}
