using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaShader.CI
{
    /// <summary>Checks that hidden acrylic surfaces do not add another coat.</summary>
    public static class Thin2DAcrylicLayerCheck
    {
        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static void Run()
        {
            AssetDatabase.ImportAsset(ShaderCompileChecker.Acrylic2DPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var shader = Shader.Find("SabaShader/Acrylic2D");
            if (shader == null) throw new InvalidOperationException("Acrylic2D shader was not imported.");

            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(0.7f, 0.9f, 1f, 0.25f));
            material.SetFloat("_Opacity", 0.35f);
            material.SetFloat("_PaperGrain", 0f);
            material.SetFloat("_SurfaceShadowEnabled", 0f);
            material.SetFloat("_RimWidth", 0f);
            material.SetFloat("_WhiteBorderWidth", 0f);
            material.SetFloat("_EdgeIntensity", 0f);
            material.SetFloat("_SpecularIntensity", 0f);
            material.SetFloat("_TransmissionStrength", 0f);

            var cameraObject = new GameObject("Acrylic layer test camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -3f);
            camera.orthographic = true;
            camera.orthographicSize = 0.9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.2f, 0.3f);
            camera.cullingMask = 1 << 20;

            var sculpture = AssetDatabase.LoadAssetAtPath<Mesh>(
                "Assets/Samples/SabaShader/0.5.0/Thin2D Demo/Meshes/Sculpture.asset");
            if (sculpture == null) throw new InvalidOperationException("The acrylic layer test mesh was not built.");

            var front = CreateMeshObject("Acrylic layer front", sculpture);
            front.layer = 20;
            front.GetComponent<Renderer>().sharedMaterial = material;
            var back = CreateMeshObject("Acrylic layer back", sculpture);
            back.layer = 20;
            back.transform.position = new Vector3(0f, 0f, 0.25f);
            back.GetComponent<Renderer>().sharedMaterial = material;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.8f);
            back.SetActive(false);
            var single = SampleCenter(camera);
            back.SetActive(true);
            var overlapping = SampleCenter(camera);
            var difference = Vector3.Distance(
                new Vector3(single.r, single.g, single.b),
                new Vector3(overlapping.r, overlapping.g, overlapping.b));
            Debug.Log($"[Thin2DAcrylicLayerCheck] single={single}, overlapping={overlapping}, difference={difference:F5}");
            if (difference > 0.02f)
                throw new InvalidOperationException("Overlapping surfaces changed the acrylic color or opacity.");

            UnityEngine.Object.DestroyImmediate(front);
            UnityEngine.Object.DestroyImmediate(back);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(material);
        }

        static Color SampleCenter(Camera camera)
        {
            var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 64f, 64f), 0, 0);
                pixels.Apply();
                return pixels.GetPixel(32, 32);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static GameObject CreateMeshObject(string name, Mesh mesh)
        {
            var gameObject = new GameObject(name);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>();
            return gameObject;
        }
    }
}
