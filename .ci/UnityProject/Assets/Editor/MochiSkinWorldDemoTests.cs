using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaShader.CI
{
    public class MochiSkinWorldDemoTests
    {
        const string ComponentName = "SabaShader.Samples.MochiSkinWorldDemoObject";
        const string Pressure0 = "_io_github_sabas0ba_mochiskin_Pressure0";

        [Test]
        public void SampleSceneImportsWithRestAndContactSurfaces()
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
            foreach (var component in objects)
            {
                var renderer = component.GetComponent<MeshRenderer>();
                var mesh = component.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(renderer.sharedMaterial, Is.Not.Null, component.name);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("SabaShader/Illust2D"), component.name);
                Assert.That(renderer.sharedMaterial.HasProperty(Pressure0), Is.True, component.name);
                Assert.That(mesh, Is.Not.Null, component.name);
                Assert.That(mesh.vertexCount, Is.GreaterThan(3000), component.name);
            }

            var contact = objects.Single(component => component.name == "Contact Driven Surface");
            var serialized = new SerializedObject(contact);
            Assert.That(serialized.FindProperty("animateInPlayMode").boolValue, Is.True);
            Assert.That(serialized.FindProperty("pressure0").floatValue, Is.GreaterThan(0.9f));
            for (var index = 0; index < 4; index++)
            {
                Assert.That(serialized.FindProperty("probe" + index).objectReferenceValue, Is.Not.Null);
            }
        }

        [Test]
        public void RestSurfaceKeepsAllPressuresAtZero()
        {
            var scene = EditorSceneManager.OpenScene(MochiSkinWorldDemoBuilder.ScenePath, OpenSceneMode.Single);
            var rest = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Single(component => component != null && component.name == "Rest Surface");
            var material = rest.GetComponent<MeshRenderer>().sharedMaterial;

            for (var index = 0; index < 4; index++)
            {
                Assert.That(
                    material.GetFloat("_io_github_sabas0ba_mochiskin_Pressure" + index),
                    Is.Zero.Within(0.0001f));
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
