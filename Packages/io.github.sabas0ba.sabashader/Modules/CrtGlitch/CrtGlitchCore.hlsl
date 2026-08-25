#ifndef SABASHADER_CRTGLITCH_CORE_INCLUDED
#define SABASHADER_CRTGLITCH_CORE_INCLUDED

// =============================================================================
// CRT / Glitch core
// -----------------------------------------------------------------------------
// ブラウン管の走査線・シャドウマスク・ロールバーと、映像が乱れたときの
// 帯のずれ・色ずれ・ざらつきを、本体が描き終えた色に対してかける数式。
//
// 画面を撮り直さずにどこまでやるか:
//   モジュールは隣接ピクセルを読めないので、画面を歪めたり、ぼかしたり、
//   実際に横へずらしたりはできない。ずらしが要るところ（帯のずれ・色ずれ）は
//   勾配 (ddx/ddy) からの 1 次近似で、ずらした先の色を推定している。
//   面の上で色が滑らかに変わるところではよく合い、シルエットや模様の境目では
//   外れるので、補正量に上限を置いてある。
//
//   勾配は 2x2 のピクセルごとに 1 つしか無いため、2 ピクセルより細かい
//   ずらしは段が付く。走査線・マスク・ロールバー・ざらつき・周辺の落ち込みは
//   ずらしを伴わないので、この制約を受けない。
//
// Unity / レンダーパイプライン / Shader Core のどれにも依存しない。
// HLSL としても、tests/harness/prelude.glsl を前置した GLSL 3.30 core としても
// そのままコンパイルできる部分集合で書いてある。
// =============================================================================

struct SBSCrtStyle
{
    // 効き。0 で完全に無効。
    half amount;

    // 走査線の濃さと間隔（画面ピクセル）。
    half scanline;
    half scanlinePitch;

    // シャドウマスク（RGB の縦縞）の濃さと、縞 1 組の幅（画面ピクセル）。
    half mask;
    half maskPitch;

    // ロールバーの濃さと、画面を流れる速さ（画面 1 枚 / 秒）。
    half roll;
    half rollSpeed;

    // ざらつきの量と、粒の大きさ（画面ピクセル）。
    half noise;
    half noiseScale;

    // 色ずれの幅（画面ピクセル）。画面の外側ほど広がる。
    half aberration;

    // 乱れの出やすさ、帯の高さ（画面ピクセル）、横ずれの幅（画面ピクセル）、
    // 帯でのチャンネル入れ替えの量。
    half glitch;
    half glitchScale;
    half glitchShift;
    half glitchColor;

    // 周辺の落ち込み。
    half vignette;

    // 頂点の裂け幅と、裂ける帯の高さ（どちらもメートル）。
    half tearing;
    half tearScale;

    // 時間。テストから固定値を渡せるようにスタイル側に持たせる。
    half time;
};

// -----------------------------------------------------------------------------
// 補助
// -----------------------------------------------------------------------------

// 0-1 に収まる決定的な擬似乱数。
//
// sin は実装ごとに結果が違うので使わない。座標を無理数由来の係数で
// 2 方向に畳んで小数部を取り、互いを掛け合わせて 2 巡かき混ぜる。
// 係数は 1/pi, ln2, sqrt(2)-1, sqrt(3)-1, 1/phi。
//
// SurfaceOverlay にも同じ構成のものが別名で入っている。モジュールは
// 単体で持ち出せるようにしてあるので、共有ファイルにはしていない。
half SBSCrtHash(half2 p)
{
    half2 q = frac(half2(
        p.x * 0.3183099 + p.y * 0.6931472,
        p.x * 0.4142136 + p.y * 0.7320508));
    q = frac(half2(q.x * (q.y + 61.83), q.y * (q.x + 61.83)));
    q = frac(half2(q.x * (q.y + 61.83), q.y * (q.x + 61.83)));
    return frac(q.x + q.y * 0.6180340);
}

// 画面上でずらした先の色を、勾配からの 1 次近似で推定する。
//
// シルエットや模様の境目では近似が大きく外れるので、補正量に上限を置いて
// 元の色へ寄せる。これをしないと縁に飽和した点が散る。
//
// 乱れた帯はもともと硬い破綻が欲しい効果なので、縁でも切らずに頭打ちにする。
half3 SBSCrtShift(half3 color, half2 offset)
{
    half3 correction = ddx(color) * offset.x + ddy(color) * offset.y;
    half magnitude = length(correction);
    correction *= (magnitude > 1.0) ? (1.0 / magnitude) : 1.0;
    return color + correction;
}

// 同じずらしを、勾配が急なところでは弱めて返す。
//
// 色ずれは「滑らかな面でだけ効いてほしい」効果で、上限で頭打ちにするだけだと
// シルエットに色の輪が残る。0.3 を境に二次で落とすと、滑らかな面（補正量は
// 0.1 に届かない）はほぼ素通りし、縁（補正量が 1 を超える）では消える。
half3 SBSCrtShiftSoft(half3 color, half2 offset)
{
    half3 correction = ddx(color) * offset.x + ddy(color) * offset.y;
    half magnitude = length(correction);
    correction *= 1.0 / (1.0 + magnitude * magnitude * 11.1);
    return color + correction;
}

// -----------------------------------------------------------------------------
// ブラウン管側
// -----------------------------------------------------------------------------

// 走査線。pitch 画面ピクセルごとに 1 本、暗い線が入る。
half SBSCrtScanline(half screenY, SBSCrtStyle st)
{
    half pitch = max(st.scanlinePitch, 2.0);
    half phase = frac(screenY / pitch);

    // 山が線と線の間、谷が線そのもの。
    half wave = 0.5 - 0.5 * cos(phase * 6.2831853);
    return 1.0 - saturate(st.scanline) * (1.0 - wave);
}

// シャドウマスク。R/G/B の縦縞を 1 組 maskPitch 画面ピクセルで並べる。
//
// 縞は 3 つに割り切る。位相をずらした余弦を重ねると、山と山の中間で 2 色が
// 同時に持ち上がり、面全体が黄色や水色に寄って見える（R と G の中間で
// B だけが 0 になるため）。実物のシャドウマスクも縞は分かれている。
//
// 該当する縞を 2.4、隣を 0.3 にすると平均が 1 になり、全体の明るさが変わらない。
half3 SBSCrtMask(half screenX, SBSCrtStyle st)
{
    half pitch = max(st.maskPitch, 3.0);
    half slot = floor(frac(screenX / pitch) * 3.0);

    half3 stripe = half3(
        (slot < 0.5) ? 2.4 : 0.3,
        (slot > 0.5 && slot < 1.5) ? 2.4 : 0.3,
        (slot > 1.5) ? 2.4 : 0.3);

    return lerp(half3(1.0, 1.0, 1.0), stripe, saturate(st.mask));
}

// ロールバー。垂直同期がずれた画面に出る、ゆっくり流れる帯。
//
// 画面の高さに対する割合で位置を決めるので、解像度が変わっても幅が変わらない。
half SBSCrtRoll(half screenV, SBSCrtStyle st)
{
    half position = frac(screenV - st.time * st.rollSpeed);

    // 画面のおよそ 1/3 を占める帯。中心が明るく、周りをわずかに沈ませる。
    half band = saturate(1.0 - abs(position - 0.5) * 6.0);
    return 1.0 + saturate(st.roll) * (band * 0.6 - 0.15);
}

// ざらつき。時間を 24 分の 1 秒に刻んで、静止画としても再現できるようにする。
half SBSCrtNoise(half2 screen, SBSCrtStyle st)
{
    half scale = max(st.noiseScale, 1.0);
    half2 cell = floor(screen / scale);
    half tick = floor(st.time * 24.0);
    return SBSCrtHash(cell + half2(tick, tick * 0.37)) - 0.5;
}

// 周辺の落ち込み。画面の中心で 1、四隅で最も暗い。
half SBSCrtVignette(half2 screenUV, SBSCrtStyle st)
{
    half2 offset = (screenUV - half2(0.5, 0.5)) * 2.0;
    half radius = saturate(dot(offset, offset) * 0.5);
    return 1.0 - saturate(st.vignette) * radius * radius;
}

// 色ずれ。R を進めた先、B を戻した先から取る。G は動かさない。
half3 SBSCrtAberration(half3 color, half2 offset)
{
    half3 forward = SBSCrtShiftSoft(color, offset);
    half3 backward = SBSCrtShiftSoft(color, -offset);
    return half3(forward.r, color.g, backward.b);
}

// -----------------------------------------------------------------------------
// 乱れ側
// -----------------------------------------------------------------------------

// 画面を横に切った帯ごとの状態。x = 出ているか (0 or 1)、y = 帯ごとの乱数。
//
// 時間を 12 分の 1 秒に刻むので、帯は滑らかに動かずに切り替わる。
// 映像の乱れは連続して動かない方がそれらしい。
half2 SBSCrtBand(half screenY, SBSCrtStyle st)
{
    half scale = max(st.glitchScale, 1.0);
    half row = floor(screenY / scale);
    half tick = floor(st.time * 12.0);

    half pick = SBSCrtHash(half2(row, tick));
    half variation = SBSCrtHash(half2(row + 0.5, tick + 7.0));

    // glitch が 0 なら 1 本も出ず、1 ならすべての帯が出る。
    //
    // 変数名に active を使わないこと。GLSL の予約語なので、HLSL では通っても
    // ヘッドレスの描画テストが通らない（HLSL 側の line と対になる例）。
    half visible = step(1.0 - saturate(st.glitch), pick);
    return half2(visible, variation);
}

// 帯でのチャンネル入れ替え。乱数で 2 通りの並べ替えを選ぶ。
half3 SBSCrtChannelSwap(half3 color, half pick, half strength)
{
    half3 rotated = (pick < 0.5)
        ? half3(color.g, color.b, color.r)
        : half3(color.b, color.r, color.g);
    return lerp(color, rotated, saturate(strength));
}

// 頂点の裂け。高さで切った帯ごとの横ずらし量を返す。
//
// 画面ではなくモデルの上下で切っているので、頂点シェーダーから使える。
// 画面に対して水平に裂けて見えるかどうかは、ずらす向きの取り方で決まる。
half SBSCrtTear(half height, SBSCrtStyle st)
{
    half scale = max(st.tearScale, 1.0e-3);
    half row = floor(height / scale);
    half tick = floor(st.time * 12.0);

    half pick = SBSCrtHash(half2(row, tick + 3.0));
    half variation = SBSCrtHash(half2(row + 0.5, tick + 19.0));

    half visible = step(1.0 - saturate(st.glitch), pick);
    return visible * (variation * 2.0 - 1.0) * st.tearing;
}

// -----------------------------------------------------------------------------
// まとめ
// -----------------------------------------------------------------------------

// screen は画面ピクセル座標、resolution は画面の大きさ。
half3 SBSCrtApply(half3 color, half2 screen, half2 resolution, SBSCrtStyle st)
{
    half2 size = max(resolution, half2(1.0, 1.0));
    half2 screenUV = screen / size;

    half3 result = color;

    // 1. 乱れた帯。横にずらし、帯によっては色も入れ替える。
    half2 band = SBSCrtBand(screen.y, st);
    half shift = (band.y * 2.0 - 1.0) * st.glitchShift * band.x;
    result = SBSCrtShift(result, half2(shift, 0.0));
    result = SBSCrtChannelSwap(result, band.y, st.glitchColor * band.x);

    // 2. 色ずれ。画面の中心から外へ向かうほど広げる。
    half2 radial = screenUV - half2(0.5, 0.5);
    half spread = length(radial);
    half2 direction = (spread > 1.0e-4) ? (radial / spread) : half2(1.0, 0.0);
    result = SBSCrtAberration(result, direction * (st.aberration * min(spread * 2.0, 1.0)));

    // 3. 走査線とシャドウマスク
    result *= SBSCrtScanline(screen.y, st);
    result *= SBSCrtMask(screen.x, st);

    // 4. ロールバー
    result *= SBSCrtRoll(screenUV.y, st);

    // 5. ざらつき
    result += SBSCrtNoise(screen, st) * saturate(st.noise);

    // 6. 周辺の落ち込み
    result *= SBSCrtVignette(screenUV, st);

    result = max(result, half3(0.0, 0.0, 0.0));
    return lerp(color, result, saturate(st.amount));
}

#endif // SABASHADER_CRTGLITCH_CORE_INCLUDED
