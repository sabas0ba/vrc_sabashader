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
        public void パッケージにシェーダーが存在する()
        {
            var paths = ShaderCompileChecker.FindShaderPaths();
            Assert.That(paths, Is.Not.Empty, $"{ShaderCompileChecker.PackagePath} にシェーダーが見つかりません");
        }

        [Test]
        public void Illust2Dがシェーダーとしてインポートできる()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.Illust2DPath);
            Assert.That(shader, Is.Not.Null,
                "Shader として読み込めません。Shader Core の SCShaderImporter が動いていない可能性があります");
            Assert.That(shader.name, Is.EqualTo("SabaShader/Illust2D"));
        }

        [Test]
        public void すべてのシェーダーがエラー無くコンパイルされる()
        {
            var failures = ShaderCompileChecker.CollectFailures();
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void Illust2Dが4つのパスを持つ()
        {
            var shader = ShaderCompileChecker.ImportAndLoad(ShaderCompileChecker.Illust2DPath);
            Assert.That(shader, Is.Not.Null);

            var passes = ShaderCompileChecker.PassNames(shader);
            CollectionAssert.IsSubsetOf(ShaderCompileChecker.ExpectedPasses, passes);
        }

        [Test]
        public void Illust2Dのマテリアルに必要なプロパティが生成される()
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
    }
}
