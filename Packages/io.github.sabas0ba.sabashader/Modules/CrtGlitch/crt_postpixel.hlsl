{
    // 本体が描き終えた色に対してかける。画面を撮り直すことはできないので、
    // 走査線・マスク・ロールバー・ざらつき・砂嵐・周辺の落ち込みは手続きで足し、
    // ずらしが要る帯・升・ゴースト・色ずれは勾配からの 1 次近似で済ませている。
    //
    // ファイル名が phase_postpixel.hlsl でないのは意図的。Shader Core は
    // JSON に書いたフェーズと phase_*.hlsl の両方を無条件に拾うため、
    // 両方に該当すると同じコードが 2 回入る。並び順を afters で指定したいので、
    // JSON 側だけに載せている（tests/test_scmodule.py が守っている）。
    SBSCrtStyle crtStyle;
    crtStyle.amount = _Amount;

    crtStyle.scanline = _Scanline;
    crtStyle.scanlinePitch = _ScanlinePitch;
    crtStyle.mask = _Mask;
    crtStyle.maskPitch = _MaskPitch;
    crtStyle.vignette = _Vignette;
    crtStyle.curvature = _Curvature;

    crtStyle.aberration = _Aberration;

    crtStyle.roll = _Roll;
    crtStyle.rollSpeed = _RollSpeed;
    crtStyle.noise = _Noise;
    crtStyle.noiseScale = _NoiseScale;
    crtStyle.noiseTone = _NoiseTone;
    crtStyle.noiseChroma = _NoiseChroma;
    crtStyle.staticAmount = _Static;
    crtStyle.staticTear = _StaticTear;

    crtStyle.glitch = _Glitch;
    crtStyle.glitchScale = _GlitchScale;
    crtStyle.glitchShift = _GlitchShift;
    crtStyle.glitchColor = _GlitchColor;
    crtStyle.block = _Block;
    crtStyle.blockScale = _BlockScale;
    crtStyle.blockShift = _BlockShift;
    crtStyle.blockCrush = _BlockCrush;

    crtStyle.tearing = _Tearing;
    crtStyle.tearScale = _TearScale;

    crtStyle.time = SCTime();

    // ForwardAdd はライトごとの結果を加算する。光量と無関係な粒や砂嵐を
    // 各パスで足すとライト数に応じて累積するため、ForwardBase だけで加える。
    #ifdef UNITY_PASS_FORWARDADD
        crtStyle.noise = 0.0;
        crtStyle.staticAmount = 0.0;
    #endif

    // 画面ピクセル座標と画面の大きさ。VR の両眼を 1 枚に並べる古い
    // シングルパスでは、片眼ぶんの中心が画面の中心とずれるため、
    // 周辺の落ち込みと色ずれの中心が眼ごとに寄る。
    sd.col.rgb = SBSCrtApply(sd.col.rgb, vertex.positionRaw.xy, _ScreenParams.xy, crtStyle);
}
