using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SabaShader.EditorTools
{
    public sealed class TransformationBankClipGeneratorWindow : EditorWindow
    {
        GameObject avatarRoot;
        GameObject outfitA;
        GameObject outfitB;
        TransformationBankStyle style = TransformationBankStyle.Arcane;
        float duration = 2.0f;
        float effectIntensity = 1.6f;
        bool applyRecommendedPreset = true;
        string outputFolder = "Assets/SabaShader/TransformationBank";
        Vector2 scroll;
        IReadOnlyList<TransformationBankMaterialIssue> materialIssues =
            Array.Empty<TransformationBankMaterialIssue>();
        Shader[] compatibleShaders = Array.Empty<Shader>();
        Material[] compatibleMaterials = Array.Empty<Material>();
        int selectedShaderIndex;
        int selectedMaterialIndex;
        bool materialSectionExpanded = true;

        [MenuItem("Tools/SabaShader/Transformation Bank Clip Generator")]
        static void Open()
        {
            var window = GetWindow<TransformationBankClipGeneratorWindow>();
            window.titleContent = new GUIContent("Transformation Bank Clips");
            window.minSize = new Vector2(480.0f, 480.0f);
            window.Show();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Transformation Bank Clip Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Clip生成は元のScene、Material、Animator Controllerを変更しません。" +
                "Material互換性セクションの割当操作のみ、選択RendererのMaterial SlotをUndo対応で変更します。",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            avatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar Root", avatarRoot, typeof(GameObject), true);
            outfitA = (GameObject)EditorGUILayout.ObjectField("衣装 A", outfitA, typeof(GameObject), true);
            outfitB = (GameObject)EditorGUILayout.ObjectField("衣装 B", outfitB, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshMaterialCompatibility();
            }

            EditorGUILayout.Space();
            style = (TransformationBankStyle)EditorGUILayout.EnumPopup("VFX Style", style);
            duration = EditorGUILayout.FloatField("遷移時間 (秒)", duration);
            effectIntensity = EditorGUILayout.Slider("Effect Intensity", effectIntensity, 0.0f, 4.0f);
            applyRecommendedPreset = EditorGUILayout.Toggle("Style推奨値を適用", applyRecommendedPreset);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                outputFolder = EditorGUILayout.TextField("出力先", outputFolder);
                if (GUILayout.Button("選択", GUILayout.Width(64.0f)))
                {
                    SelectOutputFolder();
                }
            }

            DrawMaterialCompatibility();

            EditorGUILayout.HelpBox(
                "A→BとB→AのClip、Incoming／Outgoing Material複製、Generation Reportを生成します。" +
                "Particle SystemとAnimator Controllerへの組み込みは生成しません。",
                MessageType.None);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(avatarRoot == null || outfitA == null || outfitB == null))
            {
                if (GUILayout.Button("Animation Clipを生成", GUILayout.Height(34.0f)))
                {
                    Generate();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawMaterialCompatibility()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                materialSectionExpanded = EditorGUILayout.Foldout(
                    materialSectionExpanded,
                    "Material互換性",
                    true);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("再スキャン", GUILayout.Width(88.0f)))
                {
                    RefreshMaterialCompatibility();
                }
            }
            if (!materialSectionExpanded)
            {
                return;
            }

            if (avatarRoot == null || outfitA == null || outfitB == null)
            {
                EditorGUILayout.HelpBox(
                    "Avatar Rootと衣装A/Bを選択すると、Material互換性を検査します。",
                    MessageType.Info);
                return;
            }
            if (materialIssues.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "選択した衣装のMaterialはTransformation Bankを利用できます。",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                materialIssues.Count + "件のMaterial SlotでTransformation Bankを利用できません。" +
                "元Materialアセットを変更せず、互換Materialを生成するか既存Materialを割り当てられます。",
                MessageType.Warning);

            DrawShaderCandidates();
            DrawMaterialCandidates();

            foreach (var issue in materialIssues)
            {
                DrawMaterialIssue(issue);
            }
        }

        void DrawShaderCandidates()
        {
            EditorGUILayout.LabelField("利用可能なShader", EditorStyles.boldLabel);
            if (compatibleShaders.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Transformation Bank対応Shaderが見つかりません。" +
                    "Tools/SabaShader/Select Modulesで __TransformationBank を有効にし、" +
                    "Apply後のShaderコンパイル完了を待って再スキャンしてください。",
                    MessageType.Warning);
                return;
            }

            selectedShaderIndex = EditorGUILayout.Popup(
                "生成元Shader",
                Mathf.Clamp(selectedShaderIndex, 0, compatibleShaders.Length - 1),
                compatibleShaders.Select(ShaderLabel).ToArray());
        }

        void DrawMaterialCandidates()
        {
            EditorGUILayout.LabelField("利用可能なProject Material", EditorStyles.boldLabel);
            if (compatibleMaterials.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "既存のTransformation Bank対応Materialはありません。互換Materialを生成してください。",
                    MessageType.Info);
                return;
            }

            selectedMaterialIndex = EditorGUILayout.Popup(
                "割当Material",
                Mathf.Clamp(selectedMaterialIndex, 0, compatibleMaterials.Length - 1),
                compatibleMaterials.Select(MaterialLabel).ToArray());
        }

        void DrawMaterialIssue(TransformationBankMaterialIssue issue)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    issue.OutfitLabel + " / " + issue.RendererPath + " / Slot " + issue.MaterialSlot,
                    EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("現在のMaterial", issue.Material, typeof(Material), false);
                }
                EditorGUILayout.LabelField(
                    "現在のShader",
                    issue.Material != null && issue.Material.shader != null
                        ? issue.Material.shader.name
                        : "(なし)");
                EditorGUILayout.LabelField("不足Property", string.Join(", ", issue.MissingProperties));

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(compatibleShaders.Length == 0))
                    {
                        if (GUILayout.Button("互換Materialを生成して割当"))
                        {
                            CreateAndAssignMaterial(issue);
                        }
                    }
                    using (new EditorGUI.DisabledScope(compatibleMaterials.Length == 0))
                    {
                        if (GUILayout.Button("選択Materialを割当"))
                        {
                            AssignExistingMaterial(issue);
                        }
                    }
                }
            }
        }

        void RefreshMaterialCompatibility()
        {
            materialIssues = TransformationBankMaterialCompatibility.FindIssues(CurrentOptions());
            compatibleShaders = TransformationBankMaterialCompatibility.FindCompatibleShaders();
            compatibleMaterials = TransformationBankMaterialCompatibility.FindCompatibleMaterials();
            selectedShaderIndex = compatibleShaders.Length == 0
                ? 0
                : Mathf.Clamp(selectedShaderIndex, 0, compatibleShaders.Length - 1);
            selectedMaterialIndex = compatibleMaterials.Length == 0
                ? 0
                : Mathf.Clamp(selectedMaterialIndex, 0, compatibleMaterials.Length - 1);
            Repaint();
        }

        void CreateAndAssignMaterial(TransformationBankMaterialIssue issue)
        {
            try
            {
                var generated = TransformationBankMaterialCompatibility.CreateAndAssign(
                    issue,
                    compatibleShaders[selectedShaderIndex],
                    style,
                    effectIntensity,
                    outputFolder.TrimEnd('/', '\\') + "/PreparedMaterials");
                Selection.activeObject = generated;
                EditorGUIUtility.PingObject(generated);
                RefreshMaterialCompatibility();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Material生成失敗", exception.Message, "OK");
            }
        }

        void AssignExistingMaterial(TransformationBankMaterialIssue issue)
        {
            try
            {
                var material = compatibleMaterials[selectedMaterialIndex];
                TransformationBankMaterialCompatibility.Assign(issue, material);
                Selection.activeObject = material;
                EditorGUIUtility.PingObject(material);
                RefreshMaterialCompatibility();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Material割当失敗", exception.Message, "OK");
            }
        }

        static string ShaderLabel(Shader shader)
        {
            var path = AssetDatabase.GetAssetPath(shader);
            return string.IsNullOrEmpty(path) ? shader.name : shader.name + " — " + path;
        }

        static string MaterialLabel(Material material)
        {
            var path = AssetDatabase.GetAssetPath(material);
            return material.name + " — " + material.shader.name + " — " + path;
        }

        void SelectOutputFolder()
        {
            var selected = EditorUtility.OpenFolderPanel("Transformation Bank出力先", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            var assetsPath = Application.dataPath.Replace('\\', '/');
            var normalized = selected.Replace('\\', '/');
            if (!normalized.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Transformation Bank", "Assets以下のFolderを選択してください。", "OK");
                return;
            }
            outputFolder = "Assets" + normalized.Substring(assetsPath.Length);
        }

        void Generate()
        {
            var options = CurrentOptions();
            var errors = TransformationBankClipGenerator.Validate(options);
            if (errors.Count > 0)
            {
                RefreshMaterialCompatibility();
                EditorUtility.DisplayDialog("生成できません", string.Join("\n", errors), "OK");
                return;
            }

            try
            {
                var result = TransformationBankClipGenerator.Generate(options);
                Selection.activeObject = result.Report;
                EditorGUIUtility.PingObject(result.Report);
                EditorUtility.DisplayDialog(
                    "生成完了",
                    "Animation ClipとMaterialを生成しました。\n" + result.OutputFolder,
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("生成失敗", exception.Message, "OK");
            }
        }

        TransformationBankClipGenerationOptions CurrentOptions()
        {
            return new TransformationBankClipGenerationOptions
            {
                AvatarRoot = avatarRoot,
                OutfitA = outfitA,
                OutfitB = outfitB,
                Style = style,
                Duration = duration,
                EffectIntensity = effectIntensity,
                ApplyRecommendedPreset = applyRecommendedPreset,
                OutputFolder = outputFolder,
            };
        }
    }
}
