using UnityEditor;
using UnityEngine;

namespace SabaShader.Samples.Editor
{
    [CustomEditor(typeof(TransformationBankDemoController))]
    [CanEditMultipleObjects]
    public sealed class TransformationBankDemoControllerEditor : UnityEditor.Editor
    {
        SerializedProperty style;
        SerializedProperty progress;
        SerializedProperty animateInPlayMode;
        SerializedProperty animationSpeed;
        SerializedProperty effectIntensity;
        SerializedProperty particleIntensity;
        SerializedProperty particleSize;

        void OnEnable()
        {
            style = serializedObject.FindProperty("style");
            progress = serializedObject.FindProperty("progress");
            animateInPlayMode = serializedObject.FindProperty("animateInPlayMode");
            animationSpeed = serializedObject.FindProperty("animationSpeed");
            effectIntensity = serializedObject.FindProperty("effectIntensity");
            particleIntensity = serializedObject.FindProperty("particleIntensity");
            particleSize = serializedObject.FindProperty("particleSize");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "SAMPLE ONLY / サンプル専用\n" +
                "旧衣装、新衣装用の一時MaterialとParticle previewを生成します。" +
                "アバターやワールドへこのComponentを追加しないでください。",
                MessageType.Warning);

            serializedObject.Update();
            EditorGUILayout.PropertyField(style, new GUIContent("VFX Style"));
            EditorGUILayout.PropertyField(effectIntensity, new GUIContent("Effect Intensity"));
            EditorGUILayout.PropertyField(particleIntensity, new GUIContent("Particle Intensity"));
            EditorGUILayout.PropertyField(particleSize, new GUIContent("Particle Size"));
            EditorGUILayout.PropertyField(animateInPlayMode, new GUIContent("Auto Animate in Play Mode"));
            using (new EditorGUI.DisabledScope(!animateInPlayMode.boolValue))
            {
                EditorGUILayout.PropertyField(animationSpeed, new GUIContent("Animation Speed"));
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying && animateInPlayMode.boolValue))
            {
                EditorGUILayout.PropertyField(progress, new GUIContent("Progress"));
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Demo Preview"))
            {
                foreach (var inspectedTarget in targets)
                {
                    ((TransformationBankDemoController)inspectedTarget).Apply();
                }
            }

            EditorGUILayout.HelpBox(
                "Play Modeでは12 Styleが同期して自動再生します。下段は旧衣装から新衣装への固定snapshotです。\n" +
                "実利用では material._io_github_sabas0ba_transformationbank_Progress を" +
                "Animation Controllerから制御します。",
                MessageType.Info);
        }
    }
}
