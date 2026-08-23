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

    // 積もりの厚み（メートル）。頂点を法線方向へ押し出す量。
    half thickness;

    // 水滴の付着。粒の量と細かさ、法線をどれだけ歪めるか。
    // 濡れて見えるかどうかは、色を暗くするより粒のハイライトで決まる。
    half droplet;
    half dropletScale;
    half dropletBump;

    // したたり（雨だれ・汗）の強さと細かさと速さ。
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

// 付着した水滴。升目ごとに 1 粒置き、中心からの距離で丸い盛り上がりを作る。
// 戻り値の x が高さ、yz が中心からのずれ（法線を歪めるのに使う）。
half3 SBSOverlayDroplet(half2 uv, SBSOverlayStyle st)
{
    half scale = max(st.dropletScale, 1.0e-3);
    half2 grid = uv * scale;
    half2 cell = floor(grid);
    half2 local = frac(grid) - half2(0.5, 0.5);

    // 升目ごとに位置と大きさを散らす。等間隔に並ぶと粒に見えない。
    half2 jitter = half2(
        SBSOverlayHash(cell) - 0.5,
        SBSOverlayHash(cell + half2(37.0, 11.0)) - 0.5) * 0.7;
    half radius = 0.16 + 0.24 * SBSOverlayHash(cell + half2(5.0, 23.0));

    half2 offset = local - jitter;
    half distance = length(offset);
    half fall = saturate(1.0 - distance / max(radius, 1.0e-3));

    // 球冠の断面。縁で急に落ちるので粒の輪郭が立つ。
    half height = fall * fall;
    return half3(height, offset.x * fall, offset.y * fall);
}

// 縦に流れるしたたり。uv.y を下向きに流し、横方向は列ごとに位相をずらす。
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

    // したたりは「足す」のではなく「被覆の形を筋に寄せる」。足すと面の向きで
    // 既に高い被覆に上乗せされて飽和し、筋が見えなくなる。
    half streak = SBSOverlayStreak(uv, st);
    base = lerp(base, streak, saturate(st.streak));

    // 付着した粒は、したたりとは別に上乗せする。粒は面の向きに関係なく付く。
    half droplet = SBSOverlayDroplet(uv, st).x;
    base = saturate(base + droplet * saturate(st.droplet));

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

// 頂点をどれだけ押し出すか。頂点シェーダーから呼ぶ。
//
// ピクセル側と同じ被覆率の計算を使いたいが、頂点シェーダーでは
// マスクテクスチャを引けない。面の向きと頂点カラーだけで決める。
half SBSOverlayDisplacement(half3 N, half3 up, half mask, SBSOverlayStyle st)
{
    half facing = SBSOverlayFacing(N, up, st.upBias);
    half base = facing * saturate(mask);
    half coverage = saturate(SBSOverlayStep(base, st.border, st.blur) * saturate(st.amount));
    return coverage * st.thickness;
}

// 水滴の盛り上がりで法線を歪める。接空間で呼ぶこと。
//
// 濡れて見えるかどうかは、色を暗くするより「粒がハイライトを拾うか」で決まる。
// 法線を歪めておくと、本体側のスペキュラが勝手に粒を光らせてくれる。
half3 SBSOverlayDropletNormal(half3 N, half2 uv, SBSOverlayStyle st)
{
    half3 droplet = SBSOverlayDroplet(uv, st);
    half strength = saturate(st.droplet) * st.dropletBump;

    half3 bumped = half3(
        N.x - droplet.y * strength,
        N.y - droplet.z * strength,
        N.z);

    half len = length(bumped);
    return (len > 1.0e-4) ? (bumped / len) : normalize(N);
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
