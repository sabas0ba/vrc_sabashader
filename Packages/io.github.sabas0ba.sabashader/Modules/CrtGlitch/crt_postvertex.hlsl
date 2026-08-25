{
    // 画面の丸み。ブラウン管の面が丸いぶん、外側ほど像が外へ膨らむ。
    //
    // 画面を撮り直せないので、絵ではなくモデルの頂点を動かして代用する。
    // したがって次の副作用がある。
    //   - 三角形の間は直線で結ばれるので、粗いメッシュは曲がらない
    //   - 頂点が実際に動くので、他のオブジェクトとの位置関係が崩れる
    //
    // 影のパスでは効かせない。曲がりは見る人の画面に対して決まるので、ライトから
    // 見た空間で同じ式を通しても意味が無い。そのぶん、影の形は曲がる前のまま残る。
    #ifndef SC_PASS_NON_VIEW
    if (_Amount > 0.0 && _Curvature > 0.0)
    {
        SBSCrtStyle crtCurveStyle = (SBSCrtStyle)0;
        crtCurveStyle.curvature = _Curvature * _Amount;

        // Unity が現在の眼に用意した投影行列で画面位置を求める。投影中心のずれと
        // 透視・平行投影の違いを行列側へ任せるため、対角成分と深度から組み立てない。
        float4 crtViewPosition = mul(UNITY_MATRIX_V, float4(vertex.position, 1.0));
        float4 crtClipPosition = mul(unity_CameraProjection, crtViewPosition);

        if (abs(crtClipPosition.w) > 1.0e-6)
        {
            float2 crtNdc = crtClipPosition.xy / crtClipPosition.w;
            half2 crtOffset = SBSCrtCurve(crtNdc, crtCurveStyle);

            // clip の z と w は保持したまま xy だけを曲げ、対応する逆投影行列で
            // ビュー空間へ戻す。非対称投影と平行投影でも同じ経路を通る。
            crtClipPosition.xy += crtOffset * crtClipPosition.w;
            float4 crtCurvedView = mul(unity_CameraInvProjection, crtClipPosition);

            if (abs(crtCurvedView.w) > 1.0e-6)
            {
                crtCurvedView /= crtCurvedView.w;
                float4 crtCurvedWorld = mul(UNITY_MATRIX_I_V, crtCurvedView);
                vertex.position = crtCurvedWorld.xyz / crtCurvedWorld.w;
            }
        }
    }
    #endif
}
