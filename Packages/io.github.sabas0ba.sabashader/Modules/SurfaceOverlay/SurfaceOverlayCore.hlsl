#ifndef SABASHADER_SURFACEOVERLAY_CORE_INCLUDED
#define SABASHADER_SURFACEOVERLAY_CORE_INCLUDED

// =============================================================================
// Surface Overlay core
// -----------------------------------------------------------------------------
// 面の上に「積もる・濡れる・汚れる」を重ねる数式だけを集めたファイル。
// 雨・汗・雪・汚れを 1 つの被覆率(coverage)の計算に集約している。
//
// Unity / レンダーパイプライン / Shader Core のどれにも依存しない。
// HLSL としても、tests/harness/prelude.glsl を前置した GLSL 3.30 core としても
// そのままコンパイルできる部分集合で書いてある。
//
// 編集時のルール（docs/testing.md も参照）:
//   * ベクターは必ず全成分を書く
//   * 行列・テクスチャ・グローバル変数・static は使わない
//   * 使ってよい組み込みは prelude.glsl が用意しているものだけ
//   * スカラーとベクターの暗黙変換に頼らない
// =============================================================================

struct SBSOverlayStyle
{
    // どれだけ乗せるか。0 で完全に無効。
    half amount;

    // 上を向いた面ほど乗る度合い。1 で真上だけ、0 で向きを見ない。
    // 雪や埃は上向きに積もり、汗や汚れは向きを問わないことがある。
    half upBias;

    // 被覆率のしきい値とぼかし。トゥーン塗りと同じ考え方で、
    // 硬く切ると縁がはっきりし、ぼかすとなじむ。
    half border;
    half blur;

    // 濡れの表現。素の色をどれだけ暗く・濃くするか。
    half darken;

    // 積もりの表現。法線をどれだけ上向きに寝かせるか。
    half flatten;

    // 垂れ（雨だれ・汗）の強さと細かさと速さ。
    half streak;
    half streakScale;
    half streakSpeed;

    // 時間。テストから固定値を渡せるようにスタイル側に持たせる。
    half time;
};

// -----------------------------------------------------------------------------
// 補助
// -----------------------------------------------------------------------------

// 0-1 に収まる決定的な擬似乱数。texture を使わずに粒を作るために要る。
half SBSOverlayHash(half2 p)
{
    half3 q = half3(p.x, p.y, p.x + p.y);
    q = frac(half3(q.x * 0.1031, q.y * 0.1030, q.z * 0.0973));
    half d = dot(q, half3(q.y + 33.33, q.z + 33.33, q.x + 33.33));
    q = half3(q.x + d, q.y + d, q.z + d);
    return frac((q.x + q.y) * q.z);
}

// トゥーン塗りと同じ境界。blur が 0 のとき硬い 2 値になる。
half SBSOverlayStep(half value, half border, half blur)
{
    half half_blur = max(blur, 0.0) * 0.5;
    return smoothstep(border - half_blur, border + half_blur + 1.0e-5, value);
}

// -----------------------------------------------------------------------------
// 被覆率
// -----------------------------------------------------------------------------

// 面の向きから「乗りやすさ」を出す。up は面から見た重力の逆向き。
half SBSOverlayFacing(half3 N, half3 up, half upBias)
{
    half facing = dot(normalize(N), normalize(up)) * 0.5 + 0.5;
    return lerp(1.0, facing, saturate(upBias));
}

// 縦に流れる筋。雨だれと汗はこれで向きが出る。
// uv.y を下向きに流し、横方向は列ごとに位相をずらす。
half SBSOverlayStreak(half2 uv, SBSOverlayStyle st)
{
    half scale = max(st.streakScale, 1.0e-3);
    half column = floor(uv.x * scale);
    half phase = SBSOverlayHash(half2(column, 17.0));

    half travel = uv.y * scale - st.time * st.streakSpeed * (0.5 + phase);
    half drop = frac(travel + phase);

    // 頭が濃く尾を引く形。pow で先端に寄せる。
    half body = pow(saturate(1.0 - drop), 3.0);
    half width = abs(frac(uv.x * scale) - 0.5) * 2.0;
    half across = saturate(1.0 - width);

    return saturate(body * across);
}

// 最終的な被覆率。mask は頂点カラーやマスクテクスチャから来る 0-1。
half SBSOverlayCoverage(half3 N, half3 up, half mask, half2 uv, SBSOverlayStyle st)
{
    half facing = SBSOverlayFacing(N, up, st.upBias);
    half base = facing * saturate(mask);

    // 垂れは「足す」のではなく「被覆の形を筋に寄せる」。足すと面の向きで
    // 既に高い被覆に上乗せされて飽和し、筋が見えなくなる。
    half streak = SBSOverlayStreak(uv, st);
    base = lerp(base, streak, saturate(st.streak));

    return saturate(SBSOverlayStep(base, st.border, st.blur) * saturate(st.amount));
}

// -----------------------------------------------------------------------------
// 適用
// -----------------------------------------------------------------------------

// 乗せる色を混ぜる。
//
// tint は「色をどれだけ置き換えるか」。雪や汚れは 1 で置き換え、
// 雨や汗は 0 にして darken だけを効かせる。両者を分けていないと、
// 完全に覆われた時点で色が置き換わり、濡れの沈みが見えなくなる。
half3 SBSOverlayAlbedo(half3 albedo, half3 overlay, half tint, half coverage, SBSOverlayStyle st)
{
    half3 wet = albedo * lerp(1.0, 0.45, saturate(st.darken) * coverage);
    return lerp(wet, overlay, coverage * saturate(tint));
}

// 積もったぶん法線を上向きに寝かせる。雪の丸みはこれで出る。
half3 SBSOverlayNormal(half3 N, half3 up, half coverage, SBSOverlayStyle st)
{
    half3 target = normalize(up);
    half t = saturate(st.flatten) * coverage;
    half3 blended = lerp(normalize(N), target, t);

    half len = length(blended);
    return (len > 1.0e-4) ? (blended / len) : normalize(N);
}

#endif // SABASHADER_SURFACEOVERLAY_CORE_INCLUDED
