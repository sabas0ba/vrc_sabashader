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
    /// <summary>Transformation Bankの5 StyleとSafety Cover timingを表示するUPM sampleを生成する。</summary>
    public static class TransformationBankDemoBuilder
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

                return $"Assets/Samples/{package.displayName}/{package.version}/Transformation Bank Demo";
            }
        }

        public static string ScenePath => SampleDirectory + "/TransformationBankDemo.unity";

        static readonly string[] StyleNames = { "Arcane", "Cyber", "Astral", "Gaia", "Umbra" };
        static readonly float[] TimelineProgress = { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };

        [MenuItem("SabaShader/Demo/Build Transformation Bank Demo")]
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
            Debug.Log("[TransformationBankDemoBuilder] 生成しました: " + ScenePath);
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
                throw new InvalidOperationException("Transformation Bank Demo にCameraがありません。");
            }

            Directory.CreateDirectory(outputDirectory);
            CaptureView(camera, Path.Combine(outputDirectory, "transformation_bank_demo.png"), 2560, 1440);
        }

        static Type FindDemoComponentType()
        {
            const string fullName = "SabaShader.Samples.TransformationBankDemoController";
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
            RenderSettings.ambientLight = new Color(0.2f, 0.22f, 0.3f, 1.0f);

            var root = new GameObject("Transformation Bank Demo");
            CreateCamera(root.transform);
            CreateLight(root.transform);
            CreateText(root.transform, "Title", "SabaShader / Transformation Bank", new Vector3(0.0f, 4.65f, -0.8f), 0.17f, 58, Color.white);
            CreateText(root.transform, "Style Row", "5 VFX STYLES / PLAY MODE", new Vector3(-7.4f, 3.45f, -0.8f), 0.07f, 44, new Color(0.46f, 0.76f, 1.0f, 1.0f), TextAnchor.MiddleLeft);
            CreateText(root.transform, "Timeline Row", "SAFETY TIMELINE / FIXED", new Vector3(-7.4f, -0.72f, -0.8f), 0.07f, 44, new Color(1.0f, 0.66f, 0.28f, 1.0f), TextAnchor.MiddleLeft);

            const float spacing = 2.75f;
            for (var index = 0; index < StyleNames.Length; index++)
            {
                var x = (index - 2) * spacing;
                CreateStation(
                    root.transform,
                    componentType,
                    $"Style {index:00} / {StyleNames[index]}",
                    new Vector3(x, 1.72f, 0.0f),
                    index,
                    0.5f,
                    true);
                CreateStation(
                    root.transform,
                    componentType,
                    $"Timeline {index:00} / {TimelineProgress[index]:0.00}",
                    new Vector3(x, -2.5f, 0.0f),
                    0,
                    TimelineProgress[index],
                    false);
            }
        }

        static void CreateStation(
            Transform parent,
            Type componentType,
            string name,
            Vector3 position,
            int style,
            float progress,
            bool animate)
        {
            var station = new GameObject(name);
            station.SetActive(false);
            station.transform.SetParent(parent, false);
            station.transform.localPosition = position;

            var outgoing = CreateShell(station.transform, "Outgoing / Old Outfit", 1.1f);
            var incoming = CreateShell(station.transform, "Incoming / New Outfit", 1.035f);
            var cover = CreateShell(station.transform, "Safety Cover", 0.97f);
            var label = CreateText(
                station.transform,
                "Progress Label",
                string.Empty,
                new Vector3(0.0f, -1.72f, -0.7f),
                0.052f,
                44,
                new Color(0.9f, 0.93f, 1.0f, 1.0f));

            var component = station.AddComponent(componentType);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("style").enumValueIndex = style;
            serialized.FindProperty("progress").floatValue = progress;
            serialized.FindProperty("animateInPlayMode").boolValue = animate;
            serialized.FindProperty("animationSpeed").floatValue = 0.2f;
            serialized.FindProperty("outgoingRenderer").objectReferenceValue = outgoing;
            serialized.FindProperty("incomingRenderer").objectReferenceValue = incoming;
            serialized.FindProperty("safetyCoverRenderer").objectReferenceValue = cover;
            serialized.FindProperty("progressLabel").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            station.SetActive(true);
            componentType.GetMethod("Apply", BindingFlags.Instance | BindingFlags.Public)?.Invoke(component, null);
        }

        static Renderer CreateShell(Transform parent, string name, float shellScale)
        {
            var shell = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            shell.name = name;
            shell.transform.SetParent(parent, false);
            shell.transform.localScale = new Vector3(0.78f, 1.28f, 0.78f) * shellScale;
            var collider = shell.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var renderer = shell.GetComponent<Renderer>();
            renderer.sharedMaterial = null;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        static TextMesh CreateText(
            Transform parent,
            string name,
            string text,
            Vector3 position,
            float characterSize,
            int fontSize,
            Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position;
            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = anchor;
            textMesh.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center;
            textMesh.characterSize = characterSize;
            textMesh.fontSize = fontSize;
            textMesh.color = color;
            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor("_Color", Color.white);
            textObject.GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
            return textMesh;
        }

        static void CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0.0f, 0.15f, -14.0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.006f, 0.008f, 0.018f, 1.0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50.0f;
        }

        static void CreateLight(Transform parent)
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localRotation = Quaternion.Euler(38.0f, -32.0f, 0.0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.92f, 0.95f, 1.0f, 1.0f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
        }

        static void CaptureView(Camera camera, string output, int width, int height)
        {
            var originalTarget = camera.targetTexture;
            var originalActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 1,
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
                Debug.Log("[TransformationBankDemoBuilder] 書き出しました: " + output);
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
