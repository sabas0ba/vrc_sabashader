using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SabaShader.CI
{
    /// <summary>
    /// game-ci/unity-test-runner から EditMode テストとして実行される。
    /// 落ちたときにどこが悪いか分かるよう、観点ごとにテストを分けてある。
    /// </summary>
    public class ShaderCompileTests
    {
        [Test]
        public void PackageContainsShaders()
        {
            var paths = ShaderCompileChecker.FindShaderPaths();
            Assert.That(paths, Is.Not.Empty, $"{ShaderCompileChecker.PackagePath} にシェーダーが見つかりません");
        }

        [Test]
        public void Illust2DImportsAsShader()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.Illust2DPath);
            Assert.That(shader, Is.Not.Null,
                "Shader として読み込めません。Shader Core の SCShaderImporter が動いていない可能性があります");
            Assert.That(shader.name, Is.EqualTo("SabaShader/Illust2D"));
        }

        [Test]
        public void DebugImportsAsShader()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.DebugPath);
            Assert.That(shader, Is.Not.Null,
                "Shader として読み込めません。Shader Core の SCShaderImporter が動いていない可能性があります");
            Assert.That(shader.name, Is.EqualTo("SabaShader/Debug"));
        }

        [Test]
        public void AllShadersCompileWithoutErrors()
        {
            var failures = ShaderCompileChecker.CollectFailures();
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void Illust2DDeclaresExpectedPasses()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.Illust2DPath);
            Assert.That(shader, Is.Not.Null);

            var passes = ShaderCompileChecker.PassNames(shader);
            CollectionAssert.IsSubsetOf(ShaderCompileChecker.ExpectedPasses, passes);
        }

        [Test]
        public void Illust2DMaterialExposesRequiredProperties()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.Illust2DPath);
            Assert.That(shader, Is.Not.Null);

            var material = new Material(shader);
            try
            {
                var missing = ShaderCompileChecker.RequiredProperties
                    .Where(property => !material.HasProperty(property))
                    .ToList();
                Assert.That(missing, Is.Empty, "生成されていないプロパティ: " + string.Join(", ", missing));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void DebugDeclaresExpectedPasses()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.DebugPath);
            Assert.That(shader, Is.Not.Null);

            var passes = ShaderCompileChecker.PassNames(shader);
            CollectionAssert.IsSubsetOf(ShaderCompileChecker.DebugExpectedPasses, passes);
        }

        [Test]
        public void DebugMaterialExposesRequiredProperties()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.DebugPath);
            Assert.That(shader, Is.Not.Null);

            var material = new Material(shader);
            try
            {
                var missing = ShaderCompileChecker.DebugRequiredProperties
                    .Where(property => !material.HasProperty(property))
                    .ToList();
                Assert.That(missing, Is.Empty, "生成されていないプロパティ: " + string.Join(", ", missing));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
