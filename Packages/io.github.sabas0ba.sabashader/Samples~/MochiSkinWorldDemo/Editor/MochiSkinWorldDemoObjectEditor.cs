using UnityEditor;
using UnityEngine;

namespace SabaShader.Samples.Editor
{
    [CustomEditor(typeof(MochiSkinWorldDemoObject))]
    [CanEditMultipleObjects]
    public sealed class MochiSkinWorldDemoObjectEditor : UnityEditor.Editor
    {
        SerializedProperty pressure0;
        SerializedProperty pressure1;
        SerializedProperty pressure2;
        SerializedProperty pressure3;
        SerializedProperty animateInPlayMode;
        SerializedProperty animationSpeed;

        void OnEnable()
        {
            pressure0 = serializedObject.FindProperty("pressure0");
            pressure1 = serializedObject.FindProperty("pressure1");
            pressure2 = serializedObject.FindProperty("pressure2");
            pressure3 = serializedObject.FindProperty("pressure3");
            animateInPlayMode = serializedObject.FindProperty("animateInPlayMode");
            animationSpeed = serializedObject.FindProperty("animationSpeed");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "SAMPLE ONLY / サンプル専用\n" +
                "4個のContact Receiver Proximityを再現するWorld展示用Componentです。" +
                "アバターやアップロードするWorldへ追加しないでください。",
                MessageType.Warning);

            serializedObject.Update();
            EditorGUILayout.LabelField("Contact Receiver Preview", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(pressure0, new GUIContent("Pressure 0"));
            EditorGUILayout.PropertyField(pressure1, new GUIContent("Pressure 1"));
            EditorGUILayout.PropertyField(pressure2, new GUIContent("Pressure 2"));
            EditorGUILayout.PropertyField(pressure3, new GUIContent("Pressure 3"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(
                animateInPlayMode,
                new GUIContent("Auto Animate in Play Mode"));
            using (new EditorGUI.DisabledScope(!animateInPlayMode.boolValue))
            {
                EditorGUILayout.PropertyField(animationSpeed, new GUIContent("Animation Speed"));
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Demo Preview"))
            {
                foreach (var inspectedTarget in targets)
                {
                    ((MochiSkinWorldDemoObject)inspectedTarget).Apply();
                }
            }

            EditorGUILayout.HelpBox(
                "Play Modeでは4個のprobeとPressureを位相差付きで動かします。" +
                "実際のアバターではVRC Contact ReceiverとFX Animatorから同じmaterial propertyを制御します。",
                MessageType.Info);
        }
    }
}
