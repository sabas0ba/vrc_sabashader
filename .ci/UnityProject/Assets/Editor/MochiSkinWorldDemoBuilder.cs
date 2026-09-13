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
        const string NonToonPath = "Packages/jp.lilxyzw.nontoon/Shaders/NonToon.scshader";

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
            var shader = ShaderCompileChecker.ImportAndLoad(NonToonPath);
            if (shader == null)
            {
                throw new InvalidOperationException("NonToon をインポートできませんでした。");
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
                "MOCHI SKIN / NONTOON",
                new Vector3(0.0f, 1.75f, -0.45f),
                0.075f,
                58,
                Color.white);

            CreatePatch(root.transform, componentType, "Dry Skin Surface", new Vector3(-1.10f, 0.28f, 0.0f), 0);
            CreatePatch(root.transform, componentType, "Glossy Skin Surface", new Vector3(1.10f, 0.28f, 0.0f), 1);
            CreateText(
                root.transform,
                "Dry Skin Label",
                "DRY SKIN\nROUGHNESS 0.78",
                new Vector3(-1.10f, -0.65f, -0.45f),
                0.036f,
                44,
                new Color(0.94f, 0.78f, 0.70f, 1.0f));
            CreateText(
                root.transform,
                "Glossy Skin Label",
                "GLOSSY SKIN\nROUGHNESS 0.13",
                new Vector3(1.10f, -0.65f, -0.45f),
                0.036f,
                44,
                new Color(1.0f, 0.70f, 0.65f, 1.0f));
            CreateText(
                root.transform,
                "Probe Shapes",
                "SPHERE  /  CYLINDER  /  PLATE  /  CAPSULE    -    ROTATED CONTACT FOOTPRINTS",
                new Vector3(0.0f, -0.98f, -0.45f),
                0.030f,
                38,
                new Color(0.72f, 0.78f, 0.89f, 1.0f));
            CreateText(
                root.transform,
                "Instructions",
                "PLAY: approach -> touch -> penetration    /    DEFORMATION STARTS AT PHYSICAL CONTACT",
                new Vector3(0.0f, -1.26f, -0.45f),
                0.034f,
                38,
                new Color(0.68f, 0.72f, 0.82f, 1.0f));
        }

        static void CreatePatch(Transform parent, Type componentType, string name, Vector3 position, int skinFinish)
        {
            var patch = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            patch.transform.SetParent(parent, false);
            patch.transform.localPosition = position;
            patch.transform.localRotation = Quaternion.Euler(-8.0f, skinFinish == 0 ? -18.0f : 18.0f, 0.0f);

            var component = patch.AddComponent(componentType);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("skinFinish").enumValueIndex = skinFinish;
            serialized.FindProperty("pressure0").floatValue = skinFinish == 0 ? 0.96f : 0.90f;
            serialized.FindProperty("pressure1").floatValue = skinFinish == 0 ? 0.88f : 0.98f;
            serialized.FindProperty("pressure2").floatValue = skinFinish == 0 ? 0.93f : 0.86f;
            serialized.FindProperty("pressure3").floatValue = skinFinish == 0 ? 0.99f : 0.94f;
            serialized.FindProperty("animateInPlayMode").boolValue = true;

            for (var index = 0; index < 4; index++)
            {
                var probe = CreateProbe(componentType, patch.transform, index);
                serialized.FindProperty("probe" + index).objectReferenceValue = probe.transform;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            componentType.GetMethod("Apply", BindingFlags.Instance | BindingFlags.Public)?.Invoke(component, null);
        }

        static GameObject CreateProbe(Type componentType, Transform parent, int index)
        {
            var method = componentType.GetMethod(
                "CreateProbePreview",
                BindingFlags.Static | BindingFlags.Public);
            if (method == null)
            {
                throw new MissingMethodException(componentType.FullName, "CreateProbePreview");
            }

            return (GameObject)method.Invoke(null, new object[] { parent, index });
        }

        static void CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0.0f, 0.15f, -6.0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.20f;
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
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font.RequestCharactersInTexture(text, fontSize, FontStyle.Normal);
            textMesh.text = text;
            textMesh.font = font;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = characterSize;
            textMesh.fontSize = fontSize;
            textMesh.color = color;
            textObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
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
