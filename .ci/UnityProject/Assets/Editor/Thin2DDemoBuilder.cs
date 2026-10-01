using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaShader.CI
{
    /// <summary>Builds the distributable Paper2D and Acrylic2D sample in Unity.</summary>
    public static class Thin2DDemoBuilder
    {
        const string SampleName = "Thin2D Demo";
        const string SceneName = "Thin2DDemo.unity";

        static string SampleDirectory
        {
            get
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                    ShaderCompileChecker.PackagePath + "/package.json");
                if (package == null) throw new InvalidOperationException("SabaShader package was not found.");
                return $"Assets/Samples/{package.displayName}/{package.version}/{SampleName}";
            }
        }

        public static string ScenePath => SampleDirectory + "/" + SceneName;

        [MenuItem("SabaShader/Demo/Build Thin2D Demo")]
        public static void BuildAndOpen()
        {
            Build();
            OpenDemo();
        }

        [MenuItem("SabaShader/Demo/Open Thin2D Demo")]
        public static void OpenDemo()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Import or build the Thin2D sample first.", ScenePath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[Thin2DDemoBuilder] Opened " + EditorSceneManager.GetActiveScene().path);
        }

        public static void BuildBatch()
        {
            try
            {
                Build();
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
            EnsureFolder(SampleDirectory);
            EnsureFolder(SampleDirectory + "/Materials");
            EnsureFolder(SampleDirectory + "/Meshes");
            var sculpture = SaveSculptureMesh();
            var paperShader = LoadShader("Paper2D");
            var acrylicShader = LoadShader("Acrylic2D");
            var reference = SaveMaterial("Source 3D.mat", Shader.Find("Standard"), null);
            reference.SetColor("_Color", new Color(0.9f, 0.55f, 0.32f));
            reference.SetFloat("_Glossiness", 0.45f);
            EditorUtility.SetDirty(reference);

            var paper = SaveMaterial("Paper 3D.mat", paperShader, null);
            paper.SetColor("_BaseColor", new Color(0.97f, 0.77f, 0.51f));
            paper.SetFloat("_Flatness", 1f);
            paper.SetFloat("_DepthScale", 0.18f);
            paper.SetFloat("_PaperGrain", 0.07f);
            paper.SetFloat("_OutlineWidth", 0.035f);
            paper.SetColor("_OutlineColor", new Color(0.27f, 0.12f, 0.08f));
            paper.SetFloat("_WhiteBorderWidth", 0.075f);
            paper.SetColor("_WhiteBorderColor", Color.white);
            paper.SetFloat("_SurfaceShadowEnabled", 1f);
            EditorUtility.SetDirty(paper);

            var clear = SaveMaterial("Acrylic Cyan.mat", acrylicShader, null);
            SetAcrylic(clear, 0.43f, new Color(0.20f, 0.90f, 1f), new Color(0.43f, 0.88f, 1f));
            clear.SetColor("_BaseColor", new Color(0.65f, 0.91f, 1f, 0.68f));
            var rose = SaveMaterial("Acrylic Rose.mat", acrylicShader, null);
            SetAcrylic(rose, 0.72f, new Color(1f, 0.38f, 0.70f), new Color(1f, 0.52f, 0.78f));
            rose.SetColor("_BaseColor", new Color(1f, 0.68f, 0.85f, 0.8f));
            rose.SetFloat("_SurfaceShadowEnabled", 0f);
            EditorUtility.SetDirty(rose);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.70f, 0.73f, 0.79f);
            var root = new GameObject("Thin2D Demo").transform;
            CreateCamera(root);
            CreateLight(root);
            CreateBackground(root);
            AddText(root, "Title", "3D TO 2D", new Vector3(0f, 3.10f, -0.6f), 0.08f, Color.white);
            AddText(root, "Subtitle", "The same 3D sculpture with source, paper and acrylic materials", new Vector3(0f, 2.79f, -0.6f), 0.03f, new Color(0.57f, 0.68f, 0.76f));

            AddExample(root, "Source / 3D", "Original lit mesh", -2.12f, 1.30f, reference, sculpture);
            AddExample(root, "Paper / flat", "Flat color and white margin", 2.12f, 1.30f, paper, sculpture);
            AddExample(root, "Acrylic / cyan", "Printed base, single clear coat", -2.12f, -1.60f, clear, sculpture);
            AddExample(root, "Acrylic / rose", "Surface shadows off", 2.12f, -1.60f, rose, sculpture);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save " + ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Thin2DDemoBuilder] Built " + ScenePath);
        }

        static Shader LoadShader(string name)
        {
            var path = ShaderCompileChecker.PackagePath + "/Shaders/" + name + "/" + name + ".scshader";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = Shader.Find("SabaShader/" + name);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + name);
            return shader;
        }

        static Mesh SaveSculptureMesh()
        {
            var path = SampleDirectory + "/Meshes/Sculpture.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var source = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var sphere = source.GetComponent<MeshFilter>().sharedMesh;
                var parts = new[]
                {
                    SculptPart(sphere, new Vector3(0f, -0.31f, 0f), new Vector3(1.1f, 1.35f, 0.78f), 0f),
                    SculptPart(sphere, new Vector3(0f, 0.48f, -0.10f), new Vector3(0.98f, 0.84f, 0.74f), 0f),
                    SculptPart(sphere, new Vector3(-0.36f, 1.00f, 0.09f), new Vector3(0.24f, 0.53f, 0.29f), 18f),
                    SculptPart(sphere, new Vector3(0.36f, 1.00f, 0.09f), new Vector3(0.24f, 0.53f, 0.29f), -18f),
                    SculptPart(sphere, new Vector3(0.51f, -0.56f, 0.40f), new Vector3(0.38f, 0.37f, 0.38f), 0f),
                };
                var mesh = new Mesh { name = "Sculpture" };
                mesh.CombineMeshes(parts, true, true);
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        static CombineInstance SculptPart(Mesh mesh, Vector3 position, Vector3 scale, float rotation)
        {
            return new CombineInstance
            {
                mesh = mesh,
                transform = Matrix4x4.TRS(position, Quaternion.Euler(0f, 0f, rotation), scale)
            };
        }

        static Material SaveMaterial(string fileName, Shader shader, Texture texture)
        {
            var path = SampleDirectory + "/Materials/" + fileName;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseTexture", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void SetAcrylic(Material material, float opacity, Color edgeColor, Color transmissionColor)
        {
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_Thickness", 0.9f);
            material.SetFloat("_RefractionStrength", 0.12f);
            material.SetFloat("_RefractionScale", 1.2f);
            material.SetFloat("_InternalLight", 1.15f);
            material.SetFloat("_EdgeWidth", 0.027f);
            material.SetFloat("_RimWidth", 0f);
            material.SetFloat("_Flatness", 1f);
            material.SetFloat("_DepthScale", 0.18f);
            material.SetFloat("_EdgeIntensity", 2.4f);
            material.SetColor("_EdgeColor", edgeColor);
            material.SetColor("_TransmissionColor", transmissionColor);
            material.SetFloat("_TransmissionStrength", 0.38f);
            material.SetFloat("_SpecularPower", 48f);
            material.SetFloat("_SpecularIntensity", 1.6f);
            material.SetFloat("_WhiteBorderWidth", 0.075f);
            material.SetColor("_WhiteBorderColor", Color.white);
            material.SetFloat("_SurfaceShadowEnabled", 1f);
            EditorUtility.SetDirty(material);
        }

        static void CreateCamera(Transform parent)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, -12f);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.038f, 0.06f);
        }

        static void CreateLight(Transform parent)
        {
            var go = new GameObject("Directional Light");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(25f, -25f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.87f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
        }

        static void CreateBackground(Transform parent)
        {
            CreateFlat(parent, "Display board", Vector3.zero + Vector3.forward * 1.2f,
                new Vector3(9.8f, 6.9f, 1f), new Color(0.055f, 0.080f, 0.12f));
            CreateFlat(parent, "Row divider", new Vector3(0f, -0.14f, 0.95f),
                new Vector3(8.7f, 0.012f, 1f), new Color(0.18f, 0.26f, 0.32f));
        }

        static void AddExample(Transform root, string title, string description, float x, float y, Material material, Mesh sculpture)
        {
            var group = new GameObject(title).transform;
            group.SetParent(root, false);
            group.localPosition = new Vector3(x, y, 0f);
            CreateFlat(group, "Panel", new Vector3(0f, 0f, 0.8f), new Vector3(3.72f, 2.72f, 1f),
                new Color(0.10f, 0.15f, 0.20f));
            for (var index = -2; index <= 2; index++)
                CreateFlat(group, "Background guide " + index, new Vector3(index * 0.26f, -0.06f, 0.65f),
                    new Vector3(0.13f, 1.65f, 1f), index % 2 == 0
                        ? new Color(0.35f, 0.53f, 0.59f)
                        : new Color(0.28f, 0.40f, 0.48f));

            CreateSculpture(group, material, sculpture);

            AddText(group, "Label", title.ToUpperInvariant(), new Vector3(0f, 1.14f, -0.35f),
                0.055f, new Color(0.95f, 0.95f, 0.91f));
            AddText(group, "Description", description, new Vector3(0f, -1.14f, -0.35f),
                0.03f, new Color(0.67f, 0.77f, 0.82f));
        }

        static void CreateSculpture(Transform parent, Material material, Mesh sculpture)
        {
            var model = new GameObject("3D sculpture - select a mesh to edit its material").transform;
            model.SetParent(parent, false);
            model.localPosition = new Vector3(0f, -0.07f, 0f);
            model.localRotation = Quaternion.Euler(0f, -25f, 0f);
            model.localScale = Vector3.one * 0.72f;
            model.gameObject.AddComponent<MeshFilter>().sharedMesh = sculpture;
            model.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void CreateFlat(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            var shader = Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("Unity Unlit/Color shader not found.");
            var key = ColorUtility.ToHtmlStringRGB(color);
            var path = SampleDirectory + "/Materials/Display " + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
            EditorUtility.SetDirty(material);
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static void AddText(Transform parent, string name, string value, Vector3 position, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var label = go.AddComponent<TextMesh>();
            label.text = value;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = size;
            label.fontSize = 64;
            label.color = color;
        }

        static void Capture()
        {
            var camera = UnityEngine.Object.FindObjectOfType<Camera>();
            if (camera == null) throw new InvalidOperationException("Demo camera was not created.");
            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "_test_artifacts"));
            Directory.CreateDirectory(outputDirectory);
            var output = Path.Combine(outputDirectory, "Thin2D-demo.png");
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            var pixels = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(output, pixels.EncodeToPNG());
                Debug.Log("[Thin2DDemoBuilder] Captured " + output);

                foreach (var model in UnityEngine.Object.FindObjectsOfType<Transform>())
                {
                    if (model.name.StartsWith("3D sculpture", StringComparison.Ordinal))
                        model.localRotation = Quaternion.Euler(0f, 75f, 0f);
                }
                camera.Render();
                pixels.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
                pixels.Apply();
                var sideOutput = Path.Combine(outputDirectory, "Thin2D-demo-side.png");
                File.WriteAllBytes(sideOutput, pixels.EncodeToPNG());
                Debug.Log("[Thin2DDemoBuilder] Captured " + sideOutput);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
