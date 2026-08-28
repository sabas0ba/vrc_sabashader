using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaShader.CI
{
    public class AdvancedShaderDemoTests
    {
        static readonly string[] RequiredModuleProperties =
        {
            "_io_github_sabas0ba_decal_Amount",
            "_io_github_sabas0ba_surfacedetail_Amount",
            "_io_github_sabas0ba_spatialinterior_Amount",
            "_io_github_sabas0ba_spatialinterior_Preset",
            "_io_github_sabas0ba_transition_Progress",
        };

        [Test]
        public void SampleSceneImportsWithAllAdvancedFeatures()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(AdvancedShaderDemoBuilder.ScenePath);
            Assert.That(sceneAsset, Is.Not.Null, "Advanced Shader Suite Demo sample が Assets に配置されていません。");

            var scene = EditorSceneManager.OpenScene(AdvancedShaderDemoBuilder.ScenePath, OpenSceneMode.Single);
            var objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null)
                .Where(component => component.GetType().FullName == "SabaShader.Samples.AdvancedShaderDemoObject")
                .ToArray();

            Assert.That(objects, Has.Length.EqualTo(11));
            foreach (var component in objects)
            {
                var renderer = component.GetComponent<MeshRenderer>();
                Assert.That(renderer.sharedMaterial, Is.Not.Null, component.name);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("SabaShader/Illust2D"), component.name);
            }
        }

        [Test]
        public void SampleShaderContainsAllRequiredModuleProperties()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.Illust2DPath);
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                foreach (var property in RequiredModuleProperties)
                {
                    Assert.That(material.HasProperty(property), Is.True, property);
                }
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SampleSceneHasNoMissingScripts()
        {
            var scene = EditorSceneManager.OpenScene(AdvancedShaderDemoBuilder.ScenePath, OpenSceneMode.Single);
            var missing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));

            Assert.That(missing, Is.Zero);
        }
    }
}
