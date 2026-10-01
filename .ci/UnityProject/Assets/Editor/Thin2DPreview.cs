using System;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace SabaShader.CI
{
    public static class Thin2DPreview
    {
        public static void RunBatch()
        {
            try
            {
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "_test_artifacts"));
                Directory.CreateDirectory(output);

                var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                for (var y = 0; y < texture.height; y++)
                {
                    for (var x = 0; x < texture.width; x++)
                    {
                        var dx = (x + 0.5f - 64f) / 64f;
                        var dy = (y + 0.5f - 64f) / 64f;
                        var alpha = Mathf.Clamp01((0.75f - Mathf.Sqrt(dx * dx + dy * dy)) * 64f);
                        texture.SetPixel(x, y, new Color(0.9f, 0.2f, 0.1f, alpha));
                    }
                }
                texture.Apply();

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.transform.localScale = new Vector3(2f, 2f, 1f);

                var cameraObject = new GameObject("Thin2D Preview Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, 3f);
                camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = 1.25f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                camera.cullingMask = 1 << quad.layer;

                var lightObject = new GameObject("Thin2D Preview Light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                light.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                foreach (var name in new[] { "Paper2D", "Acrylic2D" })
                {
                    AssetDatabase.ImportAsset(
                        ShaderCompileChecker.PackagePath + "/Shaders/" + name + "/" + name + ".scshader",
                        ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    var shader = Shader.Find("SabaShader/" + name);
                    if (shader == null) throw new InvalidOperationException("Shader not found: " + name);

                    var material = new Material(shader);
                    material.SetTexture("_BaseTexture", texture);
                    material.SetFloat("_EdgeWidth", 0.03f);
                    material.SetFloat("_EdgeIntensity", 2f);
                    if (name == "Paper2D") material.SetFloat("_PaperGrain", 0f);
                    quad.GetComponent<Renderer>().sharedMaterial = material;

                    for (var pass = 0; pass < material.passCount; pass++) material.SetPass(pass);
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    {
                        if (message.severity == ShaderCompilerMessageSeverity.Error)
                            throw new InvalidOperationException(name + ": " + message.message);
                    }

                    var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
                    camera.targetTexture = target;
                    var oldActive = RenderTexture.active;
                    camera.Render();
                    RenderTexture.active = target;
                    var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                    pixels.ReadPixels(new Rect(0f, 0f, 256f, 256f), 0, 0);
                    pixels.Apply();

                    var path = Path.Combine(output, name + "-unity-preview.png");
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    var center = pixels.GetPixel(128, 128);
                    var edge = pixels.GetPixel(200, 128);
                    var background = pixels.GetPixel(20, 20);
                    Debug.Log($"[Thin2DPreview] {name}: center={center}, edge={edge}, background={background}, file={path}");

                    if (center.r > 0.9f && center.b > 0.9f && center.g < 0.1f)
                        throw new InvalidOperationException(name + " rendered as error magenta");
                    if (Vector3.Distance(new Vector3(center.r, center.g, center.b), new Vector3(background.r, background.g, background.b)) < 0.03f)
                        throw new InvalidOperationException(name + " did not render a visible surface");
                    if (Vector3.Distance(new Vector3(edge.r, edge.g, edge.b), new Vector3(center.r, center.g, center.b)) < 0.03f)
                        throw new InvalidOperationException(name + " did not render a distinct contour");

                    RenderTexture.active = oldActive;
                    camera.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(pixels);
                    UnityEngine.Object.DestroyImmediate(target);
                    UnityEngine.Object.DestroyImmediate(material);
                }

                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(quad);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Thin2DPreview] " + exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
