using System.Linq;
using NUnit.Framework;
using SabaShader.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SabaShader.CI
{
    public sealed class TransformationBankClipGeneratorTests
    {
        const string TestRoot = "Assets/__TransformationBankClipGeneratorTests";

        GameObject avatar;
        GameObject outfitA;
        GameObject outfitB;
        Renderer rendererA;
        Renderer rendererB;
        Material materialA;
        Material materialB;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.CreateFolder("Assets", "__TransformationBankClipGeneratorTests");

            var shader = Shader.Find("SabaShader/Illust2D");
            Assert.That(shader, Is.Not.Null);
            materialA = new Material(shader) { name = "Outfit A Source" };
            materialB = new Material(shader) { name = "Outfit B Source" };
            AssetDatabase.CreateAsset(materialA, TestRoot + "/OutfitA.mat");
            AssetDatabase.CreateAsset(materialB, TestRoot + "/OutfitB.mat");

            avatar = new GameObject("Avatar");
            outfitA = CreateOutfit("Outfit A", materialA, out rendererA);
            outfitB = CreateOutfit("Outfit B", materialB, out rendererB);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (avatar != null)
            {
                Object.DestroyImmediate(avatar);
            }
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void GenerateCreatesTwoClipsRoleMaterialsAndReportWithoutChangingScene()
        {
            var result = TransformationBankClipGenerator.Generate(CreateOptions());

            Assert.That(result.AToBClip, Is.Not.Null);
            Assert.That(result.BToAClip, Is.Not.Null);
            Assert.That(result.Materials, Has.Length.EqualTo(4));
            Assert.That(result.Report, Is.Not.Null);
            Assert.That(AssetDatabase.Contains(result.AToBClip), Is.True);
            Assert.That(AssetDatabase.Contains(result.BToAClip), Is.True);
            Assert.That(AssetDatabase.Contains(result.Report), Is.True);
            Assert.That(
                AssetDatabase.GetAssetPath(rendererA.sharedMaterial),
                Is.EqualTo(TestRoot + "/OutfitA.mat"));
            Assert.That(
                AssetDatabase.GetAssetPath(rendererB.sharedMaterial),
                Is.EqualTo(TestRoot + "/OutfitB.mat"));

            Assert.That(
                result.Materials.Select(material => material.GetInteger(TransformationBankClipGenerator.RoleProperty)),
                Is.EquivalentTo(new[] { 0, 1, 0, 1 }));
            Assert.That(
                result.Materials.All(material =>
                    material.GetInteger(TransformationBankClipGenerator.StyleProperty) ==
                    (int)TransformationBankStyle.Flame),
                Is.True);
            Assert.That(
                result.Materials.All(material =>
                    material.GetFloat(TransformationBankClipGenerator.EffectIntensityProperty) == 2.25f),
                Is.True);
            Assert.That(result.Report.OutfitAPath, Is.EqualTo("Outfit A"));
            Assert.That(result.Report.OutfitBPath, Is.EqualTo("Outfit B"));
            Assert.That(result.Report.GeneratedMaterials, Has.Length.EqualTo(4));
            Assert.That(
                AssetDatabase.FindAssets("t:AnimatorController", new[] { result.OutputFolder }),
                Is.Empty);
        }

        [Test]
        public void GeneratedClipsKeepBothOutfitsActiveUntilOutgoingIsFullyClipped()
        {
            var options = CreateOptions();
            var result = TransformationBankClipGenerator.Generate(options);
            AssertTransition(result.AToBClip, "Outfit A", "Outfit B", options.Duration);
            AssertTransition(result.BToAClip, "Outfit B", "Outfit A", options.Duration);
        }

        [Test]
        public void GenerateCreatesMaterialReferenceCurveForEveryMaterialSlot()
        {
            rendererA.sharedMaterials = new[] { materialA, materialA };

            var result = TransformationBankClipGenerator.Generate(CreateOptions());
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(result.AToBClip);
            var outfitABindings = bindings.Where(binding => binding.path == "Outfit A/Body").ToArray();

            Assert.That(outfitABindings, Has.Length.EqualTo(2));
            Assert.That(
                outfitABindings.Select(binding => binding.propertyName),
                Is.EquivalentTo(new[]
                {
                    "m_Materials.Array.data[0]",
                    "m_Materials.Array.data[1]",
                }));
            Assert.That(result.Materials, Has.Length.EqualTo(4));
        }

        [Test]
        public void GeneratedClipSamplesRoleMaterialsAndRestoresSceneAfterPreview()
        {
            avatar.AddComponent<Animator>();
            var options = CreateOptions();
            var result = TransformationBankClipGenerator.Generate(options);

            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(avatar, result.AToBClip, options.Duration * 0.5f);
                AnimationMode.EndSampling();

                Assert.That(outfitA.activeSelf, Is.True);
                Assert.That(outfitB.activeSelf, Is.True);
                Assert.That(
                    rendererA.sharedMaterial.GetInteger(TransformationBankClipGenerator.RoleProperty),
                    Is.EqualTo((int)TransformationBankRole.Outgoing));
                Assert.That(
                    rendererB.sharedMaterial.GetInteger(TransformationBankClipGenerator.RoleProperty),
                    Is.EqualTo((int)TransformationBankRole.Incoming));
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }
            }

            Assert.That(AssetDatabase.GetAssetPath(rendererA.sharedMaterial), Is.EqualTo(TestRoot + "/OutfitA.mat"));
            Assert.That(AssetDatabase.GetAssetPath(rendererB.sharedMaterial), Is.EqualTo(TestRoot + "/OutfitB.mat"));
        }

        [Test]
        public void ValidateRejectsMaterialWithoutTransformationBankProperties()
        {
            var shader = Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            var unsupported = new Material(shader) { name = "Unsupported" };
            AssetDatabase.CreateAsset(unsupported, TestRoot + "/Unsupported.mat");
            rendererA.sharedMaterial = unsupported;

            var options = CreateOptions();
            var errors = TransformationBankClipGenerator.Validate(options);

            Assert.That(errors, Has.Some.Contains("Transformation Bankが有効ではありません"));
            Assert.That(
                () => TransformationBankClipGenerator.Generate(options),
                Throws.TypeOf<System.InvalidOperationException>());
            Assert.That(AssetDatabase.IsValidFolder(TestRoot + "/Generated"), Is.False);
        }

        [Test]
        public void ValidateRejectsDuplicateRendererBindingPaths()
        {
            var duplicate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            duplicate.name = "Body";
            duplicate.transform.SetParent(outfitA.transform, false);
            Object.DestroyImmediate(duplicate.GetComponent<Collider>());
            duplicate.GetComponent<MeshRenderer>().sharedMaterial = materialA;

            var errors = TransformationBankClipGenerator.Validate(CreateOptions());

            Assert.That(errors, Has.Some.Contains("RendererのAnimation binding pathが重複しています"));
        }

        TransformationBankClipGenerationOptions CreateOptions()
        {
            return new TransformationBankClipGenerationOptions
            {
                AvatarRoot = avatar,
                OutfitA = outfitA,
                OutfitB = outfitB,
                Style = TransformationBankStyle.Flame,
                Duration = 1.5f,
                EffectIntensity = 2.25f,
                ApplyRecommendedPreset = true,
                OutputFolder = TestRoot + "/Generated",
            };
        }

        GameObject CreateOutfit(string name, Material material, out Renderer renderer)
        {
            var outfit = new GameObject(name);
            outfit.transform.SetParent(avatar.transform, false);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Body";
            mesh.transform.SetParent(outfit.transform, false);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            renderer = mesh.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return outfit;
        }

        static void AssertTransition(
            AnimationClip clip,
            string outgoingRoot,
            string incomingRoot,
            float duration)
        {
            var floatBindings = AnimationUtility.GetCurveBindings(clip);
            var activeBindings = floatBindings.Where(binding => binding.propertyName == "m_IsActive").ToArray();
            Assert.That(activeBindings, Has.Length.EqualTo(2));

            var outgoingActive = AnimationUtility.GetEditorCurve(
                clip,
                activeBindings.Single(binding => binding.path == outgoingRoot));
            var incomingActive = AnimationUtility.GetEditorCurve(
                clip,
                activeBindings.Single(binding => binding.path == incomingRoot));
            Assert.That(outgoingActive.Evaluate(duration - 1.0f / 120.0f), Is.EqualTo(1.0f));
            Assert.That(outgoingActive.Evaluate(duration), Is.EqualTo(0.0f));
            Assert.That(incomingActive.Evaluate(0.0f), Is.EqualTo(1.0f));
            Assert.That(incomingActive.Evaluate(duration), Is.EqualTo(1.0f));

            var progressBindings = floatBindings
                .Where(binding => binding.propertyName == TransformationBankClipGenerator.MaterialProgressBinding)
                .ToArray();
            Assert.That(progressBindings, Has.Length.EqualTo(2));
            foreach (var binding in progressBindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                Assert.That(curve.Evaluate(0.0f), Is.EqualTo(0.0f).Within(0.0001f));
                Assert.That(curve.Evaluate(duration * 0.5f), Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(curve.Evaluate(duration), Is.EqualTo(1.0f).Within(0.0001f));
            }

            var materialBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.That(materialBindings, Has.Length.EqualTo(2));
            foreach (var binding in materialBindings)
            {
                Assert.That(binding.propertyName, Is.EqualTo("m_Materials.Array.data[0]"));
                var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                Assert.That(keyframes, Has.Length.EqualTo(1));
                Assert.That(keyframes[0].time, Is.EqualTo(0.0f));
                var material = (Material)keyframes[0].value;
                var expectedRole = binding.path.StartsWith(outgoingRoot)
                    ? TransformationBankRole.Outgoing
                    : TransformationBankRole.Incoming;
                Assert.That(
                    material.GetInteger(TransformationBankClipGenerator.RoleProperty),
                    Is.EqualTo((int)expectedRole));
            }
        }
    }
}
