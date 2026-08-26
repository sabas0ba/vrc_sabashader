// =============================================================================
// テストシーン
//
// prelude.glsl + Illust2DCore.hlsl + 生成された設定コード の後ろに連結される。
// 生成コードが以下を定義している前提:
//   const vec2  SCENE_RESOLUTION;
//   const int   SCENE_MODE;
//   const vec3  SCENE_LIGHT_DIR;
//   const vec3  SCENE_LIGHT_COLOR;
//   const vec3  SCENE_AMBIENT;
//   const float SCENE_GAMMA;
//   const vec3  SCENE_OUTLINE_COLOR;
//   const float SCENE_OUTLINE_HUE / SCENE_OUTLINE_SAT / SCENE_OUTLINE_VALUE;
//   SBSStyle sceneStyle();
// =============================================================================

out vec4 fragColor;

const vec3 SCENE_BACKGROUND = vec3(0.10, 0.11, 0.13);

// 8 行分のサンプル色。彩度・明度がばらけるように選んである。
vec3 sceneSwatchColor(int row)
{
    if (row == 0) return vec3(0.92, 0.78, 0.70); // 肌
    if (row == 1) return vec3(0.86, 0.32, 0.34); // 赤
    if (row == 2) return vec3(0.95, 0.80, 0.28); // 黄
    if (row == 3) return vec3(0.30, 0.66, 0.38); // 緑
    if (row == 4) return vec3(0.26, 0.45, 0.86); // 青
    if (row == 5) return vec3(0.58, 0.36, 0.78); // 紫
    if (row == 6) return vec3(0.95, 0.95, 0.95); // 白
    return vec3(0.12, 0.12, 0.14);               // 黒
}

// 球に貼るベースカラー。市松と上下の色替えで色相シフトの効きが見えるようにする。
vec3 sceneSphereAlbedo(vec2 uv)
{
    float checker = mod(floor(uv.x * 10.0) + floor(uv.y * 8.0), 2.0);
    vec3 top = vec3(0.92, 0.78, 0.70);
    vec3 bottom = vec3(0.30, 0.48, 0.82);
    vec3 c = mix(bottom, top, step(0.55, uv.y));
    return c * (0.84 + 0.16 * checker);
}

SBSSurface sceneDefaultSurface()
{
    SBSSurface s;
    s.albedo = vec3(1.0, 1.0, 1.0);
    s.N = vec3(0.0, 0.0, -1.0);
    s.L = normalize(SCENE_LIGHT_DIR);
    s.V = vec3(0.0, 0.0, -1.0);
    s.lightColor = SCENE_LIGHT_COLOR;
    s.ambientColor = SCENE_AMBIENT;
    s.attenuation = 1.0;
    s.shadeMask = 1.0;
    s.specularMask = 1.0;
    s.rimMask = 1.0;
    return s;
}

// mode 0: ライティングされた球
vec4 sceneSphere(vec2 ndc)
{
    vec2 p = ndc * 1.18;
    float r2 = dot(p, p);
    if (r2 > 1.0) return vec4(SCENE_BACKGROUND, 1.0);

    vec3 pos = vec3(p, -sqrt(1.0 - r2));
    vec3 N = normalize(pos);
    vec2 uv = vec2(atan(N.x, -N.z) / 6.28318530718 + 0.5, N.y * 0.5 + 0.5);

    SBSSurface s = sceneDefaultSurface();
    s.albedo = sceneSphereAlbedo(uv);
    s.N = N;
    // 落ち影を模した円板。shadowStrength の経路を必ず通す。
    s.attenuation = step(0.34, length(pos.xy - vec2(-0.42, 0.30)));

    return vec4(SBSComposeIllust(s, sceneStyle()), 1.0);
}

// -----------------------------------------------------------------------------
// mode 4-6: 球以外の立体
//
// 球は解析的に解けるが、平らな面・鋭い稜線・自己遮蔽は球では出てこない。
// これらは距離関数のレイマーチで描く。カメラは球モードと同じく
// -Z から +Z を向く平行投影で、V = (0, 0, -1) の前提を崩さない。
// -----------------------------------------------------------------------------

const float SCENE_SOLID_YAW = 0.75;
const float SCENE_SOLID_PITCH = -0.42;
const float SCENE_SOLID_EPSILON = 0.0008;
const float SCENE_SOLID_FAR = 6.0;

// ワールド座標を立体のローカル座標へ。行列の並び順に依存しないよう成分で書く。
vec3 sceneToObject(vec3 p)
{
    float cy = cos(SCENE_SOLID_YAW);
    float sy = sin(SCENE_SOLID_YAW);
    vec3 a = vec3(p.x * cy - p.z * sy, p.y, p.x * sy + p.z * cy);

    float cx = cos(SCENE_SOLID_PITCH);
    float sx = sin(SCENE_SOLID_PITCH);
    return vec3(a.x, a.y * cx - a.z * sx, a.y * sx + a.z * cx);
}

float sceneSdBox(vec3 p, vec3 b)
{
    vec3 q = abs(p) - b;
    return length(max(q, vec3(0.0, 0.0, 0.0))) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float sceneSdTorus(vec3 p, float major, float minor)
{
    vec2 q = vec2(length(p.xz) - major, p.y);
    return length(q) - minor;
}

float sceneSdCapsule(vec3 p, float half_height, float radius)
{
    vec3 q = vec3(p.x, p.y - clamp(p.y, -half_height, half_height), p.z);
    return length(q) - radius;
}

float sceneSolidSdf(vec3 p)
{
    vec3 q = sceneToObject(p);
    if (SCENE_MODE == 4) return sceneSdBox(q, vec3(0.58, 0.58, 0.58));
    if (SCENE_MODE == 5) return sceneSdTorus(q, 0.60, 0.26);
    return sceneSdCapsule(q, 0.36, 0.42);
}

vec3 sceneSolidNormal(vec3 p)
{
    vec2 e = vec2(0.0015, 0.0);
    return normalize(vec3(
        sceneSolidSdf(p + e.xyy) - sceneSolidSdf(p - e.xyy),
        sceneSolidSdf(p + e.yxy) - sceneSolidSdf(p - e.yxy),
        sceneSolidSdf(p + e.yyx) - sceneSolidSdf(p - e.yyx)));
}

// 自己遮蔽。トーラスの内側などで shadowStrength の経路を通す。
float sceneSolidShadow(vec3 p, vec3 L)
{
    float t = 0.03;
    for (int i = 0; i < 48; i++)
    {
        float d = sceneSolidSdf(p + L * t);
        if (d < SCENE_SOLID_EPSILON) return 0.0;
        t += max(d, 0.004);
        if (t > 3.0) break;
    }
    return 1.0;
}

// 立体に貼るベースカラー。球と同じく市松と上下の色替えで色相シフトを見せる。
vec3 sceneSolidAlbedo(vec3 q)
{
    float checker = mod(floor(q.x * 5.0) + floor(q.y * 5.0) + floor(q.z * 5.0), 2.0);
    vec3 top = vec3(0.92, 0.78, 0.70);
    vec3 bottom = vec3(0.30, 0.48, 0.82);
    vec3 c = mix(bottom, top, step(0.0, q.y));
    return c * (0.84 + 0.16 * checker);
}

vec3 sceneSolidSample(vec2 ndc)
{
    vec3 ro = vec3(ndc * 1.25, -3.0);
    vec3 rd = vec3(0.0, 0.0, 1.0);

    float t = 0.0;
    for (int i = 0; i < 96; i++)
    {
        vec3 p = ro + rd * t;
        float d = sceneSolidSdf(p);
        if (d < SCENE_SOLID_EPSILON)
        {
            SBSSurface s = sceneDefaultSurface();
            s.albedo = sceneSolidAlbedo(sceneToObject(p));
            s.N = sceneSolidNormal(p);
            s.attenuation = sceneSolidShadow(p, s.L);
            return SBSComposeIllust(s, sceneStyle());
        }

        t += d;
        if (t > SCENE_SOLID_FAR) break;
    }

    return SCENE_BACKGROUND;
}

// レイマーチはシルエットの 1 ピクセルが行き来しやすい。ゴールデン比較が
// 誤差幅で落ちないよう、2x2 で平均してから返す。
vec4 sceneSolid(vec2 ndc)
{
    vec2 texel = 1.0 / SCENE_RESOLUTION;
    vec3 sum = vec3(0.0, 0.0, 0.0);

    sum += sceneSolidSample(ndc + vec2(-0.25, -0.25) * texel);
    sum += sceneSolidSample(ndc + vec2(0.25, -0.25) * texel);
    sum += sceneSolidSample(ndc + vec2(-0.25, 0.25) * texel);
    sum += sceneSolidSample(ndc + vec2(0.25, 0.25) * texel);

    return vec4(sum * 0.25, 1.0);
}

// mode 1: 横軸 = ライトの当たり具合、縦軸 = ベースカラー のランプ表
vec4 sceneRampSwatch(vec2 uv)
{
    int row = int(floor(uv.y * 8.0));
    float x = uv.x;

    float c = clamp(x * 2.0 - 1.0, -1.0, 1.0);
    SBSSurface s = sceneDefaultSurface();
    s.albedo = sceneSwatchColor(row);
    s.N = vec3(0.0, 0.0, 1.0);
    s.L = vec3(0.0, sqrt(max(1.0 - c * c, 0.0)), c);

    return vec4(SBSShadedAlbedo(s, sceneStyle()), 1.0);
}

// mode 2: 横軸 = ベースカラーの反映量、縦軸 = ベースカラー のアウトライン表
vec4 sceneOutlineSwatch(vec2 uv)
{
    int row = int(floor(uv.y * 8.0));
    vec3 col = SBSOutlineColor(
        sceneSwatchColor(row),
        SCENE_OUTLINE_COLOR,
        uv.x,
        SCENE_OUTLINE_HUE,
        SCENE_OUTLINE_SAT,
        SCENE_OUTLINE_VALUE);
    return vec4(col, 1.0);
}

// mode 3: 横軸 = 入射光の明るさ、縦軸 = 入射光の色 のクランプ表
vec4 sceneLightLimitSwatch(vec2 uv)
{
    int row = int(floor(uv.y * 8.0));
    vec3 tint = sceneSwatchColor(row);
    vec3 incoming = tint * (uv.x * 2.0);
    return vec4(SBSLimitLight(incoming, sceneStyle()), 1.0);
}

// mode 7: 横軸 = 面の上向き度合い、縦軸 = ベースカラー の重ね掛け表
//
// SurfaceOverlayCore.hlsl の被覆率と適用をそのまま通す。
// 雨・汗・雪・汚れはどれもこの 1 枚の上に乗っている。
vec4 sceneOverlaySwatch(vec2 uv)
{
    int row = int(floor(uv.y * 8.0));
    vec3 albedo = sceneSwatchColor(row);

    // 横軸で真下向きから真上向きまで振る
    float c = clamp(uv.x * 2.0 - 1.0, -1.0, 1.0);
    vec3 N = vec3(sqrt(max(1.0 - c * c, 0.0)), c, 0.0);
    vec3 up = vec3(0.0, 1.0, 0.0);

    SBSOverlayStyle ost = sceneOverlayStyle();
    // coord は「流れに直交する向き / 流れに沿う向き」。テストでは uv をそのまま使う。
    float coverage = SBSOverlayCoverage(N, up, 1.0, uv, ost);
    vec3 overlay = vec3(0.86, 0.88, 0.92);

    // 置き換え量は darken との効き分けを見るため縦位置で振る
    float tint = float(row % 2);

    return vec4(SBSOverlayAlbedo(albedo, overlay, tint, coverage, ost), 1.0);
}

// mode 8: 横軸 = 明るさ、縦軸 = ベースカラー のドット絵化表
//
// PixelArtCore.hlsl の量子化とディザをそのまま通す。
// 画面座標に依存するので、決定的になるよう解像度から作った座標を渡す。
vec4 scenePixelSwatch(vec2 uv)
{
    int row = int(floor(uv.y * 8.0));
    vec3 albedo = sceneSwatchColor(row) * (uv.x * 1.2);

    SBSPixelStyle pst = scenePixelStyle();

    vec2 screen = uv * SCENE_RESOLUTION;
    float threshold = SBSPixelThreshold(screen, pst);

    vec3 quantized = SBSPixelQuantize(albedo, threshold, pst);

    // 組み込みパレットを通す。preset が 0 のときはテクスチャの代わりに
    // 虹色のグラデーションで代用する。
    float coord = SBSPixelPaletteCoord(albedo, threshold, pst);
    vec3 palette = SBSPixelPalettePreset(albedo, coord, pst);
    if (pst.preset < 0.5) palette = SBSHsvToRgb(vec3(coord, 0.65, 0.55 + 0.45 * coord));

    return vec4(SBSPixelApply(albedo, quantized, palette, pst), 1.0);
}

// mode 9: 水滴とその跡だけを見る
//
// 面の被覆（向き x マスク）を 0 にして、粒と尾だけが被覆に効くようにする。
// mode 7 のように面の被覆が 1 だと飽和して粒が見えない。
vec4 sceneDropletSwatch(vec2 uv)
{
    SBSOverlayStyle ost = sceneOverlayStyle();

    vec3 N = vec3(0.0, 0.0, -1.0);
    vec3 up = vec3(0.0, 1.0, 0.0);

    // x = 流れに直交する向き、y = 流れに沿う向き
    float coverage = SBSOverlayCoverage(N, up, 0.0, uv, ost);

    vec3 albedo = vec3(0.30, 0.34, 0.42);
    vec3 water = vec3(0.82, 0.88, 0.95);

    return vec4(SBSOverlayAlbedo(albedo, water, 1.0, coverage, ost), 1.0);
}

// mode 10: ブラウン管とグリッチをかけるテストカード
//
// 上半分は色帯。帯の中は上下に明るさを振ってあるので勾配が立つが、
// 帯と帯の境目は不連続なので、ずらしの 1 次近似が外れるところも同時に見える。
// 下半分は色相と明るさの滑らかな面で、ずらしと色ずれが素直に効く。
vec3 sceneCrtCard(vec2 uv)
{
    if (uv.y > 0.5)
    {
        int bar = int(floor(uv.x * 8.0));
        vec3 base = sceneSwatchColor(bar);

        // 帯の中を横に明るくしていく。色ずれと横ずれはどちらも横向きなので、
        // 勾配を横に立てておくと効きが見える。縦は平らにしてあり、
        // ロールバーのような縦の効果と混ざらない。
        float shade = 0.45 + 0.9 * fract(uv.x * 8.0);
        return base * shade;
    }

    // 色相を横に、明るさを縦に振った面
    float hue = uv.x;
    float value = 0.15 + 1.5 * uv.y;
    return SBSHsvToRgb(vec3(hue, 0.6, clamp(value, 0.0, 1.0)));
}

// mode 12: 外部の RenderTexture を模した入力カード
//
// テストではテクスチャを引けないため、色帯とアルファ勾配を手続きで作る。
// UV の変形と、入力色・明るさ・アルファによる合成は出荷するコアをそのまま通す。
vec4 sceneVideoInputSample(vec2 uv)
{
    vec2 bounded = clamp(uv, vec2(0.0, 0.0), vec2(0.9999, 0.9999));
    int bar = int(floor(bounded.x * 8.0));
    vec3 color = sceneSwatchColor(bar) * (0.35 + 0.65 * bounded.y);
    float alpha = 0.25 + 0.75 * bounded.y;
    return vec4(color, alpha);
}

vec4 sceneVideoInputSwatch(vec2 uv)
{
    SBSVideoInputStyle vst = sceneVideoInputStyle();
    // 中央揃えではない範囲にして、反転と Tiling / Offset の順序も固定する。
    vec2 sourceUV = SBSVideoInputUV(uv, vec4(0.75, 0.65, 0.05, 0.20), vst);
    vec4 video = sceneVideoInputSample(sourceUV);
    vec3 base = sceneCrtCard(uv);
    return vec4(SBSVideoInputApply(base, video, vst), 1.0);
}

// mode 13: LCD / LED / LED Wall の画素構造
vec4 sceneDisplayPanelSwatch(vec2 uv)
{
    SBSDisplayPanelStyle pst = sceneDisplayPanelStyle();
    vec2 screen = uv * SCENE_RESOLUTION;
    vec3 card = sceneCrtCard(uv);
    return vec4(SBSDisplayPanelApply(card, screen, 0.72, pst), 1.0);
}

// mode 11: 立体（カプセル）にブラウン管とグリッチをかける
//
// 平らなテストカードには無いシルエットが入る。ずらしの 1 次近似は縁で
// 大きく外れるので、補正量の上限が効いているかはここで見る。
//
// ここで色ずれは使わない。この立体はレイマーチで描いており、隣り合う画素で
// ループの回数が違う（分岐がそろわない）。分岐がそろわないところで取った
// 勾配の値は規定されておらず、llvmpipe では背景にまばらな点が出る。
// 実際のラスタライズでは起きないが、テストの当てにはならないので、
// 色ずれは平らなテストカード側 (mode 10) で見る。
vec4 sceneCrtSolid(vec2 ndc)
{
    vec4 base = sceneSolid(ndc);
    return vec4(SBSCrtApply(base.rgb, gl_FragCoord.xy, SCENE_RESOLUTION, sceneCrtStyle()), 1.0);
}

vec4 sceneCrtSwatch(vec2 uv)
{
    vec2 screen = uv * SCENE_RESOLUTION;
    vec3 card = sceneCrtCard(uv);
    return vec4(SBSCrtApply(card, screen, SCENE_RESOLUTION, sceneCrtStyle()), 1.0);
}

void main()
{
    vec2 uv = gl_FragCoord.xy / SCENE_RESOLUTION;
    vec2 ndc = uv * 2.0 - 1.0;

    vec4 col;
    if (SCENE_MODE == 13) col = sceneDisplayPanelSwatch(uv);
    else if (SCENE_MODE == 12) col = sceneVideoInputSwatch(uv);
    else if (SCENE_MODE == 11) col = sceneCrtSolid(ndc);
    else if (SCENE_MODE == 10) col = sceneCrtSwatch(uv);
    else if (SCENE_MODE == 9) col = sceneDropletSwatch(uv);
    else if (SCENE_MODE == 8) col = scenePixelSwatch(uv);
    else if (SCENE_MODE == 7) col = sceneOverlaySwatch(uv);
    else if (SCENE_MODE == 0) col = sceneSphere(ndc);
    else if (SCENE_MODE == 1) col = sceneRampSwatch(uv);
    else if (SCENE_MODE == 2) col = sceneOutlineSwatch(uv);
    else if (SCENE_MODE == 3) col = sceneLightLimitSwatch(uv);
    else col = sceneSolid(ndc);

    col.rgb = pow(max(col.rgb, vec3(0.0)), vec3(1.0 / SCENE_GAMMA));
    fragColor = vec4(clamp(col.rgb, 0.0, 1.0), 1.0);
}
