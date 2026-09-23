using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SabaShader.EditorTools
{
    public sealed class TransformationBankMaterialIssue
    {
        public string OutfitLabel { get; internal set; }
        public string RendererPath { get; internal set; }
        public Renderer Renderer { get; internal set; }
        public int MaterialSlot { get; internal set; }
        public Material Material { get; internal set; }
        public string[] MissingProperties { get; internal set; }

        public string ValidationMessage
        {
            get
            {
                if (Material == null)
                {
                    return OutfitLabel + "のMaterial Slotが空です: " +
                        RendererPath + " [" + MaterialSlot + "]";
                }
                if (Material.shader == null)
                {
                    return OutfitLabel + "のMaterialにShaderがありません: " +
                        RendererPath + " [" + MaterialSlot + "]";
                }
                return OutfitLabel + "のMaterialでTransformation Bankが有効ではありません: " +
                    RendererPath + " [" + MaterialSlot + "] / " + Material.name + " / missing: " +
                    string.Join(", ", MissingProperties);
            }
        }
    }

    public static class TransformationBankMaterialCompatibility
    {
        static readonly string[] RequiredPropertyNames =
        {
            TransformationBankClipGenerator.ProgressProperty,
            TransformationBankClipGenerator.RoleProperty,
            TransformationBankClipGenerator.StyleProperty,
            TransformationBankClipGenerator.EffectIntensityProperty,
        };

        public static IReadOnlyList<string> RequiredProperties
        {
            get { return RequiredPropertyNames; }
        }

        public static IReadOnlyList<TransformationBankMaterialIssue> FindIssues(
            TransformationBankClipGenerationOptions options)
        {
            var issues = new List<TransformationBankMaterialIssue>();
            if (options == null || options.AvatarRoot == null)
            {
                return issues;
            }

            CollectIssues(options.AvatarRoot.transform, options.OutfitA, "衣装A", issues);
            CollectIssues(options.AvatarRoot.transform, options.OutfitB, "衣装B", issues);
            return issues;
        }

        public static bool IsCompatible(Material material)
        {
            return material != null && material.shader != null &&
                RequiredPropertyNames.All(material.HasProperty);
        }

        public static bool IsCompatible(Shader shader)
        {
            if (shader == null)
            {
                return false;
            }

            Material probe = null;
            try
            {
                probe = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                return RequiredPropertyNames.All(probe.HasProperty);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (probe != null)
                {
                    UnityEngine.Object.DestroyImmediate(probe);
                }
            }
        }

        public static Shader[] FindCompatibleShaders()
        {
            var shaders = new Dictionary<int, Shader>();
            foreach (var guid in AssetDatabase.FindAssets("t:Shader"))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (shader != null)
                {
                    shaders[shader.GetInstanceID()] = shader;
                }
            }
            foreach (var shader in Resources.FindObjectsOfTypeAll<Shader>())
            {
                if (shader != null)
                {
                    shaders[shader.GetInstanceID()] = shader;
                }
            }

            return shaders.Values
                .Where(IsCompatible)
                .OrderBy(shader => shader.name, StringComparer.Ordinal)
                .ThenBy(shader => AssetDatabase.GetAssetPath(shader), StringComparer.Ordinal)
                .ToArray();
        }

        public static Material[] FindCompatibleMaterials()
        {
            return AssetDatabase.FindAssets("t:Material")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(IsCompatible)
                .OrderBy(material => material.name, StringComparer.Ordinal)
                .ThenBy(material => AssetDatabase.GetAssetPath(material), StringComparer.Ordinal)
                .ToArray();
        }

        public static Material CreateAndAssign(
            TransformationBankMaterialIssue issue,
            Shader shader,
            TransformationBankStyle style,
            float effectIntensity,
            string outputFolder)
        {
            if (issue == null)
            {
                throw new ArgumentNullException(nameof(issue));
            }
            if (issue.Renderer == null)
            {
                throw new InvalidOperationException("対象Rendererが存在しません。");
            }
            if (!IsCompatible(shader))
            {
                throw new InvalidOperationException("選択したShaderはTransformation Bankに対応していません。");
            }

            string normalizedOutput;
            if (!TransformationBankClipGenerator.TryNormalizeAssetFolder(outputFolder, out normalizedOutput))
            {
                throw new InvalidOperationException("Material出力先はAssets以下に指定してください。");
            }
            TransformationBankClipGenerator.EnsureAssetFolder(normalizedOutput);

            var generated = new Material(shader);
            if (issue.Material != null)
            {
                var targetKeywords = generated.shaderKeywords;
                generated.CopyPropertiesFromMaterial(issue.Material);
                generated.shader = shader;
                generated.shaderKeywords = targetKeywords;
            }
            var sourceName = issue.Material == null ? issue.Renderer.name : issue.Material.name;
            generated.name = sourceName + " / Transformation Bank Compatible";
            generated.SetInteger(TransformationBankClipGenerator.RoleProperty, (int)TransformationBankRole.Incoming);
            generated.SetInteger(TransformationBankClipGenerator.StyleProperty, (int)style);
            generated.SetFloat(TransformationBankClipGenerator.ProgressProperty, 1.0f);
            generated.SetFloat(TransformationBankClipGenerator.EffectIntensityProperty, effectIntensity);

            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                normalizedOutput + "/" +
                TransformationBankClipGenerator.SanitizeAssetName(sourceName + "_TransformationBank") + ".mat");
            AssetDatabase.CreateAsset(generated, assetPath);
            AssetDatabase.SaveAssets();
            Assign(issue, generated);
            return generated;
        }

        public static void Assign(TransformationBankMaterialIssue issue, Material material)
        {
            if (issue == null)
            {
                throw new ArgumentNullException(nameof(issue));
            }
            if (!IsCompatible(material))
            {
                throw new InvalidOperationException("選択したMaterialはTransformation Bankに対応していません。");
            }
            if (issue.Renderer == null)
            {
                throw new InvalidOperationException("対象Rendererが存在しません。");
            }

            var materials = issue.Renderer.sharedMaterials;
            if (issue.MaterialSlot < 0)
            {
                throw new InvalidOperationException("Material Slotが不正です。再スキャンしてください。");
            }
            if (issue.MaterialSlot >= materials.Length)
            {
                Array.Resize(ref materials, issue.MaterialSlot + 1);
            }

            Undo.RecordObject(issue.Renderer, "Assign Transformation Bank Material");
            materials[issue.MaterialSlot] = material;
            issue.Renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(issue.Renderer);
            if (PrefabUtility.IsPartOfPrefabInstance(issue.Renderer))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(issue.Renderer);
            }
        }

        static void CollectIssues(
            Transform avatarRoot,
            GameObject outfitRoot,
            string outfitLabel,
            ICollection<TransformationBankMaterialIssue> issues)
        {
            if (outfitRoot == null)
            {
                return;
            }

            var renderers = outfitRoot.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is SkinnedMeshRenderer || renderer is MeshRenderer);
            foreach (var renderer in renderers)
            {
                var path = renderer.transform.IsChildOf(avatarRoot)
                    ? AnimationUtility.CalculateTransformPath(renderer.transform, avatarRoot)
                    : renderer.name;
                var materials = renderer.sharedMaterials;
                if (materials.Length == 0)
                {
                    issues.Add(new TransformationBankMaterialIssue
                    {
                        OutfitLabel = outfitLabel,
                        RendererPath = path,
                        Renderer = renderer,
                        MaterialSlot = 0,
                        Material = null,
                        MissingProperties = RequiredPropertyNames.ToArray(),
                    });
                    continue;
                }

                for (var slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    var missing = material == null || material.shader == null
                        ? RequiredPropertyNames.ToArray()
                        : RequiredPropertyNames.Where(property => !material.HasProperty(property)).ToArray();
                    if (missing.Length == 0)
                    {
                        continue;
                    }
                    issues.Add(new TransformationBankMaterialIssue
                    {
                        OutfitLabel = outfitLabel,
                        RendererPath = path,
                        Renderer = renderer,
                        MaterialSlot = slot,
                        Material = material,
                        MissingProperties = missing,
                    });
                }
            }
        }
    }
}
