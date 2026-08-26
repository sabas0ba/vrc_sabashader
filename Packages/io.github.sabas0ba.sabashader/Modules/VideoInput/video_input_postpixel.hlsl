{
    // 外部の RenderTexture を、ライティングまで終えた色へ Unlit として合成する。
    // PixelArt と CrtGlitch が後段に来るよう .scmodule 側で順序を指定している。
    // これにより入力映像にもドット絵化や CRT の走査線をかけられる。
    if (_Amount > 0.0)
    {
        SBSVideoInputStyle videoStyle;
        videoStyle.amount = _Amount;
        videoStyle.tint = _Tint;
        videoStyle.brightness = _Brightness;
        videoStyle.mirrorX = _MirrorX;
        videoStyle.flipY = _FlipY;
        videoStyle.additivePass = 0.0;

        #ifdef UNITY_PASS_FORWARDADD
            // ForwardAdd はライトごとに呼ばれる。入力映像をここでも足すと
            // ライト数だけ明るくなるため、元の加算光を合成率ぶん減らすだけにする。
            videoStyle.additivePass = 1.0;
        #endif

        half2 videoUV = SBSVideoInputUV(vertex.uv[0].xy, _VideoTexture_ST, videoStyle);
        half4 videoSample = SCSampleClamp(_VideoTexture, videoUV);
        sd.col.rgb = SBSVideoInputApply(sd.col.rgb, videoSample, videoStyle);
    }
}
