using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaShader.CI
{
    /// <summary>UPM sample として配布する Debug shader の確認シーンを決定的に生成する。</summary>
    public static class DebugShaderDemoBuilder
    {
        public static string SampleDirectory
        {
            get
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(ShaderCompileChecker.DebugPath);
                if (package == null)
                {
                    throw new InvalidOperationException("SabaShader package の情報を取得できませんでした。");
                }

                return $"Assets/Samples/{package.displayName}/{package.version}/Debug Shader Demo";
            }
        }

        public static string ScenePath => SampleDirectory + "/DebugShaderDemo.unity";

        static readonly string[] ModeNames =
        {
            "Wireframe",
            "Vertex Color",
            "Vertex Alpha",
            "UV0",
            "UV1",
            "UV2",
            "UV3",
            "World Position",
            "Object Position",
            "World Normal",
            "World Tangent",
            "World Bitangent",
            "Front Face",
            "Light Direction",
            "Light Color",
            "Light Attenuation",
            "View Direction",
            "View Facing",
        };

        [MenuItem("SabaShader/Demo/Build Debug Shader Demo")]
        public static void BuildAndOpen()
        {
            Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.DebugPath);
            if (shader == null)
            {
                throw new InvalidOperationException("SabaShader/Debug をインポートできませんでした。");
            }

            var componentType = FindDemoComponentType();
            var sourceSphere = GetSourceSphere();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PopulateScene(componentType, sourceSphere);
            EnsureDirectory(SampleDirectory);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("シーンを保存できませんでした: " + ScenePath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[DebugShaderDemoBuilder] 生成しました: " + ScenePath);
        }

        static Type FindDemoComponentType()
        {
            const string fullName = "SabaShader.Samples.DebugShaderDemoObject";
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(fullName + " が見つかりません。先に sample を Assets へ配置してください。");
            }

            return type;
        }

        static Mesh GetSourceSphere()
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                return primitive.GetComponent<MeshFilter>().sharedMesh;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(primitive);
            }
        }

        static void PopulateScene(Type componentType, Mesh sourceSphere)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.38f, 1.0f);

            var root = new GameObject("Debug Shader Demo");
            CreateCamera(root.transform);
            CreateLight(root.transform);
            CreateTitle(root.transform);

            const int columns = 6;
            const float horizontalSpacing = 2.15f;
            const float verticalSpacing = 2.25f;

            for (var mode = 0; mode < ModeNames.Length; mode++)
            {
                var column = mode % columns;
                var row = mode / columns;
                var position = new Vector3(
                    (column - (columns - 1) * 0.5f) * horizontalSpacing,
                    (1 - row) * verticalSpacing,
                    0.0f);

                CreateModeObject(root.transform, componentType, sourceSphere, mode, position);
            }
        }

        static void CreateModeObject(Transform parent, Type componentType, Mesh sourceSphere, int mode, Vector3 position)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"{mode:00} {ModeNames[mode]}";
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = position;
            sphere.transform.localScale = Vector3.one * 1.35f;
            sphere.GetComponent<MeshFilter>().sharedMesh = sourceSphere;

            var component = sphere.AddComponent(componentType);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("mode").intValue = mode;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            componentType.GetMethod("Apply", BindingFlags.Instance | BindingFlags.Public)?.Invoke(component, null);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = position + new Vector3(0.0f, -0.9f, -0.75f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = $"{mode:00}  {ModeNames[mode]}";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.13f;
            label.fontSize = 48;
            label.color = new Color(0.9f, 0.92f, 0.96f, 1.0f);
        }

        static void CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0.0f, 0.35f, -12.0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.014f, 0.02f, 1.0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50.0f;
        }

        static void CreateLight(Transform parent)
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localRotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.93f, 0.82f, 1.0f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
        }

        static void CreateTitle(Transform parent)
        {
            var titleObject = new GameObject("Title");
            titleObject.transform.SetParent(parent, false);
            titleObject.transform.localPosition = new Vector3(0.0f, 4.05f, -0.75f);
            var title = titleObject.AddComponent<TextMesh>();
            title.text = "SabaShader / Debug Shader Demo";
            title.anchor = TextAnchor.MiddleCenter;
            title.alignment = TextAlignment.Center;
            title.characterSize = 0.18f;
            title.fontSize = 56;
            title.color = Color.white;
        }

        static void EnsureDirectory(string assetPath)
        {
            var segments = assetPath.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
