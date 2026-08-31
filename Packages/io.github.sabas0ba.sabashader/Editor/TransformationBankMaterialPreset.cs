using UnityEngine;

namespace SabaShader.EditorTools
{
    static class TransformationBankMaterialPreset
    {
        const string Bank = TransformationBankClipGenerator.BankPrefix;

        public static void Apply(Material material, TransformationBankStyle style)
        {
            var noiseScale = style == TransformationBankStyle.Astral ? 10.0f : 7.0f;
            var noise = style == TransformationBankStyle.Gaia ? 0.58f : 0.36f;
            var edgeWidth = 0.09f;
            var edgeEmission = 3.8f;
            var displacement = style == TransformationBankStyle.Cyber ? 0.16f : 0.1f;
            var blockScale = 8.0f;
            var patternScale = style == TransformationBankStyle.Astral ? 8.0f : 6.0f;
            var patternSpeed = 1.0f;
            var patternEmission = 3.0f;

            switch (style)
            {
                case TransformationBankStyle.Flame:
                    noiseScale = 4.5f;
                    noise = 0.78f;
                    edgeWidth = 0.13f;
                    edgeEmission = 5.5f;
                    displacement = 0.24f;
                    patternScale = 4.2f;
                    patternSpeed = 1.8f;
                    patternEmission = 5.2f;
                    break;
                case TransformationBankStyle.Shatter:
                    noiseScale = 6.0f;
                    noise = 0.46f;
                    edgeWidth = 0.07f;
                    edgeEmission = 4.2f;
                    displacement = 0.46f;
                    blockScale = 4.2f;
                    patternScale = 7.0f;
                    patternSpeed = 0.55f;
                    patternEmission = 3.8f;
                    break;
                case TransformationBankStyle.Glitch:
                    noiseScale = 12.0f;
                    noise = 0.82f;
                    edgeWidth = 0.065f;
                    edgeEmission = 5.0f;
                    displacement = 0.28f;
                    blockScale = 10.0f;
                    patternScale = 12.0f;
                    patternSpeed = 2.2f;
                    patternEmission = 4.8f;
                    break;
                case TransformationBankStyle.Melt:
                    noiseScale = 5.0f;
                    noise = 0.72f;
                    edgeWidth = 0.11f;
                    edgeEmission = 3.4f;
                    displacement = 0.3f;
                    blockScale = 6.0f;
                    patternScale = 5.0f;
                    patternSpeed = 0.7f;
                    patternEmission = 2.8f;
                    break;
                case TransformationBankStyle.CosmicRift:
                    noiseScale = 9.0f;
                    noise = 0.64f;
                    edgeWidth = 0.12f;
                    edgeEmission = 6.0f;
                    displacement = 0.34f;
                    blockScale = 7.0f;
                    patternScale = 9.0f;
                    patternSpeed = 0.65f;
                    patternEmission = 6.2f;
                    break;
                case TransformationBankStyle.MagicalSparkle:
                    noiseScale = 7.5f;
                    noise = 0.48f;
                    edgeWidth = 0.1f;
                    edgeEmission = 6.5f;
                    displacement = 0.3f;
                    blockScale = 9.0f;
                    patternScale = 11.0f;
                    patternSpeed = 1.6f;
                    patternEmission = 7.0f;
                    break;
                case TransformationBankStyle.ManaMist:
                    noiseScale = 5.5f;
                    noise = 0.82f;
                    edgeWidth = 0.14f;
                    edgeEmission = 4.8f;
                    displacement = 0.42f;
                    blockScale = 6.0f;
                    patternScale = 5.5f;
                    patternSpeed = 0.55f;
                    patternEmission = 4.6f;
                    break;
            }

            SetFloat(material, Bank + "NoiseScale", noiseScale);
            SetFloat(material, Bank + "Noise", noise);
            SetFloat(material, Bank + "EdgeWidth", edgeWidth);
            SetFloat(material, Bank + "EdgeEmission", edgeEmission);
            SetFloat(material, Bank + "Displacement", displacement);
            SetFloat(material, Bank + "BlockScale", blockScale);
            SetFloat(material, Bank + "PatternScale", patternScale);
            SetFloat(material, Bank + "PatternSpeed", patternSpeed);
            SetFloat(material, Bank + "PatternEmission", patternEmission);
        }

        static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }
    }
}
