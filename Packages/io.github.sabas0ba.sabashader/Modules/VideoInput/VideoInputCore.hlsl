#ifndef SABASHADER_VIDEOINPUT_CORE_INCLUDED
#define SABASHADER_VIDEOINPUT_CORE_INCLUDED

// =============================================================================
// Video Input core
// -----------------------------------------------------------------------------
// 外部テクスチャを UV で引き、シェーダー本体が描き終えた色へ Unlit として
// 合成する数式。テクスチャを引く処理だけは phase 側に置き、UV と合成は
// Unity / Shader Core に依存しない形で回帰テストする。
//
// 編集時のルール（docs/testing.md も参照）:
//   * ベクターは必ず全成分を書く
//   * 行列・テクスチャ・グローバル変数・static は使わない
//   * 使ってよい組み込みは prelude.glsl が用意しているものだけ
//   * スカラーとベクターの暗黙変換に頼らない
// =============================================================================

struct SBSVideoInputStyle
{
    // 入力映像への置き換え量。0 で本体の色をそのまま残す。
    half amount;

    // RGB は入力映像へ掛ける色、A は合成率へ掛ける値。
    half4 tint;
    half brightness;

    // RenderTexture や UV の向きが素材と合わない場合の反転。
    half mirrorX;
    half flipY;

    // ForwardAdd では入力映像を再度足さず、元の加算光だけを減衰する。
    half additivePass;
};

half2 SBSVideoInputUV(half2 uv, half4 scaleOffset, SBSVideoInputStyle st)
{
    // 反転は UV0 の 0.5 を中心に行い、その後で Tiling / Offset を適用する。
    // 逆順にすると、Offset が中央揃えでないときに参照範囲そのものが移動する。
    half2 oriented = uv;
    oriented.x = lerp(oriented.x, 1.0 - oriented.x, saturate(st.mirrorX));
    oriented.y = lerp(oriented.y, 1.0 - oriented.y, saturate(st.flipY));
    return oriented * scaleOffset.xy + scaleOffset.zw;
}

half SBSVideoInputOpacity(half sampleAlpha, SBSVideoInputStyle st)
{
    return saturate(st.amount) * saturate(st.tint.a) * saturate(sampleAlpha);
}

half3 SBSVideoInputApply(half3 base, half4 video, SBSVideoInputStyle st)
{
    half opacity = SBSVideoInputOpacity(video.a, st);

    // ForwardAdd の出力は後で ForwardBase へ加算される。ここで映像を足すと
    // ライト数に比例して増えるため、元の光だけを (1-opacity) 倍して返す。
    if (st.additivePass > 0.5)
        return base * (1.0 - opacity);

    half3 source = video.rgb * st.tint.rgb * max(st.brightness, 0.0);
    return lerp(base, source, opacity);
}

#endif // SABASHADER_VIDEOINPUT_CORE_INCLUDED
