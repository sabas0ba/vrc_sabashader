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
