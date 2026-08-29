using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaShader.CI
{
    public class TransformationBankDemoTests
    {
        const string Bank = "_io_github_sabas0ba_transformationbank_";

        [Test]
        public void SampleSceneContainsFiveStylesAndFiveTimelineSnapshots()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TransformationBankDemoBuilder.ScenePath);
            Assert.That(sceneAsset, Is.Not.Null, "Transformation Bank Demo sample がAssetsに配置されていません。");

            var components = OpenComponents();
            Assert.That(components, Has.Length.EqualTo(10));
            var serialized = components.Select(component => new SerializedObject(component)).ToArray();
            Assert.That(
                serialized.Count(item => item.FindProperty("animateInPlayMode").boolValue),
                Is.EqualTo(5));
            Assert.That(
                serialized.Where(item => item.FindProperty("animateInPlayMode").boolValue)
                    .Select(item => item.FindProperty("style").enumValueIndex),
                Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }));
            var timeline = serialized
                .Where(item => !item.FindProperty("animateInPlayMode").boolValue)
                .Select(item => item.FindProperty("progress").floatValue)
                .OrderBy(value => value)
                .ToArray();
            Assert.That(timeline, Has.Length.EqualTo(5));
            for (var index = 0; index < timeline.Length; index++)
            {
                Assert.That(timeline[index], Is.EqualTo(index * 0.25f).Within(0.0001f));
            }
        }

        [Test]
        public void EveryStationHasThreeConfiguredRoleMaterials()
        {
            foreach (var component in OpenComponents())
            {
                var serialized = new SerializedObject(component);
                AssertRole(serialized, "outgoingRenderer", 1, component.name);
                AssertRole(serialized, "incomingRenderer", 0, component.name);
                AssertRole(serialized, "safetyCoverRenderer", 2, component.name);
            }
        }

        [Test]
        public void ManualProgressUpdateReusesGeneratedMaterials()
        {
            var component = OpenComponents().First(item => item.name.StartsWith("Style 00"));
            var serialized = new SerializedObject(component);
            var renderer = (Renderer)serialized.FindProperty("safetyCoverRenderer").objectReferenceValue;
            var material = renderer.sharedMaterial;
            serialized.FindProperty("animateInPlayMode").boolValue = false;
            serialized.FindProperty("progress").floatValue = 0.63f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            Assert.That(renderer.sharedMaterial.GetFloat(Bank + "Progress"), Is.EqualTo(0.63f).Within(0.0001f));
        }

        [Test]
        public void SampleSceneHasNoMissingScripts()
        {
            var scene = EditorSceneManager.OpenScene(TransformationBankDemoBuilder.ScenePath, OpenSceneMode.Single);
            var missing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
            Assert.That(missing, Is.Zero);
        }

        [Test]
        public void TextLabelsDisableDynamicBatchingAfterSceneReload()
        {
            var scene = EditorSceneManager.OpenScene(TransformationBankDemoBuilder.ScenePath, OpenSceneMode.Single);
            var textMeshes = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TextMesh>(true))
                .ToArray();

            Assert.That(textMeshes, Is.Not.Empty);
            Assert.That(
                textMeshes.All(textMesh => textMesh.GetComponent<MeshRenderer>().HasPropertyBlock()),
                Is.True);
        }

        static MonoBehaviour[] OpenComponents()
        {
            var scene = EditorSceneManager.OpenScene(TransformationBankDemoBuilder.ScenePath, OpenSceneMode.Single);
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null)
                .Where(component => component.GetType().FullName == "SabaShader.Samples.TransformationBankDemoController")
                .ToArray();
        }

        static void AssertRole(SerializedObject controller, string rendererProperty, int role, string station)
        {
            var renderer = (Renderer)controller.FindProperty(rendererProperty).objectReferenceValue;
            Assert.That(renderer, Is.Not.Null, station + " / " + rendererProperty);
            Assert.That(renderer.sharedMaterial, Is.Not.Null, station + " / " + rendererProperty);
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("SabaShader/Illust2D"));
            Assert.That(renderer.sharedMaterial.GetInteger(Bank + "Role"), Is.EqualTo(role));
            Assert.That(renderer.sharedMaterial.GetFloat(Bank + "Progress"),
                Is.EqualTo(controller.FindProperty("progress").floatValue).Within(0.0001f));
        }
    }
}
