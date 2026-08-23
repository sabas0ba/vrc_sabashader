#ifndef SABASHADER_ILLUST2D_LIGHTING_INCLUDED
#define SABASHADER_ILLUST2D_LIGHTING_INCLUDED

// Shader Core の birp_lighting.hlsl が要求する 2 つの関数を実装する。
// トゥーン系なので「全ライトを 1 本の代表ライトにまとめる」方針をとる。
// - lightSum.direction: 明るさで重み付けした方向の和（後段で正規化する）
// - lightSum.color:     指向性成分の色の和
// - env:                方向を持たない環境光成分

// SH の L0 + L1 だけを評価する。L2 は 2D 塗りでは効果が薄いので省略している。
half3 SBSShEvalL0L1(half4 SHAr, half4 SHAg, half4 SHAb, half3 N)
{
    half4 n = half4(N.x, N.y, N.z, 1.0);
    return half3(dot(SHAr, n), dot(SHAg, n), dot(SHAb, n));
}

void SCCalculateLight(inout SCLightData lightSum, inout SCShadingData sd, inout SCCustomData cd, SCVertexData vertex, SCLightData light)
{
    lightSum.direction += light.direction * SBSLuminance(light.color);
    lightSum.color += light.color;

    // light.color = _LightColor0 * (影 * 距離減衰) なので、比を取ると減衰量が復元できる。
    // トゥーンのランプは「光の強さ」ではなく「影の有無」で切りたいので分離しておく。
    half referenceLuminance = SBSLuminance(_LightColor0.rgb);
    half attenuation = (referenceLuminance > 1.0e-4) ? saturate(SBSLuminance(light.color) / referenceLuminance) : 1.0;
    sd.shadow = min(sd.shadow, attenuation);

    __SC_PHASE_light__
}

void SCCalculateEnvironmentLight(inout SCLightData lightSum, inout half3 env, inout SCShadingData sd, inout SCCustomData cd, SCVertexData vertex, half4 SHAr, half4 SHAg, half4 SHAb, half4 SHBr, half4 SHBg, half4 SHBb, half4 SHC)
{
    half3 shDir = half3(
        SHAr.x + SHAg.x + SHAb.x,
        SHAr.y + SHAg.y + SHAb.y,
        SHAr.z + SHAg.z + SHAb.z);

    half shLen = length(shDir);
    shDir = (shLen > 1.0e-4) ? (shDir / shLen) : half3(0.0, 1.0, 0.0);

    half3 shLight = max(SBSShEvalL0L1(SHAr, SHAg, SHAb, shDir), half3(0.0, 0.0, 0.0));
    half3 shShade = max(SBSShEvalL0L1(SHAr, SHAg, SHAb, -shDir), half3(0.0, 0.0, 0.0));
    half3 shDelta = max(shLight - shShade, half3(0.0, 0.0, 0.0));

    lightSum.direction += shDir * (SBSLuminance(shDelta) * _SHLightDirectionWeight);
    lightSum.color += shDelta * _SHLightWeight;
    env += shShade;
}

// 代表ライトの方向。ライトが 1 つも無いワールドでも NaN にならないようにする。
half3 SBSResolveLightDirection(half3 accumulated)
{
    half len = length(accumulated);
    return (len > 1.0e-4) ? (accumulated / len) : normalize(half3(0.3, 1.0, -0.6));
}

#endif // SABASHADER_ILLUST2D_LIGHTING_INCLUDED
