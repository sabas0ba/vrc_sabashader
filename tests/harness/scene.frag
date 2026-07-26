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

void main()
{
    vec2 uv = gl_FragCoord.xy / SCENE_RESOLUTION;
    vec2 ndc = uv * 2.0 - 1.0;

    vec4 col;
    if (SCENE_MODE == 0) col = sceneSphere(ndc);
    else if (SCENE_MODE == 1) col = sceneRampSwatch(uv);
    else if (SCENE_MODE == 2) col = sceneOutlineSwatch(uv);
    else col = sceneLightLimitSwatch(uv);

    col.rgb = pow(max(col.rgb, vec3(0.0)), vec3(1.0 / SCENE_GAMMA));
    fragColor = vec4(clamp(col.rgb, 0.0, 1.0), 1.0);
}
