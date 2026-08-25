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

        half3 crtToVertex = vertex.position - camera.position;

        // camera.forward はビュー行列の 3 行目で、カメラの後ろを向いている。
        // 前にあるものほど大きい正の値にしたいので、向きを入れ替えて取る。
        half crtDepth = dot(camera.position - vertex.position, camera.forward);

        if (crtDepth > 1.0e-3)
        {
            // 画面の中心を 0、端を ±1 とした位置。投影行列の対角成分で
            // 画角ぶんを畳むので、画角を変えても丸みの出方が変わらない。
            half2 crtNdc = half2(
                dot(crtToVertex, camera.right) * UNITY_MATRIX_P._m00,
                dot(crtToVertex, camera.up) * UNITY_MATRIX_P._m11) / crtDepth;

            half2 crtOffset = SBSCrtCurve(crtNdc, crtCurveStyle);

            // 同じ尺度のずらし量をワールドへ戻す。奥ほど同じ幅が長い距離になる。
            vertex.position +=
                camera.right * (crtOffset.x * crtDepth / UNITY_MATRIX_P._m00) +
                camera.up * (crtOffset.y * crtDepth / UNITY_MATRIX_P._m11);
        }
    }
    #endif
}
