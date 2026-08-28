using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaShader.CI
{
    /// <summary>UPM sample として配布する高度シェーダー機能の統合確認シーンを生成する。</summary>
    public static class AdvancedShaderDemoBuilder
    {
        public static string SampleDirectory
        {
            get
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(ShaderCompileChecker.Illust2DPath);
                if (package == null)
                {
                    throw new InvalidOperationException("SabaShader package の情報を取得できませんでした。");
                }

                return $"Assets/Samples/{package.displayName}/{package.version}/Advanced Shader Suite Demo";
            }
        }

        public static string ScenePath => SampleDirectory + "/AdvancedShaderSuiteDemo.unity";

        static readonly string[] FeatureNames =
        {
            "Decal / UV Cylinder",
            "Decal / Projected Logo",
            "Surface / Skin",
            "Surface / Fabric",
            "Spatial / Universe Rift",
            "Spatial / Starfield",
            "Spatial / Cyber Back",
            "Spatial / Mud",
            "Transition / Upward",
            "Transition / Glitch",
            "Transition / Liquid",
        };

        static readonly PrimitiveType[] PrimitiveTypes =
        {
            PrimitiveType.Cylinder,
            PrimitiveType.Cylinder,
            PrimitiveType.Sphere,
            PrimitiveType.Cube,
            PrimitiveType.Sphere,
            PrimitiveType.Sphere,
            PrimitiveType.Sphere,
            PrimitiveType.Sphere,
            PrimitiveType.Capsule,
            PrimitiveType.Capsule,
            PrimitiveType.Sphere,
        };

        static readonly float[] ProgressValues =
        {
            1.0f,
            1.0f,
            1.0f,
            1.0f,
            1.0f,
            1.0f,
            1.0f,
            1.0f,
            0.56f,
            0.48f,
            0.24f,
        };

        [MenuItem("SabaShader/Demo/Build Advanced Shader Suite Demo")]
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

        public static void CaptureBatch()
        {
            try
            {
                Capture();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Capture()
        {
            var outputDirectory = ArgumentValue("-captureOutputDirectory");
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new ArgumentException("-captureOutputDirectory <path> を指定してください。");
            }

            var failures = ShaderCompileChecker.CollectFailures();
            if (failures.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", failures));
            }

            Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var camera = UnityEngine.Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException("Advanced Shader Suite Demo に Camera がありません。");
            }

            Directory.CreateDirectory(outputDirectory);
            CaptureView(camera, Path.Combine(outputDirectory, "advanced_shader_suite_demo.png"), 2560, 1440, 0.1f, 5.15f);
            CaptureView(camera, Path.Combine(outputDirectory, "advanced_shader_surface_features.png"), 2560, 960, 1.42f, 3.35f);
            CaptureView(camera, Path.Combine(outputDirectory, "advanced_shader_transitions.png"), 2560, 480, -2.72f, 1.35f);
        }

        public static void Build()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.Illust2DPath);
            if (shader == null)
            {
                throw new InvalidOperationException("SabaShader/Illust2D をインポートできませんでした。");
            }

            var componentType = FindDemoComponentType();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PopulateScene(componentType);
            EnsureDirectory(SampleDirectory);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("シーンを保存できませんでした: " + ScenePath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[AdvancedShaderDemoBuilder] 生成しました: " + ScenePath);
        }

        static Type FindDemoComponentType()
        {
            const string fullName = "SabaShader.Samples.AdvancedShaderDemoObject";
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(fullName + " が見つかりません。先に sample を Assets へ配置してください。");
            }

            return type;
        }

        static void PopulateScene(Type componentType)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.28f, 0.34f, 1.0f);

            var root = new GameObject("Advanced Shader Suite Demo");
            CreateCamera(root.transform);
            CreateLight(root.transform);
            CreateTitle(root.transform);

            const float verticalSpacing = 2.75f;
            for (var feature = 0; feature < FeatureNames.Length; feature++)
            {
                var row = feature < 4 ? 0 : feature < 8 ? 1 : 2;
                var column = row < 2 ? feature % 4 : feature - 8;
                var columns = row < 2 ? 4 : 3;
                var horizontalSpacing = row < 2 ? 3.1f : 3.65f;
                var position = new Vector3(
                    (column - (columns - 1) * 0.5f) * horizontalSpacing,
                    (1 - row) * verticalSpacing,
                    0.0f);
                CreateFeatureObject(root.transform, componentType, feature, position);
            }
        }

        static void CreateFeatureObject(Transform parent, Type componentType, int feature, Vector3 position)
        {
            var demoObject = GameObject.CreatePrimitive(PrimitiveTypes[feature]);
            demoObject.name = $"{feature:00} {FeatureNames[feature]}";
            demoObject.transform.SetParent(parent, false);
            demoObject.transform.localPosition = position;
            demoObject.transform.localScale = ObjectScale(feature);
            if (feature <= 1)
            {
                demoObject.transform.localRotation = Quaternion.Euler(-8.0f, -18.0f, 0.0f);
            }

            var component = demoObject.AddComponent(componentType);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("feature").enumValueIndex = feature;
            serialized.FindProperty("progress").floatValue = ProgressValues[feature];
            serialized.FindProperty("animateInPlayMode").boolValue = feature >= 8;
            if (feature <= 1)
            {
                var decalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    SampleDirectory + "/Textures/DecalDemoEmblem.png");
                if (decalTexture == null)
                {
                    throw new InvalidOperationException("DecalDemoEmblem.png を読み込めませんでした。");
                }

                serialized.FindProperty("decalTextureAsset").objectReferenceValue = decalTexture;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            componentType.GetMethod("Apply", BindingFlags.Instance | BindingFlags.Public)?.Invoke(component, null);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = position + new Vector3(0.0f, -1.05f, -0.8f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = FeatureNames[feature];
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.055f;
            label.fontSize = 48;
            label.color = new Color(0.9f, 0.92f, 0.96f, 1.0f);
        }

        static Vector3 ObjectScale(int feature)
        {
            if (feature <= 1)
            {
                return new Vector3(1.35f, 0.75f, 1.35f);
            }

            if (feature == 8 || feature == 9)
            {
                return new Vector3(1.05f, 1.05f, 1.05f);
            }

            return Vector3.one * 1.45f;
        }

        static void CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0.0f, 0.1f, -12.0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.011f, 0.019f, 1.0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50.0f;
        }

        static void CreateLight(Transform parent)
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localRotation = Quaternion.Euler(42.0f, -38.0f, 0.0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.91f, 0.78f, 1.0f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
        }

        static void CreateTitle(Transform parent)
        {
            var titleObject = new GameObject("Title");
            titleObject.transform.SetParent(parent, false);
            titleObject.transform.localPosition = new Vector3(0.0f, 4.65f, -0.8f);
            var title = titleObject.AddComponent<TextMesh>();
            title.text = "SabaShader / Advanced Shader Suite";
            title.anchor = TextAnchor.MiddleCenter;
            title.alignment = TextAlignment.Center;
            title.characterSize = 0.17f;
            title.fontSize = 56;
            title.color = Color.white;
        }

        static void CaptureView(
            Camera camera,
            string output,
            int width,
            int height,
            float verticalPosition,
            float orthographicSize)
        {
            var originalPosition = camera.transform.position;
            var originalSize = camera.orthographicSize;
            var originalTarget = camera.targetTexture;
            var originalActive = RenderTexture.active;
            var target = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 4,
            };
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                camera.transform.position = new Vector3(originalPosition.x, verticalPosition, originalPosition.z);
                camera.orthographicSize = orthographicSize;
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(output, texture.EncodeToPNG());
                Debug.Log("[AdvancedShaderDemoBuilder] 書き出しました: " + output);
            }
            finally
            {
                camera.transform.position = originalPosition;
                camera.orthographicSize = originalSize;
                camera.targetTexture = originalTarget;
                RenderTexture.active = originalActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static string ArgumentValue(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
            {
                if (arguments[index] == name)
                {
                    return arguments[index + 1];
                }
            }

            return null;
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
