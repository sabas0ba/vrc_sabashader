using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SabaShader.EditorTools
{
    public sealed class TransformationBankGenerationReport : ScriptableObject
    {
        [SerializeField] string outputFolder;
        [SerializeField] string outfitAPath;
        [SerializeField] string outfitBPath;
        [SerializeField] TransformationBankStyle style;
        [SerializeField] float duration;
        [SerializeField] float effectIntensity;
        [SerializeField] string[] sourceMaterials;
        [SerializeField] Material[] generatedMaterials;
        [SerializeField] AnimationClip aToBClip;
        [SerializeField] AnimationClip bToAClip;

        public string OutputFolder => outputFolder;
        public string OutfitAPath => outfitAPath;
        public string OutfitBPath => outfitBPath;
        public TransformationBankStyle Style => style;
        public float Duration => duration;
        public float EffectIntensity => effectIntensity;
        public string[] SourceMaterials => sourceMaterials;
        public Material[] GeneratedMaterials => generatedMaterials;
        public AnimationClip AToBClip => aToBClip;
        public AnimationClip BToAClip => bToAClip;

        internal void Initialize(
            string generatedOutputFolder,
            string generatedOutfitAPath,
            string generatedOutfitBPath,
            TransformationBankStyle generatedStyle,
            float generatedDuration,
            float generatedEffectIntensity,
            Material[] sources,
            Material[] materials,
            AnimationClip forwardClip,
            AnimationClip reverseClip)
        {
            outputFolder = generatedOutputFolder;
            outfitAPath = generatedOutfitAPath;
            outfitBPath = generatedOutfitBPath;
            style = generatedStyle;
            duration = generatedDuration;
            effectIntensity = generatedEffectIntensity;
            sourceMaterials = sources
                .Select(material =>
                {
                    var path = AssetDatabase.GetAssetPath(material);
                    return string.IsNullOrEmpty(path) ? material.name : path;
                })
                .ToArray();
            generatedMaterials = materials;
            aToBClip = forwardClip;
            bToAClip = reverseClip;
        }
    }
}
