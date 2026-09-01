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
        public void SampleSceneContainsTwelveStylesAndFiveTimelineSnapshots()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TransformationBankDemoBuilder.ScenePath);
            Assert.That(sceneAsset, Is.Not.Null, "Transformation Bank Demo sample がAssetsに配置されていません。");

            var components = OpenComponents();
            Assert.That(components, Has.Length.EqualTo(17));
            var serialized = components.Select(component => new SerializedObject(component)).ToArray();
            Assert.That(
                serialized.Count(item => item.FindProperty("animateInPlayMode").boolValue),
                Is.EqualTo(12));
            Assert.That(
                serialized.Where(item => item.FindProperty("animateInPlayMode").boolValue)
                    .Select(item => item.FindProperty("style").enumValueIndex),
                Is.EquivalentTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }));
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
        public void EveryStationHasTwoConfiguredRoleMaterialsAndParticles()
        {
            foreach (var component in OpenComponents())
            {
                var serialized = new SerializedObject(component);
                AssertRole(serialized, "outgoingRenderer", 1, component.name);
                AssertRole(serialized, "incomingRenderer", 0, component.name);
                Assert.That(
                    serialized.FindProperty("primaryParticles").objectReferenceValue,
                    Is.TypeOf<ParticleSystem>());
                Assert.That(
                    serialized.FindProperty("accentParticles").objectReferenceValue,
                    Is.TypeOf<ParticleSystem>());
            }
        }

        [Test]
        public void EveryStationUsesDistinctRoleShapes()
        {
            foreach (var component in OpenComponents())
            {
                var serialized = new SerializedObject(component);
                var outgoing = (Renderer)serialized.FindProperty("outgoingRenderer").objectReferenceValue;
                var incoming = (Renderer)serialized.FindProperty("incomingRenderer").objectReferenceValue;

                Assert.That(outgoing.GetComponent<MeshFilter>().sharedMesh.name, Does.Contain("Capsule"));
                Assert.That(incoming.GetComponent<MeshFilter>().sharedMesh.name, Does.Contain("Cylinder"));
            }
        }

        [Test]
        public void ShatterAndGlitchUseMeshParticles()
        {
            foreach (var component in OpenComponents().Where(item => item.name.Contains("Shatter") || item.name.Contains("Glitch")))
            {
                var serialized = new SerializedObject(component);
                foreach (var property in new[] { "primaryParticles", "accentParticles" })
                {
                    var particles = (ParticleSystem)serialized.FindProperty(property).objectReferenceValue;
                    var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
                    Assert.That(particleRenderer.sharedMaterial, Is.Not.Null);
                    Assert.That(particleRenderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Mesh));
                    Assert.That(particleRenderer.mesh, Is.Not.Null);
                }
            }
        }

        [Test]
        public void EveryStyleUsesTailoredParticleMeshes()
        {
            var components = OpenComponents().Where(item => item.name.StartsWith("Style ")).ToArray();
            var primaryMeshNames = components
                .Select(component => new SerializedObject(component))
                .Select(serialized => (ParticleSystem)serialized.FindProperty("primaryParticles").objectReferenceValue)
                .Select(particles => particles.GetComponent<ParticleSystemRenderer>())
                .Where(renderer => renderer.enabled)
                .Select(renderer =>
                {
                    Assert.That(renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Mesh));
                    Assert.That(renderer.mesh, Is.Not.Null);
                    Assert.That(renderer.mesh.name, Does.StartWith("Transformation Bank Particle / "));
                    Assert.That(renderer.mesh.name, Does.Not.Contain("Quad"));
                    Assert.That(AssetDatabase.Contains(renderer.mesh), Is.True);
                    Assert.That(
                        AssetDatabase.GetAssetPath(renderer.mesh),
                        Is.EqualTo(TransformationBankDemoBuilder.ParticleMeshAssetPath));
                    return renderer.mesh.name;
                })
                .Distinct()
                .ToArray();

            Assert.That(primaryMeshNames, Has.Length.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void AstralAndGaiaDisableParticles()
        {
            foreach (var component in OpenComponents().Where(item => item.name.Contains("Astral") || item.name.Contains("Gaia")))
            {
                var serialized = new SerializedObject(component);
                foreach (var property in new[] { "primaryParticles", "accentParticles" })
                {
                    var particles = (ParticleSystem)serialized.FindProperty(property).objectReferenceValue;
                    Assert.That(particles.GetComponent<ParticleSystemRenderer>().enabled, Is.False);
                    Assert.That(particles.emission.enabled, Is.False);
                }
            }
        }

        [Test]
        public void UmbraUsesMistMeshesAndFlameUsesEmbers()
        {
            var components = OpenComponents();
            var umbra = new SerializedObject(components.First(item => item.name.Contains("Umbra")));
            var flame = new SerializedObject(components.First(item => item.name.Contains("Flame")));
            foreach (var property in new[] { "primaryParticles", "accentParticles" })
            {
                var umbraParticles = (ParticleSystem)umbra.FindProperty(property).objectReferenceValue;
                var flameParticles = (ParticleSystem)flame.FindProperty(property).objectReferenceValue;
                Assert.That(umbraParticles.GetComponent<ParticleSystemRenderer>().mesh.name, Does.EndWith("MistOrb"));
                Assert.That(
                    AssetDatabase.GetAssetPath(umbraParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial),
                    Is.EqualTo(TransformationBankDemoBuilder.MistParticleMaterialPath));
                Assert.That(flameParticles.GetComponent<ParticleSystemRenderer>().mesh.name, Does.EndWith("Ember"));
            }
        }

        [Test]
        public void MeltUsesDropletAndBeadParticles()
        {
            var component = OpenComponents().First(item => item.name.Contains("Melt"));
            var serialized = new SerializedObject(component);
            var primary = (ParticleSystem)serialized.FindProperty("primaryParticles").objectReferenceValue;
            var accent = (ParticleSystem)serialized.FindProperty("accentParticles").objectReferenceValue;

            Assert.That(primary.GetComponent<ParticleSystemRenderer>().mesh.name, Does.EndWith("Droplet"));
            Assert.That(accent.GetComponent<ParticleSystemRenderer>().mesh.name, Does.EndWith("Bead"));
        }

        [Test]
        public void ManualProgressUpdateReusesGeneratedMaterials()
        {
            var component = OpenComponents().First(item => item.name.StartsWith("Style 00"));
            var serialized = new SerializedObject(component);
            var renderer = (Renderer)serialized.FindProperty("incomingRenderer").objectReferenceValue;
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
