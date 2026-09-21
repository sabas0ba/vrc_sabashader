SC_Texture2D(_BaseTexture, "white", [SCMainTexture], "__Texture", "")
SC_ScaleOffset(_BaseTexture)
SC_SamplerState(sampler_BaseTexture)
SC_color(_BaseColor, (1,1,1,1), [], "__Color", "")
SC_Texture2DArray(_SharedGradients, "white", [SCGradients], "__SharedGradients", "")
SC_Texture2D(_NormalMap, "bump", [], "__NormalMap", "")
SC_float(_NormalScale, 1, [SCCache][SCRange(-10,10)], "__NormalScale", "")
SC_float(_Roughness, 0.8, [SCRange(0.002,1)], "__Roughness", "")

SC_Box
SC_uint(_Cull, 2, [SCEnum(Off,0,Front,1,Back,2)], "__Cull", "__CullDesc")
SC_uint(_AlphaMode, 1, [SCEnum(Opaque,0,Cutout,1)], "__AlphaMode", "__AlphaModeDesc")
SC_float(_Cutoff, 0.5, [SCRange(0,1)], "__Cutoff", "")
SC_BoxEnd

SC_Foldout(__Paper)
SC_float(_DepthScale, 1, [SCRange(0.02,1)], "__DepthScale", "__DepthScaleDesc")
SC_float(_Flatness, 0.9, [SCRange(0,1)], "__Flatness", "__FlatnessDesc")
SC_float(_ShadeThreshold, 0.45, [SCRange(0,1)], "__ShadeThreshold", "__ShadeThresholdDesc")
SC_uint(_SurfaceShadowEnabled, 1, [SCToggle], "__SurfaceShadowEnabled", "__SurfaceShadowEnabledDesc")
SC_color(_BackfaceColor, (0.82,0.72,0.58,1), [], "__BackfaceColor", "")
SC_float(_BackfaceStrength, 0.35, [SCRange(0,1)], "__BackfaceStrength", "")
SC_float(_PaperGrain, 0.08, [SCRange(0,1)], "__PaperGrain", "__PaperGrainDesc")
SC_float(_PaperGrainScale, 180, [SCRange(1,1000)], "__PaperGrainScale", "")
SC_color(_EdgeColor, (1,0.7,0.35,1), [SCHDR], "__EdgeColor", "")
SC_float(_EdgeWidth, 0.01, [SCRange(0.001,0.05)], "__EdgeWidth", "__EdgeWidthDesc")
SC_float(_EdgePower, 4, [SCRange(0.1,16)], "__EdgePower", "")
SC_float(_EdgeIntensity, 0.12, [SCRange(0,2)], "__EdgeIntensity", "")
SC_float(_OutlineWidth, 0.025, [SCRange(0,0.1)], "__OutlineWidth", "__OutlineWidthDesc")
SC_color(_OutlineColor, (0.23,0.12,0.08,1), [], "__OutlineColor", "")
SC_float(_WhiteBorderWidth, 0.06, [SCRange(0,0.2)], "__WhiteBorderWidth", "__WhiteBorderWidthDesc")
SC_color(_WhiteBorderColor, (1,1,1,1), [], "__WhiteBorderColor", "")
SC_FoldoutEnd

SC_Foldout(__Specular)
SC_color(_SpecularColor, (1,0.9,0.75,1), [SCHDR], "__SpecularColor", "")
SC_float(_SpecularIntensity, 0.15, [SCRange(0,2)], "__SpecularIntensity", "")
SC_float(_SpecularPower, 48, [SCRange(1,256)], "__SpecularPower", "")
SC_FoldoutEnd

SC_Foldout(__LightSettings)
SC_uint(_CastShadowEnabled, 1, [SCToggle], "__CastShadowEnabled", "__CastShadowEnabledDesc")
SC_float(_SHLightWeight, 1, [SCRange(0,2)], "__SHLightWeight", "")
SC_float(_SHLightDirectionWeight, 0.6, [SCRange(0,2)], "__SHLightDirectionWeight", "")
SC_FoldoutEnd
