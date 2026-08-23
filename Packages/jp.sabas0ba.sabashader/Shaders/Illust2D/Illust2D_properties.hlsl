SC_Texture2D(_BaseTexture, "white", [SCMainTexture], "__Texture", "")
SC_ScaleOffset(_BaseTexture)
SC_SamplerState(sampler_BaseTexture)
SC_color(_BaseColor, (1,1,1,1), [], "__Color", "")
SC_Texture2D(_SharedMask, "white", [SCMask], "__SharedMask", "")
SC_Texture2DArray(_SharedGradients, "white", [SCGradients], "__SharedGradients", "")
SC_float(_NormalScale, 1, [SCCache][SCRange(-10,10)], "", "")
SC_Texture2D(_NormalMap, "bump", [], "__NormalMap", "")
SC_uint(_NormalMapWithRoughness, 0, [SCToggle], "__NormalMapWithRoughness", "")
SC_float(_Roughness, 0.5, [SCRange(0.002,1)], "__Roughness", "")

SC_Box
SC_uint(_Cull, 2, [SCEnum(Off,0,Front,1,Back,2)], "__Cull", "__CullDesc")
SC_uint(_AlphaMode, 0, [SCEnum(Opaque,0,Cutout,1)], "__AlphaMode", "__AlphaModeDesc")
SC_float(_Cutoff, 0.5, [SCRange(0,1)], "__Cutoff", "")
SC_BoxEnd

SC_Foldout(__Shading)
SC_float(_ShadeBorder1, 0.5, [SCRange(0,1)], "__ShadeBorder1", "__ShadeBorder1Desc")
SC_float(_ShadeBlur1, 0.06, [SCRange(0,1)], "__ShadeBlur1", "__ShadeBlurDesc")
SC_color(_Shade1Color, (1,1,1,1), [], "__Shade1Color", "__ShadeColorDesc")
SC_float(_Shade1HueShift, 0.02, [SCRange(-0.5,0.5)], "__Shade1HueShift", "__HueShiftDesc")
SC_float(_Shade1Saturation, 1.15, [SCRange(0,3)], "__Shade1Saturation", "")
SC_float(_Shade1Value, 0.82, [SCRange(0,2)], "__Shade1Value", "")
SC_float(_ShadeBorder2, 0.22, [SCRange(0,1)], "__ShadeBorder2", "__ShadeBorder2Desc")
SC_float(_ShadeBlur2, 0.06, [SCRange(0,1)], "__ShadeBlur2", "__ShadeBlurDesc")
SC_color(_Shade2Color, (1,1,1,1), [], "__Shade2Color", "__ShadeColorDesc")
SC_float(_Shade2HueShift, 0.05, [SCRange(-0.5,0.5)], "__Shade2HueShift", "__HueShiftDesc")
SC_float(_Shade2Saturation, 1.3, [SCRange(0,3)], "__Shade2Saturation", "")
SC_float(_Shade2Value, 0.62, [SCRange(0,2)], "__Shade2Value", "")
SC_float(_ShadeSteps, 0, [SCRangeInt(0,8)], "__ShadeSteps", "__ShadeStepsDesc")
SC_float(_ShadowStrength, 1, [SCRange(0,1)], "__ShadowStrength", "__ShadowStrengthDesc")
SC_uint(_ShadeMaskChannel, 4, [SCEnum(R,0,G,1,B,2,A,3,None,4)], "__ShadeMaskChannel", "")
SC_FoldoutEnd

SC_Foldout(__Specular)
SC_color(_SpecularColor, (1,1,1,1), [SCHDR], "__SpecularColor", "")
SC_float(_SpecularBorder, 0.5, [SCRange(0,1)], "__SpecularBorder", "")
SC_float(_SpecularBlur, 0.02, [SCRange(0,1)], "__SpecularBlur", "")
SC_float(_SpecularSmoothness, 0.85, [SCRange(0,1)], "__SpecularSmoothness", "")
SC_uint(_SpecularMaskChannel, 4, [SCEnum(R,0,G,1,B,2,A,3,None,4)], "__SpecularMaskChannel", "")
SC_FoldoutEnd

SC_Foldout(__RimLight)
SC_color(_RimColor, (0,0,0,1), [SCHDR], "__RimColor", "")
SC_float(_RimBorder, 0.72, [SCRange(0,1)], "__RimBorder", "")
SC_float(_RimBlur, 0.12, [SCRange(0,1)], "__RimBlur", "")
SC_float(_RimLightAlign, 1, [SCRange(0,1)], "__RimLightAlign", "__RimLightAlignDesc")
SC_uint(_RimMaskChannel, 4, [SCEnum(R,0,G,1,B,2,A,3,None,4)], "__RimMaskChannel", "")
SC_FoldoutEnd

SC_Foldout(__Emission)
SC_Texture2D(_EmissionMap, "white", [], "__EmissionMap", "")
SC_color(_EmissionColor, (0,0,0,1), [SCHDR], "__EmissionColor", "")
SC_FoldoutEnd

SC_Foldout(__Outline)
SC_uint(_OutlineEnabled, 1, [SCToggle], "__OutlineEnabled", "")
SC_float(_OutlineWidth, 0.15, [SCRange(0,5)], "__OutlineWidth", "__OutlineWidthDesc")
SC_color(_OutlineColor, (0.15,0.1,0.13,1), [], "__OutlineColor", "")
SC_float(_OutlineAlbedoBlend, 0.6, [SCRange(0,1)], "__OutlineAlbedoBlend", "__OutlineAlbedoBlendDesc")
SC_float(_OutlineHueShift, 0.02, [SCRange(-0.5,0.5)], "__OutlineHueShift", "__HueShiftDesc")
SC_float(_OutlineSaturation, 1.2, [SCRange(0,3)], "__OutlineSaturation", "")
SC_float(_OutlineValue, 0.45, [SCRange(0,2)], "__OutlineValue", "")
SC_float(_OutlineFixedWidth, 1, [SCRange(0,1)], "__OutlineFixedWidth", "__OutlineFixedWidthDesc")
SC_uint(_OutlineVertexColorChannel, 4, [SCEnum(R,0,G,1,B,2,A,3,None,4)], "__OutlineVertexColorChannel", "__OutlineVertexColorChannelDesc")
SC_FoldoutEnd

SC_Foldout(__LightSettings)
SC_float(_LightMinLimit, 0.08, [SCRange(0,1)], "__LightMinLimit", "__LightMinLimitDesc")
SC_float(_LightMaxLimit, 1, [SCRange(0,10)], "__LightMaxLimit", "__LightMaxLimitDesc")
SC_float(_MonochromeLighting, 0, [SCRange(0,1)], "__MonochromeLighting", "__MonochromeLightingDesc")
SC_float(_AsUnlit, 0, [SCRange(0,1)], "__AsUnlit", "__AsUnlitDesc")
SC_float(_SHLightWeight, 1, [SCRange(0,2)], "__SHLightWeight", "__SHLightWeightDesc")
SC_float(_SHLightDirectionWeight, 0.6, [SCRange(0,2)], "__SHLightDirectionWeight", "__SHLightDirectionWeightDesc")
SC_FoldoutEnd

SC_Foldout(__Grading)
SC_float(_Saturation, 1.05, [SCRange(0,3)], "__Saturation", "__SaturationDesc")
SC_float(_Contrast, 1.02, [SCRange(0,3)], "__Contrast", "__ContrastDesc")
SC_FoldoutEnd
