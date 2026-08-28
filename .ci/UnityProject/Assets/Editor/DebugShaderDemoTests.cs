using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaShader.CI
{
    public class DebugShaderDemoTests
    {
        [Test]
        public void SampleSceneImportsWithAllDebugModes()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(DebugShaderDemoBuilder.ScenePath);
            Assert.That(sceneAsset, Is.Not.Null, "Debug Shader Demo sample が Assets に配置されていません。");

            var scene = EditorSceneManager.OpenScene(DebugShaderDemoBuilder.ScenePath, OpenSceneMode.Single);
            var objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null)
                .Where(component => component.GetType().FullName == "SabaShader.Samples.DebugShaderDemoObject")
                .ToArray();

            Assert.That(objects, Has.Length.EqualTo(18));
            foreach (var component in objects)
            {
                var renderer = component.GetComponent<MeshRenderer>();
                Assert.That(renderer.sharedMaterial, Is.Not.Null, component.name);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("SabaShader/Debug"), component.name);
                Assert.That(component.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null, component.name);
            }
        }

        [Test]
        public void SampleSceneHasNoMissingScripts()
        {
            var scene = EditorSceneManager.OpenScene(DebugShaderDemoBuilder.ScenePath, OpenSceneMode.Single);
            var missing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));

            Assert.That(missing, Is.Zero);
        }
    }
}
