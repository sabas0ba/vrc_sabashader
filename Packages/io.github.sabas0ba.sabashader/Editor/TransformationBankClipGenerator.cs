using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SabaShader.EditorTools
{
    public enum TransformationBankStyle
    {
        Arcane,
        Cyber,
        Astral,
        Gaia,
        Umbra,
        Flame,
        Shatter,
        Glitch,
        Melt,
        CosmicRift,
        MagicalSparkle,
        ManaMist,
    }

    public enum TransformationBankRole
    {
        Incoming,
        Outgoing,
    }

    public sealed class TransformationBankClipGenerationOptions
    {
        public GameObject AvatarRoot;
        public GameObject OutfitA;
        public GameObject OutfitB;
        public TransformationBankStyle Style = TransformationBankStyle.Arcane;
        public float Duration = 2.0f;
        public float EffectIntensity = 1.6f;
        public bool ApplyRecommendedPreset = true;
        public string OutputFolder = "Assets/SabaShader/TransformationBank";
    }

    public sealed class TransformationBankClipGenerationResult
    {
        public string OutputFolder { get; internal set; }
        public AnimationClip AToBClip { get; internal set; }
        public AnimationClip BToAClip { get; internal set; }
        public Material[] Materials { get; internal set; }
        public TransformationBankGenerationReport Report { get; internal set; }
    }

    /// <summary>
    /// 元のSceneとMaterialを変更せず、衣装間遷移用のMaterialとAnimation Clipを生成する。
    /// </summary>
    public static class TransformationBankClipGenerator
    {
        public const string BankPrefix = "_io_github_sabas0ba_transformationbank_";
        public const string ProgressProperty = BankPrefix + "Progress";
        public const string RoleProperty = BankPrefix + "Role";
        public const string StyleProperty = BankPrefix + "Style";
        public const string EffectIntensityProperty = BankPrefix + "EffectIntensity";
        public const string MaterialProgressBinding = "material." + ProgressProperty;

        const float FrameRate = 60.0f;
        const string MaterialArrayPrefix = "m_Materials.Array.data[";

        public static IReadOnlyList<string> Validate(TransformationBankClipGenerationOptions options)
        {
            var errors = new List<string>();
            if (options == null)
            {
                errors.Add("生成設定がありません。");
                return errors;
            }

            if (options.AvatarRoot == null)
            {
                errors.Add("Avatar Rootを指定してください。");
            }
            if (options.OutfitA == null)
            {
                errors.Add("衣装Aを指定してください。");
            }
            if (options.OutfitB == null)
            {
                errors.Add("衣装Bを指定してください。");
            }
            if (options.Duration < 0.1f)
            {
                errors.Add("遷移時間は0.1秒以上にしてください。");
            }
            if (options.EffectIntensity < 0.0f || options.EffectIntensity > 4.0f)
            {
                errors.Add("Effect Intensityは0から4の範囲で指定してください。");
            }

            string normalizedOutput;
            if (!TryNormalizeAssetFolder(options.OutputFolder, out normalizedOutput))
            {
                errors.Add("出力先はAssets以下のProject相対パスで指定してください。");
            }

            if (options.AvatarRoot == null || options.OutfitA == null || options.OutfitB == null)
            {
                return errors;
            }

            if (options.OutfitA == options.OutfitB)
            {
                errors.Add("衣装Aと衣装Bには異なるGameObjectを指定してください。");
                return errors;
            }
            if (options.OutfitA == options.AvatarRoot || options.OutfitB == options.AvatarRoot)
            {
                errors.Add("Avatar Root自体を衣装Rootとして指定できません。");
            }
            var outfitAIsDescendant = IsDescendantOf(options.OutfitA.transform, options.AvatarRoot.transform);
            var outfitBIsDescendant = IsDescendantOf(options.OutfitB.transform, options.AvatarRoot.transform);
            if (!outfitAIsDescendant)
            {
                errors.Add("衣装AはAvatar Rootの配下に配置してください。");
            }
            if (!outfitBIsDescendant)
            {
                errors.Add("衣装BはAvatar Rootの配下に配置してください。");
            }
            if (IsDescendantOf(options.OutfitA.transform, options.OutfitB.transform) ||
                IsDescendantOf(options.OutfitB.transform, options.OutfitA.transform))
            {
                errors.Add("衣装Aと衣装BのRootを親子関係にできません。");
            }
            if (outfitAIsDescendant && outfitBIsDescendant &&
                AnimationUtility.CalculateTransformPath(options.OutfitA.transform, options.AvatarRoot.transform) ==
                AnimationUtility.CalculateTransformPath(options.OutfitB.transform, options.AvatarRoot.transform))
            {
                errors.Add("衣装Aと衣装BのAnimation binding pathが重複しています。Root名を変更してください。");
            }

            var renderersA = CollectRenderers(options.OutfitA);
            var renderersB = CollectRenderers(options.OutfitB);
            if (renderersA.Length == 0)
            {
                errors.Add("衣装AにSkinnedMeshRendererまたはMeshRendererがありません。");
            }
            if (renderersB.Length == 0)
            {
                errors.Add("衣装BにSkinnedMeshRendererまたはMeshRendererがありません。");
            }

            var rendererSet = new HashSet<Renderer>(renderersA);
            if (renderersB.Any(rendererSet.Contains))
            {
                errors.Add("衣装Aと衣装Bで同じRendererを共有できません。");
            }

            foreach (var duplicate in renderersA.Concat(renderersB)
                .GroupBy(renderer =>
                    AnimationUtility.CalculateTransformPath(renderer.transform, options.AvatarRoot.transform) +
                    "\n" + renderer.GetType().FullName)
                .Where(group => group.Count() > 1))
            {
                errors.Add(
                    "RendererのAnimation binding pathが重複しています: " +
                    AnimationUtility.CalculateTransformPath(duplicate.First().transform, options.AvatarRoot.transform) +
                    " / " + duplicate.First().GetType().Name);
            }

            foreach (var issue in TransformationBankMaterialCompatibility.FindIssues(options))
            {
                errors.Add(issue.ValidationMessage);
            }
            return errors;
        }

        public static TransformationBankClipGenerationResult Generate(
            TransformationBankClipGenerationOptions options)
        {
            var errors = Validate(options);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }

            string outputParent;
            TryNormalizeAssetFolder(options.OutputFolder, out outputParent);
            EnsureAssetFolder(outputParent);

            var pairName = SanitizeAssetName(
                options.OutfitA.name + "_" + options.OutfitB.name + "_" + StyleLabel(options.Style));
            var outputFolder = CreateUniqueFolder(outputParent, pairName);
            var materialFolder = CreateFolder(outputFolder, "Materials");
            var clipFolder = CreateFolder(outputFolder, "Clips");

            try
            {
                var renderersA = OrderRenderers(CollectRenderers(options.OutfitA), options.AvatarRoot.transform);
                var renderersB = OrderRenderers(CollectRenderers(options.OutfitB), options.AvatarRoot.transform);
                var sourceMaterials = renderersA.Concat(renderersB)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Distinct()
                    .OrderBy(material => material.name, StringComparer.Ordinal)
                    .ThenBy(material => material.GetInstanceID())
                    .ToArray();
                var generatedMaterials = new List<Material>();
                var roleMaterials = new Dictionary<MaterialRoleKey, Material>();

                foreach (var source in sourceMaterials)
                {
                    foreach (TransformationBankRole role in Enum.GetValues(typeof(TransformationBankRole)))
                    {
                        var generated = CreateRoleMaterial(
                            source,
                            role,
                            options.Style,
                            options.EffectIntensity,
                            options.ApplyRecommendedPreset,
                            materialFolder);
                        roleMaterials.Add(new MaterialRoleKey(source, role), generated);
                        generatedMaterials.Add(generated);
                    }
                }

                var aToB = CreateTransitionClip(
                    options.AvatarRoot.transform,
                    options.OutfitA,
                    renderersA,
                    options.OutfitB,
                    renderersB,
                    roleMaterials,
                    options.Duration,
                    options.OutfitA.name + "_To_" + options.OutfitB.name + "_" + StyleLabel(options.Style));
                var aToBPath = AssetDatabase.GenerateUniqueAssetPath(
                    clipFolder + "/" + SanitizeAssetName(aToB.name) + ".anim");
                AssetDatabase.CreateAsset(aToB, aToBPath);

                var bToA = CreateTransitionClip(
                    options.AvatarRoot.transform,
                    options.OutfitB,
                    renderersB,
                    options.OutfitA,
                    renderersA,
                    roleMaterials,
                    options.Duration,
                    options.OutfitB.name + "_To_" + options.OutfitA.name + "_" + StyleLabel(options.Style));
                var bToAPath = AssetDatabase.GenerateUniqueAssetPath(
                    clipFolder + "/" + SanitizeAssetName(bToA.name) + ".anim");
                AssetDatabase.CreateAsset(bToA, bToAPath);

                var report = ScriptableObject.CreateInstance<TransformationBankGenerationReport>();
                report.name = "Transformation Bank Generation Report";
                report.Initialize(
                    outputFolder,
                    AnimationUtility.CalculateTransformPath(options.OutfitA.transform, options.AvatarRoot.transform),
                    AnimationUtility.CalculateTransformPath(options.OutfitB.transform, options.AvatarRoot.transform),
                    options.Style,
                    options.Duration,
                    options.EffectIntensity,
                    sourceMaterials,
                    generatedMaterials.ToArray(),
                    aToB,
                    bToA);
                var reportPath = AssetDatabase.GenerateUniqueAssetPath(
                    outputFolder + "/TransformationBankGenerationReport.asset");
                AssetDatabase.CreateAsset(report, reportPath);

                AssetDatabase.SaveAssets();
                return new TransformationBankClipGenerationResult
                {
                    OutputFolder = outputFolder,
                    AToBClip = aToB,
                    BToAClip = bToA,
                    Materials = generatedMaterials.ToArray(),
                    Report = report,
                };
            }
            catch
            {
                AssetDatabase.DeleteAsset(outputFolder);
                AssetDatabase.SaveAssets();
                throw;
            }
        }

        static Renderer[] CollectRenderers(GameObject root)
        {
            return root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
                .ToArray();
        }

        static Renderer[] OrderRenderers(Renderer[] renderers, Transform avatarRoot)
        {
            return renderers
                .OrderBy(renderer => AnimationUtility.CalculateTransformPath(renderer.transform, avatarRoot),
                    StringComparer.Ordinal)
                .ThenBy(renderer => renderer.GetType().Name, StringComparer.Ordinal)
                .ToArray();
        }

        static Material CreateRoleMaterial(
            Material source,
            TransformationBankRole role,
            TransformationBankStyle style,
            float effectIntensity,
            bool applyRecommendedPreset,
            string materialFolder)
        {
            var clone = new Material(source)
            {
                name = source.name + " / Transformation Bank / " + StyleLabel(style) + " / " + role,
            };
            clone.SetInteger(RoleProperty, (int)role);
            clone.SetInteger(StyleProperty, (int)style);
            clone.SetFloat(EffectIntensityProperty, effectIntensity);
            clone.SetFloat(ProgressProperty, role == TransformationBankRole.Incoming ? 1.0f : 0.0f);
            if (applyRecommendedPreset)
            {
                TransformationBankMaterialPreset.Apply(clone, style);
            }

            var path = AssetDatabase.GenerateUniqueAssetPath(
                materialFolder + "/" + SanitizeAssetName(source.name + "_" + StyleLabel(style) + "_" + role) +
                ".mat");
            AssetDatabase.CreateAsset(clone, path);
            return clone;
        }

        static AnimationClip CreateTransitionClip(
            Transform avatarRoot,
            GameObject sourceRoot,
            Renderer[] sourceRenderers,
            GameObject targetRoot,
            Renderer[] targetRenderers,
            IReadOnlyDictionary<MaterialRoleKey, Material> roleMaterials,
            float duration,
            string clipName)
        {
            var clip = new AnimationClip
            {
                name = clipName,
                frameRate = FrameRate,
                legacy = false,
            };

            SetActiveCurve(clip, avatarRoot, sourceRoot.transform, duration, false);
            SetActiveCurve(clip, avatarRoot, targetRoot.transform, duration, true);
            SetRendererCurves(
                clip,
                avatarRoot,
                sourceRenderers,
                TransformationBankRole.Outgoing,
                roleMaterials,
                duration);
            SetRendererCurves(
                clip,
                avatarRoot,
                targetRenderers,
                TransformationBankRole.Incoming,
                roleMaterials,
                duration);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        static void SetActiveCurve(
            AnimationClip clip,
            Transform avatarRoot,
            Transform outfitRoot,
            float duration,
            bool activeAtEnd)
        {
            AnimationCurve curve;
            if (activeAtEnd)
            {
                curve = new AnimationCurve(
                    new Keyframe(0.0f, 1.0f),
                    new Keyframe(duration, 1.0f));
            }
            else
            {
                var holdTime = Mathf.Max(0.0f, duration - 1.0f / FrameRate);
                curve = new AnimationCurve(
                    new Keyframe(0.0f, 1.0f),
                    new Keyframe(holdTime, 1.0f),
                    new Keyframe(duration, 0.0f));
            }
            SetConstantTangents(curve);

            var binding = EditorCurveBinding.FloatCurve(
                AnimationUtility.CalculateTransformPath(outfitRoot, avatarRoot),
                typeof(GameObject),
                "m_IsActive");
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        static void SetRendererCurves(
            AnimationClip clip,
            Transform avatarRoot,
            IEnumerable<Renderer> renderers,
            TransformationBankRole role,
            IReadOnlyDictionary<MaterialRoleKey, Material> roleMaterials,
            float duration)
        {
            foreach (var renderer in renderers)
            {
                var path = AnimationUtility.CalculateTransformPath(renderer.transform, avatarRoot);
                var progressBinding = EditorCurveBinding.FloatCurve(
                    path,
                    renderer.GetType(),
                    MaterialProgressBinding);
                var progressCurve = AnimationCurve.Linear(0.0f, 0.0f, duration, 1.0f);
                AnimationUtility.SetEditorCurve(clip, progressBinding, progressCurve);

                var materials = renderer.sharedMaterials;
                for (var slot = 0; slot < materials.Length; slot++)
                {
                    var binding = EditorCurveBinding.PPtrCurve(
                        path,
                        renderer.GetType(),
                        MaterialArrayPrefix + slot + "]");
                    var keyframes = new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0.0f,
                            value = roleMaterials[new MaterialRoleKey(materials[slot], role)],
                        },
                    };
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
                }
            }
        }

        static void SetConstantTangents(AnimationCurve curve)
        {
            for (var index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
            }
        }

        static bool IsDescendantOf(Transform candidate, Transform root)
        {
            return candidate != root && candidate.IsChildOf(root);
        }

        internal static bool TryNormalizeAssetFolder(string path, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim().TrimEnd('/');
            if (normalized != "Assets" && !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return false;
            }
            return normalized.Split('/').All(segment => segment != "." && segment != "..");
        }

        internal static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    var guid = AssetDatabase.CreateFolder(current, parts[index]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new InvalidOperationException("出力Folderを作成できません: " + next);
                    }
                }
                current = next;
            }
        }

        static string CreateUniqueFolder(string parent, string baseName)
        {
            for (var suffix = 0; suffix < 10000; suffix++)
            {
                var name = suffix == 0 ? baseName : baseName + "_" + suffix;
                var candidate = parent + "/" + name;
                if (AssetDatabase.IsValidFolder(candidate))
                {
                    continue;
                }
                var guid = AssetDatabase.CreateFolder(parent, name);
                if (!string.IsNullOrEmpty(guid))
                {
                    return candidate;
                }
            }
            throw new InvalidOperationException("一意な出力Folderを作成できません。");
        }

        static string CreateFolder(string parent, string name)
        {
            var guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException("出力Folderを作成できません: " + parent + "/" + name);
            }
            return parent + "/" + name;
        }

        internal static string SanitizeAssetName(string value)
        {
            var invalid = new HashSet<char>(System.IO.Path.GetInvalidFileNameChars());
            var sanitized = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "TransformationBank" : sanitized;
        }

        internal static string StyleLabel(TransformationBankStyle style)
        {
            switch (style)
            {
                case TransformationBankStyle.CosmicRift:
                    return "Cosmic Rift";
                case TransformationBankStyle.MagicalSparkle:
                    return "Magical Sparkle";
                case TransformationBankStyle.ManaMist:
                    return "Mana Mist";
                default:
                    return style.ToString();
            }
        }

        readonly struct MaterialRoleKey : IEquatable<MaterialRoleKey>
        {
            readonly int materialInstanceId;
            readonly TransformationBankRole role;

            public MaterialRoleKey(Material material, TransformationBankRole roleValue)
            {
                materialInstanceId = material.GetInstanceID();
                role = roleValue;
            }

            public bool Equals(MaterialRoleKey other)
            {
                return materialInstanceId == other.materialInstanceId && role == other.role;
            }

            public override bool Equals(object obj)
            {
                return obj is MaterialRoleKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return materialInstanceId * 397 ^ (int)role;
                }
            }
        }
    }
}
