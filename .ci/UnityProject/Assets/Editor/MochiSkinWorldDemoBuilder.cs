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
    /// <summary>Mochi SkinをVRCSDKなしで確認するWorld展示形式のUPM sampleを生成する。</summary>
    public static class MochiSkinWorldDemoBuilder
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

                return $"Assets/Samples/{package.displayName}/{package.version}/Mochi Skin World Demo";
            }
        }

        public static string ScenePath => SampleDirectory + "/MochiSkinWorldDemo.unity";

        [MenuItem("SabaShader/Demo/Build Mochi Skin World Demo")]
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
                throw new InvalidOperationException("Mochi Skin World Demo にCameraがありません。");
            }

            Directory.CreateDirectory(outputDirectory);
            CaptureView(
                camera,
                Path.Combine(outputDirectory, "mochi_skin_world_demo.png"),
                1920,
                1080);
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
            Debug.Log("[MochiSkinWorldDemoBuilder] 生成しました: " + ScenePath);
        }

        static Type FindDemoComponentType()
        {
            const string fullName = "SabaShader.Samples.MochiSkinWorldDemoObject";
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(fullName + " が見つかりません。先にsampleをAssetsへ配置してください。");
            }

            return type;
        }

        static void PopulateScene(Type componentType)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.29f, 0.32f, 1.0f);

            var root = new GameObject("Mochi Skin World Demo");
            CreateCamera(root.transform);
            CreateLights(root.transform);
            CreateText(
                root.transform,
                "Title",
                "SabaShader / Mochi Skin World Demo",
                new Vector3(0.0f, 1.75f, -0.45f),
                0.075f,
                58,
                Color.white);

            CreatePatch(root.transform, componentType, "Rest Surface", new Vector3(-1.12f, 0.25f, 0.0f), false);
            CreatePatch(root.transform, componentType, "Contact Driven Surface", new Vector3(1.12f, 0.25f, 0.0f), true);
            CreateText(
                root.transform,
                "Rest Label",
                "REST / Pressure = 0",
                new Vector3(-1.12f, -0.68f, -0.45f),
                0.036f,
                44,
                new Color(0.82f, 0.85f, 0.91f, 1.0f));
            CreateText(
                root.transform,
                "Contact Label",
                "4 CONTACT RECEIVERS",
                new Vector3(1.12f, -0.68f, -0.45f),
                0.036f,
                44,
                new Color(1.0f, 0.79f, 0.72f, 1.0f));
            CreateText(
                root.transform,
                "Instructions",
                "Enter Play Mode to animate proximity pressure  /  Select the right surface to edit Pressure 0-3",
                new Vector3(0.0f, -1.25f, -0.45f),
                0.038f,
                38,
                new Color(0.68f, 0.72f, 0.82f, 1.0f));
        }

        static void CreatePatch(Transform parent, Type componentType, string name, Vector3 position, bool contactDriven)
        {
            var patch = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            patch.transform.SetParent(parent, false);
            patch.transform.localPosition = position;
            patch.transform.localRotation = Quaternion.Euler(-4.0f, contactDriven ? 13.0f : -13.0f, 0.0f);

            var component = patch.AddComponent(componentType);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("pressure0").floatValue = contactDriven ? 0.95f : 0.0f;
            serialized.FindProperty("pressure1").floatValue = contactDriven ? 0.68f : 0.0f;
            serialized.FindProperty("pressure2").floatValue = contactDriven ? 0.42f : 0.0f;
            serialized.FindProperty("pressure3").floatValue = contactDriven ? 0.82f : 0.0f;
            serialized.FindProperty("animateInPlayMode").boolValue = contactDriven;

            if (contactDriven)
            {
                for (var index = 0; index < 4; index++)
                {
                    var probe = CreateProbe(patch.transform, index);
                    serialized.FindProperty("probe" + index).objectReferenceValue = probe.transform;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            componentType.GetMethod("Apply", BindingFlags.Instance | BindingFlags.Public)?.Invoke(component, null);
        }

        static GameObject CreateProbe(Transform parent, int index)
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probe.name = "Contact Probe " + index;
            probe.transform.SetParent(parent, false);
            probe.transform.localScale = Vector3.one * 0.13f;
            var collider = probe.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return probe;
        }

        static void CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0.0f, 0.15f, -6.0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.016f, 0.027f, 1.0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50.0f;
        }

        static void CreateLights(Transform parent)
        {
            var keyObject = new GameObject("Key Light");
            keyObject.transform.SetParent(parent, false);
            keyObject.transform.localRotation = Quaternion.Euler(33.0f, -42.0f, 0.0f);
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1.0f, 0.82f, 0.72f, 1.0f);
            key.intensity = 1.25f;
            key.shadows = LightShadows.None;

            var fillObject = new GameObject("Fill Light");
            fillObject.transform.SetParent(parent, false);
            fillObject.transform.localRotation = Quaternion.Euler(18.0f, 132.0f, 0.0f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.52f, 0.67f, 1.0f, 1.0f);
            fill.intensity = 0.62f;
            fill.shadows = LightShadows.None;
        }

        static void CreateText(
            Transform parent,
            string name,
            string text,
            Vector3 position,
            float characterSize,
            int fontSize,
            Color color)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position;
            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = characterSize;
            textMesh.fontSize = fontSize;
            textMesh.color = color;
        }

        static void CaptureView(Camera camera, string output, int width, int height)
        {
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
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(output, texture.EncodeToPNG());
                Debug.Log("[MochiSkinWorldDemoBuilder] 書き出しました: " + output);
            }
            finally
            {
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
