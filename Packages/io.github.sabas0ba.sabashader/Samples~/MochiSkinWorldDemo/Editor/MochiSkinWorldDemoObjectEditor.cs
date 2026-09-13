using UnityEditor;
using UnityEngine;

namespace SabaShader.Samples.Editor
{
    [CustomEditor(typeof(MochiSkinWorldDemoObject))]
    [CanEditMultipleObjects]
    public sealed class MochiSkinWorldDemoObjectEditor : UnityEditor.Editor
    {
        SerializedProperty skinFinish;
        SerializedProperty pressure0;
        SerializedProperty pressure1;
        SerializedProperty pressure2;
        SerializedProperty pressure3;
        SerializedProperty depth;
        SerializedProperty outerBulge;
        SerializedProperty indentSpread;
        SerializedProperty edgeSoftness;
        SerializedProperty contourIrregularity;
        SerializedProperty irregularityScale;
        SerializedProperty contactThreshold;
        SerializedProperty contactSoftness;
        SerializedProperty normalStrength;
        SerializedProperty animateInPlayMode;
        SerializedProperty animationSpeed;

        void OnEnable()
        {
            skinFinish = serializedObject.FindProperty("skinFinish");
            pressure0 = serializedObject.FindProperty("pressure0");
            pressure1 = serializedObject.FindProperty("pressure1");
            pressure2 = serializedObject.FindProperty("pressure2");
            pressure3 = serializedObject.FindProperty("pressure3");
            depth = serializedObject.FindProperty("depth");
            outerBulge = serializedObject.FindProperty("outerBulge");
            indentSpread = serializedObject.FindProperty("indentSpread");
            edgeSoftness = serializedObject.FindProperty("edgeSoftness");
            contourIrregularity = serializedObject.FindProperty("contourIrregularity");
            irregularityScale = serializedObject.FindProperty("irregularityScale");
            contactThreshold = serializedObject.FindProperty("contactThreshold");
            contactSoftness = serializedObject.FindProperty("contactSoftness");
            normalStrength = serializedObject.FindProperty("normalStrength");
            animateInPlayMode = serializedObject.FindProperty("animateInPlayMode");
            animationSpeed = serializedObject.FindProperty("animationSpeed");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "SAMPLE ONLY / サンプル専用\n" +
                "球、円柱、板、capsuleによる4個のContact Receiver Proximityを再現するWorld展示用Componentです。" +
                "アバターやアップロードするWorldへ追加しないでください。",
                MessageType.Warning);

            serializedObject.Update();
            EditorGUILayout.LabelField("NonToon Skin", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(skinFinish, new GUIContent("Skin Finish"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Contact Receiver Preview", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(pressure0, new GUIContent("Pressure 0 / Sphere"));
            EditorGUILayout.PropertyField(pressure1, new GUIContent("Pressure 1 / Cylinder"));
            EditorGUILayout.PropertyField(pressure2, new GUIContent("Pressure 2 / Plate"));
            EditorGUILayout.PropertyField(pressure3, new GUIContent("Pressure 3 / Capsule"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cushion Profile", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(depth, new GUIContent("Depth"));
            EditorGUILayout.PropertyField(outerBulge, new GUIContent("Outer Bulge"));
            EditorGUILayout.PropertyField(indentSpread, new GUIContent("Indent Spread"));
            EditorGUILayout.PropertyField(edgeSoftness, new GUIContent("Edge Softness"));
            EditorGUILayout.PropertyField(contourIrregularity, new GUIContent("Contour Irregularity"));
            EditorGUILayout.PropertyField(irregularityScale, new GUIContent("Irregularity Scale"));
            EditorGUILayout.PropertyField(normalStrength, new GUIContent("Normal Strength"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Contact Onset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(contactThreshold, new GUIContent("Contact Threshold"));
            EditorGUILayout.PropertyField(contactSoftness, new GUIContent("Contact Softness"));
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
                "Play Modeではprobeが接触面へ到達するまでProximityを変形へ反映せず、" +
                "接触後のpenetrationだけを凹みに変換します。実際のアバターではVRC Contact Receiverと" +
                "FX Animatorから同じmaterial propertyを制御します。",
                MessageType.Info);
        }
    }
}
