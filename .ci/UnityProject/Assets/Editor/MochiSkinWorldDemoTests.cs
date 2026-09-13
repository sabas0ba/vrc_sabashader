using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaShader.CI
{
    public class MochiSkinWorldDemoTests
    {
        const string ComponentName = "SabaShader.Samples.MochiSkinWorldDemoObject";
        const string MochiSkin = "_io_github_sabas0ba_mochiskin_";

        [Test]
        public void BlackComplianceMaskRemovesBothDisplacementAndShading()
        {
            var scene = EditorSceneManager.OpenScene(MochiSkinWorldDemoBuilder.ScenePath, OpenSceneMode.Single);
            var component = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .First(item => item != null && item.GetType().FullName == ComponentName);
            var material = component.GetComponent<MeshRenderer>().sharedMaterial;
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First();
            var target = new RenderTexture(256, 144, 24);
            var pixels = new Texture2D(256, 144, TextureFormat.RGBA32, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            Color32[] Capture()
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 256, 144), 0, 0);
                pixels.Apply();
                return pixels.GetPixels32();
            }
            float PixelError(Color32[] left, Color32[] right, out int maximum)
            {
                long total = 0;
                maximum = 0;
                for (var index = 0; index < left.Length; index++)
                {
                    var r = Mathf.Abs(left[index].r - right[index].r);
                    var g = Mathf.Abs(left[index].g - right[index].g);
                    var b = Mathf.Abs(left[index].b - right[index].b);
                    total += r + g + b;
                    maximum = Mathf.Max(maximum, Mathf.Max(r, Mathf.Max(g, b)));
                }
                return (float)total / (left.Length * 3);
            }
            void AssertNoDeformation(Color32[] actual, Color32[] expected)
            {
                var average = PixelError(actual, expected, out var maximum);
                Assert.That(maximum, Is.LessThanOrEqualTo(2), "最大RGB差 / 255");
                Assert.That(average, Is.LessThan(0.01f), "平均RGB差 / 255");
            }
            try
            {
                for (var index = 0; index < 4; index++) material.SetFloat(MochiSkin + "Pressure" + index, 0);
                var rest = Capture();
                for (var index = 0; index < 4; index++) material.SetFloat(MochiSkin + "Pressure" + index, 1);
                material.SetTexture(MochiSkin + "ComplianceMask", Texture2D.blackTexture);
                AssertNoDeformation(Capture(), rest);
                material.SetTexture(MochiSkin + "ComplianceMask", Texture2D.whiteTexture);
                Assert.That(PixelError(Capture(), rest, out _), Is.GreaterThan(0.02f), "白マスクで変形が有効になっていません。");
                material.SetFloat(MochiSkin + "Compliance", 0);
                AssertNoDeformation(Capture(), rest);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void BoneBakePreservesSourceAndUsesBindPoseRatherThanAnimatedTransforms()
        {
            var root = new GameObject("Compliance Test");
            var child = new GameObject("Joint");
            child.transform.SetParent(root.transform);
            child.transform.localPosition = Vector3.up;
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            var source = new Mesh();
            source.vertices = new[] { new Vector3(0.01f, 0.5f, 0), new Vector3(1, 0.5f, 0) };
            source.bindposes = new[] { Matrix4x4.identity, Matrix4x4.Translate(Vector3.down) };
            source.boneWeights = new[] {
                new BoneWeight { boneIndex0 = 0, weight0 = 0.4f, boneIndex1 = 1, weight1 = 0.6f },
                new BoneWeight { boneIndex0 = 1, weight0 = 1.0f } };
            renderer.sharedMesh = source;
            renderer.bones = new[] { root.transform, child.transform };
            var humans = new System.Collections.Generic.HashSet<Transform>(renderer.bones);
            var type = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("SabaShader.Editor.MochiSkinComplianceBaker"))
                .First(candidate => candidate != null);
            var method = type.GetMethod("BakeWithBones");
            Mesh first = null;
            Mesh second = null;
            try
            {
                first = (Mesh)method.Invoke(null, new object[] { renderer, humans });
                child.transform.localPosition = new Vector3(9, 8, 7);
                second = (Mesh)method.Invoke(null, new object[] { renderer, humans });
                Assert.That(source.uv4, Is.Empty);
                Assert.That(first.vertices, Is.EqualTo(source.vertices));
                Assert.That(first.boneWeights, Is.EqualTo(source.boneWeights));
                Assert.That(first.uv4[0].x, Is.EqualTo(0.15f).Within(0.001f));
                Assert.That(first.uv4[1].x, Is.EqualTo(1.0f));
                Assert.That(first.uv4, Is.EqualTo(second.uv4));
                renderer.sharedMesh = first;
                Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null, new object[] { renderer, humans }));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BoneComplianceIsRigidNearShaftAndJointAndSoftAway()
        {
            var type = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("SabaShader.Editor.MochiSkinComplianceBaker"))
                .First(candidate => candidate != null);
            var method = type.GetMethod("SegmentCompliance");
            float Evaluate(Vector3 position) => (float)method.Invoke(null, new object[] { position, Vector3.zero, Vector3.up });
            Assert.That(Evaluate(new Vector3(0.01f, 0.5f, 0)), Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(Evaluate(new Vector3(0.1f, 0, 0)), Is.LessThan(Evaluate(new Vector3(0.1f, 0.5f, 0))));
            Assert.That(Evaluate(new Vector3(1, 0.5f, 0)), Is.EqualTo(1));
            Assert.That(Evaluate(new Vector3(0.18f, 0.5f, 0)), Is.InRange(0.15f, 1.0f));
        }

        [TestCase(0.71f, false)]
        [TestCase(0.72f, false)]
        [TestCase(0.73f, true)]
        public void ProbeContactMatchesCurvedSurface(float proximity, bool penetrates)
        {
            var scene = EditorSceneManager.OpenScene(MochiSkinWorldDemoBuilder.ScenePath, OpenSceneMode.Single);
            var component = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .First(item => item != null && item.GetType().FullName == ComponentName);
            var method = component.GetType().GetMethod("ApplyPressures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(component, new object[] { Vector4.one * proximity });
            var material = component.GetComponent<MeshRenderer>().sharedMaterial;
            for (var index = 0; index < 4; index++)
            {
                var serialized = new SerializedObject(component);
                var probe = (Transform)serialized.FindProperty("probe" + index).objectReferenceValue;
                var separation = float.PositiveInfinity;
                Vector3 nearest = default;
                foreach (var vertex in probe.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    var local = probe.localPosition + probe.localRotation * Vector3.Scale(vertex, probe.localScale);
                    var surfaceZ = -0.12f + 0.055f * (Mathf.Pow(local.x / 0.825f, 2) + Mathf.Pow(local.y / 0.625f, 2));
                    var gap = surfaceZ - local.z;
                    if (gap < separation) { separation = gap; nearest = local; }
                }
                Assert.That(penetrates ? separation < 0 : separation >= -1.0e-6f, Is.True, probe.name);
                var point = material.GetVector(MochiSkin + "Point" + index);
                Assert.That(point.x, Is.EqualTo(nearest.x / 1.65f + 0.5f).Within(1.0e-5f));
                Assert.That(point.y, Is.EqualTo(nearest.y / 1.25f + 0.5f).Within(1.0e-5f));
            }
        }

        [Test]
        public void SampleSceneImportsDryAndGlossyNonToonSurfaces()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MochiSkinWorldDemoBuilder.ScenePath);
            Assert.That(sceneAsset, Is.Not.Null, "Mochi Skin World Demo sample がAssetsに配置されていません。");

            var scene = EditorSceneManager.OpenScene(MochiSkinWorldDemoBuilder.ScenePath, OpenSceneMode.Single);
            var objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null && component.GetType().FullName == ComponentName)
                .OrderBy(component => component.name)
                .ToArray();

            Assert.That(objects, Has.Length.EqualTo(2));
            Assert.That(objects.Select(component => component.name), Is.EquivalentTo(new[]
            {
                "Dry Skin Surface",
                "Glossy Skin Surface",
            }));
            foreach (var component in objects)
            {
                var renderer = component.GetComponent<MeshRenderer>();
                var mesh = component.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(renderer.sharedMaterial, Is.Not.Null, component.name);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("NonToon"), component.name);
                Assert.That(renderer.sharedMaterial.HasProperty(MochiSkin + "Pressure0"), Is.True, component.name);
                Assert.That(renderer.sharedMaterial.HasProperty(MochiSkin + "ContactThreshold"), Is.True, component.name);
                Assert.That(renderer.sharedMaterial.HasProperty(MochiSkin + "Shape0"), Is.True, component.name);
                Assert.That(mesh, Is.Not.Null, component.name);
                Assert.That(mesh.vertexCount, Is.GreaterThan(3900), component.name);
            }
        }

        [Test]
        public void NonToonCompilesWithMochiSkinModule()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(
                "Packages/jp.lilxyzw.nontoon/Shaders/NonToon.scshader");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True, "NonToonが現在のgraphics deviceでunsupportedです。");
            var errors = ShaderUtil.GetShaderMessages(shader)
                .Where(message => message.severity == ShaderCompilerMessageSeverity.Error)
                .Select(message => $"{message.platform}: {message.message}")
                .ToArray();

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void SurfacesUseDistinctNonToonSkinFinishesAndContactThreshold()
        {
            var scene = EditorSceneManager.OpenScene(MochiSkinWorldDemoBuilder.ScenePath, OpenSceneMode.Single);
            var objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null && component.GetType().FullName == ComponentName)
                .ToDictionary(component => component.name);

            var dryMaterial = objects["Dry Skin Surface"].GetComponent<MeshRenderer>().sharedMaterial;
            var glossyMaterial = objects["Glossy Skin Surface"].GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(dryMaterial.GetFloat("_Roughness"), Is.EqualTo(0.78f).Within(0.001f));
            Assert.That(glossyMaterial.GetFloat("_Roughness"), Is.EqualTo(0.13f).Within(0.001f));
            Assert.That(dryMaterial.GetTexture("_BaseTexture"), Is.Not.Null);
            Assert.That(glossyMaterial.GetTexture("_BaseTexture"), Is.Not.Null);
            foreach (var material in new[] { dryMaterial, glossyMaterial })
            {
                Assert.That(material.HasProperty("_jp_lilxyzw_nontoon_specular_SpecularColor"), Is.True);
                Assert.That(material.GetColor("_jp_lilxyzw_nontoon_specular_SpecularColor").r, Is.GreaterThan(0.4f));
                Assert.That(material.GetInteger("_jp_lilxyzw_nontoon_shade_ShadeGradientIndex"), Is.Zero);
                Assert.That(material.GetTexture("_SharedGradients"), Is.Not.Null);
            }

            foreach (var component in objects.Values)
            {
                var serialized = new SerializedObject(component);
                Assert.That(serialized.FindProperty("animateInPlayMode").boolValue, Is.True);
                Assert.That(serialized.FindProperty("contactThreshold").floatValue, Is.EqualTo(0.72f).Within(0.001f));
                Assert.That(serialized.FindProperty("edgeSoftness").floatValue, Is.GreaterThan(0.7f));
                Assert.That(serialized.FindProperty("contourIrregularity").floatValue, Is.GreaterThan(0.0f));
            }
        }

        [Test]
        public void EachSurfaceHasRotatedSphereCylinderPlateAndCapsuleProbes()
        {
            var scene = EditorSceneManager.OpenScene(MochiSkinWorldDemoBuilder.ScenePath, OpenSceneMode.Single);
            var surfaces = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null && component.GetType().FullName == ComponentName)
                .ToArray();
            var expectedNames = new[] { "Sphere Probe", "Cylinder Probe", "Plate Probe", "Capsule Probe" };

            foreach (var surface in surfaces)
            {
                var probes = Enumerable.Range(0, surface.transform.childCount)
                    .Select(index => surface.transform.GetChild(index))
                    .Where(child => expectedNames.Contains(child.name))
                    .ToArray();
                Assert.That(probes.Select(probe => probe.name), Is.EquivalentTo(expectedNames), surface.name);
                Assert.That(
                    probes.Where(probe => probe.name != "Sphere Probe")
                        .All(probe => probe.localRotation != Quaternion.identity),
                    Is.True,
                    surface.name);
            }
        }

        [Test]
        public void SampleSceneHasNoMissingScripts()
        {
            var scene = EditorSceneManager.OpenScene(MochiSkinWorldDemoBuilder.ScenePath, OpenSceneMode.Single);
            var missing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));

            Assert.That(missing, Is.Zero);
        }
    }
}
