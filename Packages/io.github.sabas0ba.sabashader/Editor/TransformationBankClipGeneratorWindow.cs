using System;
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
                "元のScene、Material、Animator Controllerは変更しません。" +
                "Transformation Bankを有効化済みのIllust2DまたはNonToon Materialが必要です。",
                MessageType.Info);

            EditorGUILayout.Space();
            avatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar Root", avatarRoot, typeof(GameObject), true);
            outfitA = (GameObject)EditorGUILayout.ObjectField("衣装 A", outfitA, typeof(GameObject), true);
            outfitB = (GameObject)EditorGUILayout.ObjectField("衣装 B", outfitB, typeof(GameObject), true);

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
            var options = new TransformationBankClipGenerationOptions
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
            var errors = TransformationBankClipGenerator.Validate(options);
            if (errors.Count > 0)
            {
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
    }
}
