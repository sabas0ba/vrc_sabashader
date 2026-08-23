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

    // 粒の大きさと、そのばらつき。
    half dropletSize;
    half dropletVariance;

    // 垂れやすさ。0 で全部その場に留まり、1 で全部が流れ出す。
    // 実際に流れる列は列ごとの乱数で決まるので、途中の値では
    // 「付いたまま動かない粒」と「流れる粒」が混ざる。
    half mobility;

    // したたりの尾の強さと速さ。
    half streak;
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

// 落ちた距離。動き出しはゆっくりで、やがて等速に落ち着く。
//
// 等速のままだと機械的に見える。加速だけだと際限なく速くなる。
// 指数で繋ぐと、初速 0 から滑らかに加速し、時間が経つと
// 傾きが 1 に漸近して等速になる。
half SBSOverlayTravel(half t, half accel)
{
    half a = max(accel, 1.0e-3);
    half decay = exp2(-(t / a) * 1.4426950);
    return t - a * (1.0 - decay);
}

// 付着した水滴。
//
// coord は「重力に沿う向き」を y、それに直交する向きを x に取った座標。
// 呼び出し側が接空間へ落とした重力方向から作るので、モデルが傾いても
// 粒は下へ流れる。
//
// 列ごとに乱数で性質を決める。
//   * 流れるかどうか（mobility との比較）
//   * 大きさ（dropletSize と dropletVariance）
//   * 速さ（大きい粒ほど速く落ちる）
// これで「付いたまま動かない粒」と「流れる粒」が同じ面の上に混ざる。
//
// 戻り値の x が高さ、yz が中心からのずれ（法線を歪めるのに使う）。
half3 SBSOverlayDroplet(half2 coord, SBSOverlayStyle st)
{
    half scale = max(st.dropletScale, 1.0e-3);
    half2 grid = coord * scale;
    half column = floor(grid.x);

    half rollMove = SBSOverlayHash(half2(column, 3.0));
    half rollPhase = SBSOverlayHash(half2(column, 11.0));
    half rollSize = SBSOverlayHash(half2(column, 29.0));

    // 流れる列だけ、時間で下へずらす。止まる列は座標を動かさない。
    //
    // 列ごとに速さと始まりをずらし、さらに落下は初速 0 から加速させる。
    // 全部の粒が同じ速さで一斉に動くと、板が滑っているように見える。
    half moving = step(1.0 - saturate(st.mobility), rollMove);
    half speed = st.streakSpeed * (0.3 + 1.4 * rollSize * rollSize);
    half cycle = frac(st.time * speed * 0.25 + rollPhase) * 4.0;
    half flow = moving * SBSOverlayTravel(cycle, 0.8) * (0.6 + 0.8 * rollMove);

    half2 flowed = half2(grid.x, grid.y + flow);
    half2 cell = floor(flowed);
    half2 local = frac(flowed) - half2(0.5, 0.5);

    // 升目ごとに位置を散らす。等間隔に並ぶと粒に見えない。
    half2 jitter = half2(
        SBSOverlayHash(cell) - 0.5,
        SBSOverlayHash(cell + half2(37.0, 11.0)) - 0.5) * 0.7;

    half variance = SBSOverlayHash(cell + half2(5.0, 23.0)) - 0.5;
    half radius = max(st.dropletSize * (1.0 + variance * st.dropletVariance * 2.0), 0.02);

    half2 offset = local - jitter;
    half distance = length(offset);
    half fall = saturate(1.0 - distance / radius);

    // 球冠の断面。縁で急に落ちるので粒の輪郭が立つ。
    half height = fall * fall;
    return half3(height, offset.x * fall, offset.y * fall);
}

// したたりの尾。流れる列にだけ、粒が通った跡を細く残す。
half SBSOverlayTrail(half2 coord, SBSOverlayStyle st)
{
    half scale = max(st.dropletScale, 1.0e-3);
    half2 grid = coord * scale;
    half column = floor(grid.x);

    half rollMove = SBSOverlayHash(half2(column, 3.0));
    half moving = step(1.0 - saturate(st.mobility), rollMove);

    // 列の中心に近いほど濃い、細い筋。
    // 変数名に line は使えない。HLSL の予約語で、GLSL では通るため
    // 描画テストをすり抜けて Unity のコンパイルで初めて落ちる。
    half across = abs(frac(grid.x) - 0.5) * 2.0;
    half stripe = saturate(1.0 - across / 0.35);

    return saturate(stripe * moving * saturate(st.streak));
}

// 最終的な被覆率。mask は頂点カラーやマスクテクスチャから来る 0-1。
half SBSOverlayCoverage(half3 N, half3 up, half mask, half2 coord, SBSOverlayStyle st)
{
    half facing = SBSOverlayFacing(N, up, st.upBias);
    half base = facing * saturate(mask);

    // 粒と尾は面の向きに関係なく付くので、面の被覆に上乗せする。
    half droplet = SBSOverlayDroplet(coord, st).x * saturate(st.droplet);
    half trail = SBSOverlayTrail(coord, st);
    base = saturate(base + max(droplet, trail));

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

    // ピクセル側と同じ硬いしきい値を使うと、隣り合う頂点が 0 と最大厚みに
    // 振り切れて面が三角形に割れて見える。頂点はメッシュの粗さでしか
    // 標本化できないので、変位はなだらかにする。
    half soft = SBSOverlayStep(base, st.border, max(st.blur, 0.45));

    // 縁で急に切れると断面が立って見える。両端で傾きが 0 になる形にすると、
    // 積もりの縁が丸まって「切り口」に見えなくなる。
    half profile = soft * soft * (3.0 - 2.0 * soft);
    return profile * saturate(st.amount) * st.thickness;
}

// 水滴の盛り上がりで法線を歪める。接空間で呼ぶこと。
//
// 濡れて見えるかどうかは、色を暗くするより「粒がハイライトを拾うか」で決まる。
// 法線を歪めておくと、本体側のスペキュラが勝手に粒を光らせてくれる。
half3 SBSOverlayDropletNormal(half3 N, half2 coord, SBSOverlayStyle st)
{
    half3 droplet = SBSOverlayDroplet(coord, st);
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
