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
    overlayStyle.streak = _Streak;
    overlayStyle.streakScale = _StreakScale;
    overlayStyle.streakSpeed = _StreakSpeed;
    overlayStyle.time = SCTime();

    half overlayMask = (_MaskChannel < 4) ? sd.mask[_MaskChannel] : 1.0;
    float2 overlayUV = sd.uv * _Texture_ST.xy + _Texture_ST.zw;

    half overlayCoverage = SBSOverlayCoverage(
        vertex.N, worldUp, overlayMask, overlayUV, overlayStyle);

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
    sd.N = SBSOverlayDropletNormal(sd.N, overlayUV, overlayStyle);
    sd.N_detail = SBSOverlayDropletNormal(sd.N_detail, overlayUV, overlayStyle);
}
