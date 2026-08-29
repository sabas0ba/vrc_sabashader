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
    /// <summary>Transformation Bankの12 Style、2 Role、Particle補助演出を表示するUPM sampleを生成する。</summary>
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
        public static string ParticleMaterialPath => SampleDirectory + "/TransformationBankParticles.mat";
        public static string ParticlePrefabPath => SampleDirectory + "/TransformationBankParticlePair.prefab";

        static readonly string[] StyleNames =
        {
            "Arcane", "Cyber", "Astral", "Gaia", "Umbra",
            "Flame", "Shatter", "Glitch", "Melt",
            "Cosmic Rift", "Magical Sparkle", "Mana Mist",
        };
        static readonly float[] TimelineProgress = { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };
        static int textSortingOrder;
        static Mesh particleQuad;
        static Material particleMaterial;
        static GameObject particlePrefab;

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
            EnsureDirectory(SampleDirectory);
            particleQuad = null;
            particleMaterial = CreateParticleMaterial();
            particlePrefab = CreateParticlePrefab();
            PopulateScene(componentType);
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

            RefreshTextMeshes();
            Directory.CreateDirectory(outputDirectory);
            var particleRenderers = UnityEngine.Object.FindObjectsOfType<ParticleSystemRenderer>();
            var particleRendererStates = particleRenderers.Select(renderer => renderer.enabled).ToArray();
            try
            {
                // Unity 2022.3のbatch Camera.RenderはParticle billboard描画でnative crashするため、
                // golden imageはsurface shaderの比較に限定する。通常のScene/Play Modeでは無効化しない。
                foreach (var renderer in particleRenderers)
                {
                    renderer.enabled = false;
                }

                CaptureView(camera, Path.Combine(outputDirectory, "transformation_bank_demo.png"), 2560, 1440);
            }
            finally
            {
                for (var index = 0; index < particleRenderers.Length; index++)
                {
                    particleRenderers[index].enabled = particleRendererStates[index];
                }
            }
        }

        static void RefreshTextMeshes()
        {
            foreach (var textMesh in UnityEngine.Object.FindObjectsOfType<TextMesh>())
            {
                var content = textMesh.text;
                textMesh.font.RequestCharactersInTexture(content, textMesh.fontSize, textMesh.fontStyle);
                textMesh.text = string.Empty;
                textMesh.text = content;
            }
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
            textSortingOrder = 0;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.22f, 0.3f, 1.0f);

            var root = new GameObject("Transformation Bank Demo");
            CreateCamera(root.transform);
            CreateLight(root.transform);
            CreateText(root.transform, "Title", "SabaShader / Transformation Bank", new Vector3(0.0f, 6.55f, -0.8f), 0.14f, 58, Color.white);
            CreateText(root.transform, "Style Row", "12 VFX STYLES + PARTICLES / PLAY MODE", new Vector3(-9.3f, 5.7f, -0.8f), 0.06f, 44, new Color(0.46f, 0.76f, 1.0f, 1.0f), TextAnchor.MiddleLeft);
            CreateText(root.transform, "Timeline Row", "OLD CAPSULE  >  CROSS TRANSITION  >  NEW CYLINDER", new Vector3(-9.3f, -3.0f, -0.8f), 0.058f, 44, new Color(1.0f, 0.66f, 0.28f, 1.0f), TextAnchor.MiddleLeft);

            for (var index = 0; index < StyleNames.Length; index++)
            {
                var row = index / 4;
                var column = index % 4;
                var x = (column - 1.5f) * 4.0f;
                var y = 4.25f - row * 2.55f;
                CreateStation(
                    root.transform,
                    componentType,
                    $"Style {index:00} / {StyleNames[index]}",
                    new Vector3(x, y, 0.0f),
                    index,
                    0.5f,
                    true);
            }

            const float timelineSpacing = 3.0f;
            for (var index = 0; index < TimelineProgress.Length; index++)
            {
                var x = (index - 2) * timelineSpacing;
                CreateStation(
                    root.transform,
                    componentType,
                    $"Timeline {index:00} / {TimelineProgress[index]:0.00}",
                    new Vector3(x, -4.75f, 0.0f),
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

            var outgoing = CreateShell(station.transform, "Outgoing / Old Outfit", PrimitiveType.Capsule, new Vector3(0.9f, 0.78f, 0.9f));
            var incoming = CreateShell(station.transform, "Incoming / New Outfit", PrimitiveType.Cylinder, new Vector3(0.95f, 0.84f, 0.95f));
            CreateParticlePair(station.transform, style, out var primaryParticles, out var accentParticles);
            var label = CreateText(
                station.transform,
                "Progress Label",
                string.Empty,
                new Vector3(0.0f, -1.25f, -0.7f),
                0.047f,
                44,
                new Color(0.9f, 0.93f, 1.0f, 1.0f));

            var component = station.AddComponent(componentType);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("style").enumValueIndex = style;
            serialized.FindProperty("progress").floatValue = progress;
            serialized.FindProperty("animateInPlayMode").boolValue = animate;
            serialized.FindProperty("animationSpeed").floatValue = 0.2f;
            serialized.FindProperty("effectIntensity").floatValue = 1.6f;
            serialized.FindProperty("particleIntensity").floatValue = 1.4f;
            serialized.FindProperty("particleSize").floatValue = 1.0f;
            serialized.FindProperty("outgoingRenderer").objectReferenceValue = outgoing;
            serialized.FindProperty("incomingRenderer").objectReferenceValue = incoming;
            serialized.FindProperty("primaryParticles").objectReferenceValue = primaryParticles;
            serialized.FindProperty("accentParticles").objectReferenceValue = accentParticles;
            serialized.FindProperty("progressLabel").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            station.SetActive(true);
            componentType.GetMethod("Apply", BindingFlags.Instance | BindingFlags.Public)?.Invoke(component, null);
        }

        static Renderer CreateShell(Transform parent, string name, PrimitiveType primitive, Vector3 scale)
        {
            var shell = GameObject.CreatePrimitive(primitive);
            shell.name = name;
            shell.transform.SetParent(parent, false);
            shell.transform.localScale = scale;
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

        static void CreateParticlePair(
            Transform parent,
            int style,
            out ParticleSystem primary,
            out ParticleSystem accent)
        {
            var pair = (GameObject)PrefabUtility.InstantiatePrefab(particlePrefab, parent);
            pair.name = "Particle Effect Pair";
            pair.transform.localPosition = Vector3.zero;
            primary = pair.transform.Find("Primary Effect Particles").GetComponent<ParticleSystem>();
            accent = pair.transform.Find("Accent Particles").GetComponent<ParticleSystem>();

            var depth = style == 9 ? 0.45f : -0.25f;
            primary.transform.localPosition = new Vector3(0.0f, 0.0f, depth);
            accent.transform.localPosition = new Vector3(0.0f, 0.0f, depth);
            primary.GetComponent<ParticleSystemRenderer>().sortingOrder = 1000 + style * 2;
            accent.GetComponent<ParticleSystemRenderer>().sortingOrder = 1001 + style * 2;
        }

        static ParticleSystem CreateParticleSystem(Transform parent, string name)
        {
            var particleObject = new GameObject(name);
            particleObject.transform.SetParent(parent, false);
            var particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = particleMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.mesh = ParticleQuad();
            return particles;
        }

        static GameObject CreateParticlePrefab()
        {
            var root = new GameObject("Transformation Bank Particle Pair");
            CreateParticleSystem(root.transform, "Primary Effect Particles");
            CreateParticleSystem(root.transform, "Accent Particles");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ParticlePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null)
            {
                throw new InvalidOperationException("Particle effect prefab を保存できませんでした。");
            }

            return prefab;
        }

        static Material CreateParticleMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException("Particles/Standard Unlit を読み込めませんでした。");
            }

            material = new Material(shader)
            {
                name = "Transformation Bank Particles",
            };
            AssetDatabase.CreateAsset(material, ParticleMaterialPath);
            return material;
        }

        static Mesh ParticleQuad()
        {
            if (particleQuad != null)
            {
                return particleQuad;
            }

            var temporary = GameObject.CreatePrimitive(PrimitiveType.Quad);
            particleQuad = temporary.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temporary);
            return particleQuad;
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
            var renderer = textObject.GetComponent<MeshRenderer>();
            renderer.SetPropertyBlock(propertyBlock);
            renderer.sortingOrder = 30000 - textSortingOrder++;
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
            camera.orthographicSize = 7.4f;
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
