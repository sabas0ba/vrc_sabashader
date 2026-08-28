using UnityEditor;
using UnityEngine;

namespace SabaShader.Samples.Editor
{
    [CustomEditor(typeof(AdvancedShaderDemoObject))]
    [CanEditMultipleObjects]
    public sealed class AdvancedShaderDemoObjectEditor : UnityEditor.Editor
    {
        SerializedProperty feature;
        SerializedProperty progress;
        SerializedProperty animateInPlayMode;
        SerializedProperty animationSpeed;

        void OnEnable()
        {
            feature = serializedObject.FindProperty("feature");
            progress = serializedObject.FindProperty("progress");
            animateInPlayMode = serializedObject.FindProperty("animateInPlayMode");
            animationSpeed = serializedObject.FindProperty("animationSpeed");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "SAMPLE ONLY / サンプル専用\n" +
                "デモ表示用の一時Material、Texture、Meshを生成します。" +
                "アバターやワールドへ追加しないでください。",
                MessageType.Warning);

            serializedObject.Update();
            EditorGUILayout.PropertyField(feature, new GUIContent("Demo Feature"));

            if (IsTransitionFeature(feature.enumValueIndex))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Transition Preview", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    animateInPlayMode,
                    new GUIContent("Auto Animate in Play Mode"));

                using (new EditorGUI.DisabledScope(!animateInPlayMode.boolValue))
                {
                    EditorGUILayout.PropertyField(animationSpeed, new GUIContent("Animation Speed"));
                }

                using (new EditorGUI.DisabledScope(animateInPlayMode.boolValue))
                {
                    EditorGUILayout.PropertyField(progress, new GUIContent("Progress"));
                }

                if (animateInPlayMode.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        "Play ModeではProgressを自動更新します。手動操作する場合はAuto Animateを無効にしてください。",
                        MessageType.Info);
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Demo Preview"))
            {
                foreach (var inspectedTarget in targets)
                {
                    ((AdvancedShaderDemoObject)inspectedTarget).Apply();
                }
            }

            EditorGUILayout.HelpBox(
                "実利用ではこのComponentではなくMaterialを設定し、必要に応じて " +
                "material._io_github_sabas0ba_transition_Progress をAnimation Controllerから制御します。",
                MessageType.None);
        }

        static bool IsTransitionFeature(int value)
        {
            return value >= (int)AdvancedShaderDemoObject.Feature.UpwardDissolve;
        }
    }
}
