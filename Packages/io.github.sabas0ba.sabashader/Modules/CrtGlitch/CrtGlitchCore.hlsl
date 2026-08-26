#ifndef SABASHADER_CRTGLITCH_CORE_INCLUDED
#define SABASHADER_CRTGLITCH_CORE_INCLUDED

// =============================================================================
// CRT / Glitch core
// -----------------------------------------------------------------------------
// ブラウン管の走査線・シャドウマスク・ロールバーと、映像が乱れた
// ときの帯のずれ・升の破綻・色ずれ・砂嵐を、本体が描き終えた色に対してかける数式。
//
// 画面を撮り直さずにどこまでやるか:
//   モジュールは隣接ピクセルを読めないので、画面を歪めたり、ぼかしたり、
//   実際に横へずらしたりはできない。ずらしが要るところ（帯・升・色ずれ）は
//   勾配 (ddx/ddy) からの 1 次近似で、ずらした先の色を推定している。
//   面の上で色が滑らかに変わるところではよく合い、シルエットや模様の境目では
//   外れるので、効果ごとに違う抑え方をしてある（SBSCrtShiftFrom と
//   SBSCrtShiftSoftFrom）。
//
//   勾配は 2x2 のピクセルごとに 1 つしか無いため、2 ピクセルより細かい
//   ずらしは段が付く。走査線・マスク・ロールバー・ざらつき・砂嵐・周辺の
//   落ち込みはずらしを伴わないので、この制約を受けない。
//
//   勾配は、色を変換した段の後で取り直す。チャンネル入れ替えや量子化の前の
//   勾配を後段へ渡すと、後段が別の色チャンネルをずらしてしまうため。
//
// できないこと:
//   - りん光の残像。前のフレームが要る
//   - 滲みやグロー。隣接ピクセルの重み付き和が要る
//   - ゴースト。1 次近似では段を重ねても 1 回ずらすのと同じ式に潰れる（後述）
//   - 絵そのものを曲げること。再サンプリングできないため
//
// Unity / レンダーパイプライン / Shader Core のどれにも依存しない。
// HLSL としても、tests/harness/prelude.glsl を前置した GLSL 3.30 core としても
// そのままコンパイルできる部分集合で書いてある。
// =============================================================================

struct SBSCrtStyle
{
    // 効き。0 で完全に無効。
    half amount;

    // ForwardAdd なら 1。加算合成できない砂嵐の生成と色の量子化を止める。
    half additivePass;

    // -- 画面 ----------------------------------------------------------------
    // 走査線の濃さと間隔（画面ピクセル）。
    half scanline;
    half scanlinePitch;

    // シャドウマスク（RGB の縦縞）の濃さと、縞 1 組の幅（画面ピクセル）。
    half mask;
    half maskPitch;

    // 周辺の落ち込み。
    half vignette;

    // 赤と青を逆向きに離す幅（画面ピクセル）。画面の外側ほど広がる。
    half aberration;

    // -- ざらつきと砂嵐 ------------------------------------------------------
    // ロールバーの濃さと、画面を流れる速さ（画面 1 枚 / 秒）。
    half roll;
    half rollSpeed;

    // ざらつきの量と、粒の大きさ（画面ピクセル）。
    half noise;
    half noiseScale;

    // 中間調への寄せ具合と、色まで揺らす量。
    half noiseTone;
    half noiseChroma;

    // 砂嵐で置き換える割合と、置き換える前の横方向の引き裂き幅。
    half staticAmount;
    half staticTear;

    // -- 乱れ ----------------------------------------------------------------
    // 横帯の出やすさ、帯の高さ（画面ピクセル）、横ずれの幅（画面ピクセル）、
    // 帯でのチャンネル入れ替えの量。
    half glitch;
    half glitchScale;
    half glitchShift;
    half glitchColor;

    // 升の破綻の出やすさ、升の大きさ（画面ピクセル）、横ずれの幅
    // （画面ピクセル）、升ごとに色を段へ落とす量。
    half block;
    half blockScale;
    half blockShift;
    half blockCrush;

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

// ずらした先の色を、あらかじめ取っておいた勾配からの 1 次近似で推定する。
//
// シルエットや模様の境目では近似が大きく外れるので、補正量に上限を置いて
// 元の色へ寄せる。これをしないと縁に飽和した点が散る。
//
// 帯と升の破綻はもともと硬い破綻が欲しい効果なので、縁でも切らずに頭打ちにする。
half3 SBSCrtShiftFrom(half3 color, half3 gx, half3 gy, half2 offset)
{
    half3 correction = gx * offset.x + gy * offset.y;
    half magnitude = length(correction);
    correction *= (magnitude > 1.0) ? (1.0 / magnitude) : 1.0;
    return color + correction;
}

// 同じずらしを、勾配が急なところでは弱めて返す。
//
// 色ずれは「滑らかな面でだけ効いてほしい」効果で、上限で頭打ちに
// するだけだとシルエットに色の輪が残る。0.3 を境に二次で落とすと、滑らかな面
// （補正量は 0.1 に届かない）はほぼ素通りし、縁（補正量が 1 を超える）では消える。
half3 SBSCrtShiftSoftFrom(half3 color, half3 gx, half3 gy, half2 offset)
{
    half3 correction = gx * offset.x + gy * offset.y;
    half magnitude = length(correction);
    correction *= 1.0 / (1.0 + magnitude * magnitude * 11.1);
    return color + correction;
}

// 勾配をその場で取る版。単体で使うときの入口。
half3 SBSCrtShift(half3 color, half2 offset)
{
    return SBSCrtShiftFrom(color, ddx(color), ddy(color), offset);
}

half3 SBSCrtShiftSoft(half3 color, half2 offset)
{
    return SBSCrtShiftSoftFrom(color, ddx(color), ddy(color), offset);
}

// チャンネルの並べ替え。乱数で 2 通りから選ぶ。
half3 SBSCrtChannelSwap(half3 color, half pick, half strength)
{
    half3 rotated = (pick < 0.5)
        ? half3(color.g, color.b, color.r)
        : half3(color.b, color.r, color.g);
    return lerp(color, rotated, saturate(strength));
}

// -----------------------------------------------------------------------------
// 画面
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

// 周辺の落ち込み。画面の中心で 1、四隅で最も暗い。
half SBSCrtVignette(half2 screenUV, SBSCrtStyle st)
{
    half2 offset = (screenUV - half2(0.5, 0.5)) * 2.0;
    half radius = saturate(dot(offset, offset) * 0.5);
    return 1.0 - saturate(st.vignette) * radius * radius;
}

// -----------------------------------------------------------------------------
// 色ずれ
// -----------------------------------------------------------------------------

// 色ずれ。R を進めた先、B を戻した先から取る。G は動かさない。
half3 SBSCrtAberration(half3 color, half3 gx, half3 gy, half2 offset)
{
    half3 forward = SBSCrtShiftSoftFrom(color, gx, gy, offset);
    half3 backward = SBSCrtShiftSoftFrom(color, gx, gy, -offset);
    return half3(forward.r, color.g, backward.b);
}

// ゴースト（同じ絵をずらして何段も重ねるもの）は入れていない。
//
// 1 次近似では段を重ねても意味が無いため。i 段目は c + g・d・i になるので、
// 重み w_i で平均すると sum(w_i (c + g・d・i)) / sum(w_i) = c + g・d・(i の加重平均)
// となり、1 回ずらしたのと同じ式に潰れる。段ごとに色を回せば潰れなくなるが、
// それは色が変わるだけで、像が「ずれて重なる」ようには見えない。
//
// 実測でも、色を回さずに 3 段重ねた場合の差は平均 0.15 / 255 しか出なかった。
// 本物のゴーストには別の位置の絵そのものが要るので、画面を撮り直せない
// この仕組みでは作れない。

// -----------------------------------------------------------------------------
// ざらつきと砂嵐
// -----------------------------------------------------------------------------

// ざらつき。時間を 24 分の 1 秒に刻んで、静止画としても再現できるようにする。
//
// noiseTone を上げると中間調へ寄せる。フィルムの粒は明部と暗部で目立たないので、
// 明るさで重みを付けると写真らしくなる。
// noiseChroma を上げると RGB を別々に揺らす。0 では明るさだけが揺れる。
half3 SBSCrtGrain(half3 color, half2 screen, SBSCrtStyle st)
{
    half scale = max(st.noiseScale, 1.0);
    half2 cell = floor(screen / scale);
    half tick = floor(st.time * 24.0);

    half3 grain = half3(
        SBSCrtHash(cell + half2(tick, tick * 0.37)) - 0.5,
        SBSCrtHash(cell + half2(tick + 13.0, tick * 0.71)) - 0.5,
        SBSCrtHash(cell + half2(tick + 29.0, tick * 1.13)) - 0.5);

    half3 mixed = lerp(half3(grain.x, grain.x, grain.x), grain, saturate(st.noiseChroma));

    // 4 * l * (1 - l) は中間調で 1、明部と暗部で 0 になる。
    half lum = dot(saturate(color), half3(0.2126, 0.7152, 0.0722));
    half shaped = lerp(1.0, 4.0 * lum * (1.0 - lum), saturate(st.noiseTone));

    return color + mixed * (saturate(st.noise) * shaped);
}

// 砂嵐。受信が切れかけた画面のように、像をノイズへ置き換える。
//
// 置き換える前に横へ引き裂くと、走査が追いつかずに崩れた画に近づく。
half3 SBSCrtStatic(half3 color, half3 gx, half3 gy, half2 screen, SBSCrtStyle st)
{
    half amount = saturate(st.staticAmount);

    half scale = max(st.noiseScale, 1.0);
    half tick = floor(st.time * 24.0);
    half row = floor(screen.y / max(scale * 2.0, 2.0));

    // 行ごとに横へ引き裂いてから置き換える。
    // 引き裂きにも amount を掛ける。掛けないと、砂嵐を 0 にしていても
    // 引き裂きだけが残る。
    half tear = (SBSCrtHash(half2(row, tick + 5.0)) * 2.0 - 1.0) * st.staticTear * amount;
    half3 torn = SBSCrtShiftFrom(color, gx, gy, half2(tear, 0.0));

    // 砂嵐の生成は光量と無関係なので ForwardBase だけで行う。ForwardAdd では
    // 横裂けと (1 - amount) の減衰だけを適用し、各ライトの寄与を正しく合成する。
    half3 result = torn * (1.0 - amount);
    if (st.additivePass < 0.5)
    {
        half2 cell = floor(screen / scale);
        half level = SBSCrtHash(cell + half2(tick * 1.31, tick));
        result += half3(level, level, level) * amount;
    }

    return result;
}

// -----------------------------------------------------------------------------
// 乱れ
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

// 升の破綻。画面を 2 次元の升で切り、升ごとに横へずらして色を段へ落とす。
//
// 横帯だけの乱れと違い、圧縮の壊れた映像のような四角い破綻になる。
half3 SBSCrtBlock(half3 color, half3 gx, half3 gy, half2 screen, SBSCrtStyle st)
{
    half size = max(st.blockScale, 2.0);
    half2 cell = floor(screen / size);
    half tick = floor(st.time * 12.0);

    half pick = SBSCrtHash(cell + half2(tick * 1.7, tick));
    half variation = SBSCrtHash(cell + half2(tick + 41.0, tick * 2.3 + 17.0));

    half visible = step(1.0 - saturate(st.block), pick);

    half3 shifted = SBSCrtShiftFrom(
        color, gx, gy, half2((variation * 2.0 - 1.0) * st.blockShift, 0.0));

    half3 broken = shifted;

    // 色の量子化は加算に対して非線形であり、ライトごとの ForwardAdd に適用すると
    // ライト数で結果が変わる。ForwardAdd では線形な横ずらしだけを適用する。
    if (st.additivePass < 0.5 && st.blockCrush > 0.0)
    {
        // 升ごとに色を段へ落とす。段数を 64 から 3 まで落として破綻させる。
        half crush = saturate(st.blockCrush);
        half levels = lerp(64.0, 3.0, crush);
        half3 crushed = floor(saturate(shifted) * levels + 0.5) / levels;
        broken = lerp(shifted, crushed, crush);
    }

    return lerp(color, broken, visible);
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
    half amount = saturate(st.amount);
    if (amount <= 0.0)
        return color;

    // ピクセル側の効果がすべて無効なら、座標計算と各段の分岐も省く。
    if (st.block <= 0.0 && st.glitch <= 0.0 && st.aberration <= 0.0 &&
        st.staticAmount <= 0.0 && st.scanline <= 0.0 && st.mask <= 0.0 &&
        st.roll <= 0.0 && st.noise <= 0.0 && st.vignette <= 0.0)
        return color;

    half3 result = color;

    // 1. 升の破綻
    if (st.block > 0.0)
        result = SBSCrtBlock(result, ddx(result), ddy(result), screen, st);

    // 2. 乱れた帯。横にずらし、帯によっては色も入れ替える。
    if (st.glitch > 0.0)
    {
        half2 band = SBSCrtBand(screen.y, st);
        half shift = (band.y * 2.0 - 1.0) * st.glitchShift * band.x;
        result = SBSCrtShiftFrom(result, ddx(result), ddy(result), half2(shift, 0.0));
        result = SBSCrtChannelSwap(result, band.y, st.glitchColor * band.x);
    }

    // 3. 色ずれ。画面の中心から外へ向かうほど広げる。
    if (st.aberration > 0.0)
    {
        half2 size = max(resolution, half2(1.0, 1.0));
        half2 radial = screen / size - half2(0.5, 0.5);
        half spread = length(radial);
        half2 direction = (spread > 1.0e-4) ? (radial / spread) : half2(1.0, 0.0);
        result = SBSCrtAberration(
            result, ddx(result), ddy(result),
            direction * (st.aberration * min(spread * 2.0, 1.0)));
    }

    // 4. 砂嵐。ここで置き換えると、走査線と縞はこの上に乗る。
    if (st.staticAmount > 0.0)
        result = SBSCrtStatic(result, ddx(result), ddy(result), screen, st);

    // 5. 走査線とシャドウマスク
    if (st.scanline > 0.0)
        result *= SBSCrtScanline(screen.y, st);
    if (st.mask > 0.0)
        result *= SBSCrtMask(screen.x, st);

    // 6. ロールバー
    if (st.roll > 0.0)
        result *= SBSCrtRoll(screen.y / max(resolution.y, 1.0), st);

    // 7. ざらつき
    if (st.noise > 0.0)
        result = SBSCrtGrain(result, screen, st);

    // 8. 周辺の落ち込み
    if (st.vignette > 0.0)
        result *= SBSCrtVignette(screen / max(resolution, half2(1.0, 1.0)), st);

    result = max(result, half3(0.0, 0.0, 0.0));
    return lerp(color, result, amount);
}

#endif // SABASHADER_CRTGLITCH_CORE_INCLUDED
