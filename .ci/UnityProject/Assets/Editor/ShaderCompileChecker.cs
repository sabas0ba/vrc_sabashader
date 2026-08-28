using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace SabaShader.CI
{
    /// <summary>
    /// パッケージ内のシェーダーを実際に Unity にインポート・コンパイルさせて検証する。
    ///
    /// Python 側のテストは「Unity 無しで分かること」しか見ていないので、
    /// HLSL が本当にコンパイルできるかはここでしか確認できない。
    /// Test Runner からも -executeMethod からも同じ判定を使えるようにしてある。
    /// </summary>
    public static class ShaderCompileChecker
    {
        public const string PackagePath = "Packages/io.github.sabas0ba.sabashader";
        public const string Illust2DPath = PackagePath + "/Shaders/Illust2D/Illust2D.scshader";
        public const string DebugPath = PackagePath + "/Shaders/Debug/Debug.scshader";

        public static readonly string[] ExpectedPasses =
        {
            "FORWARD",
            "OUTLINE",
            "FORWARD_DELTA",
            "SHADOW_CASTER",
        };

        // 欠けているとマテリアルエディタが壊れるプロパティ
        public static readonly string[] RequiredProperties =
        {
            "_BaseTexture",
            "_BaseTexture_ST",
            "_BaseColor",
            "_ShadeBorder1",
            "_OutlineWidth",
            "_Cull",
        };

        public static readonly string[] DebugExpectedPasses =
        {
            "FORWARD",
        };

        public static readonly string[] DebugRequiredProperties =
        {
            "_Mode",
            "_Cull",
            "_CoordinateScale",
            "_WireColor",
            "_BackgroundColor",
            "_WireWidth",
        };

        /// <summary>batchmode 用のエントリポイント。問題があれば終了コード 1 で落とす。</summary>
        public static void RunBatch()
        {
            List<string> failures;
            try
            {
                failures = CollectFailures();
            }
            catch (Exception exception)
            {
                Debug.LogError("[ShaderCompileChecker] 検証中に例外が発生しました: " + exception);
                Console.Error.WriteLine(exception.ToString());
                EditorApplication.Exit(1);
                return;
            }

            if (failures.Count > 0)
            {
                var report = new StringBuilder();
                report.AppendLine($"シェーダーのコンパイルに {failures.Count} 件の問題があります:");
                foreach (var failure in failures)
                {
                    report.AppendLine("  - " + failure);
                }

                Debug.LogError("[ShaderCompileChecker] " + report);
                Console.Error.WriteLine(report.ToString());
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[ShaderCompileChecker] すべてのシェーダーがエラー無くコンパイルされました");
            EditorApplication.Exit(0);
        }

        /// <summary>見つかった問題をすべて集めて返す。空なら合格。</summary>
        public static List<string> CollectFailures()
        {
            var failures = new List<string>();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var shaderPaths = FindShaderPaths();
            if (shaderPaths.Count == 0)
            {
                failures.Add("シェーダーが 1 つも見つかりませんでした: " + PackagePath);
                return failures;
            }

            Debug.Log($"[ShaderCompileChecker] {shaderPaths.Count} 個のシェーダーを検証します");

            foreach (var path in shaderPaths)
            {
                var shader = ImportAndLoad(path);
                if (shader == null)
                {
                    failures.Add($"{path}: Shader として読み込めませんでした（インポータが動いていない可能性）");
                    continue;
                }

                CollectMessageFailures(path, shader, failures);

                if (IsIllust2D(path))
                {
                    CollectPassFailures(path, shader, ExpectedPasses, failures);
                    CollectMaterialFailures(path, shader, RequiredProperties, failures);
                }
                else if (IsDebug(path))
                {
                    CollectPassFailures(path, shader, DebugExpectedPasses, failures);
                    CollectMaterialFailures(path, shader, DebugRequiredProperties, failures);
                }
            }

            return failures;
        }

        public static List<string> FindShaderPaths()
        {
            // .scshader は ScriptedImporter が Shader をメインオブジェクトにするので t:Shader で拾える
            return AssetDatabase.FindAssets("t:Shader", new[] { PackagePath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        public static Shader ImportAndLoad(string path)
        {
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Shader>(path);
        }

        public static List<string> PassNames(Shader shader)
        {
            // UnityEngine.Shader にパス名を取る API は無いので Material 経由で列挙する。
            // Material.passCount は有効な SubShader のパスだけを数える。
            var material = new Material(shader);
            try
            {
                var found = new List<string>();
                for (var pass = 0; pass < material.passCount; pass++)
                {
                    found.Add(material.GetPassName(pass));
                }

                return found;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static bool IsIllust2D(string path)
        {
            return path.EndsWith("Illust2D.scshader", StringComparison.Ordinal);
        }

        private static bool IsDebug(string path)
        {
            return path.EndsWith("Debug.scshader", StringComparison.Ordinal);
        }

        private static void CollectMessageFailures(string path, Shader shader, List<string> failures)
        {
            var messages = ShaderUtil.GetShaderMessages(shader);
            var errorCount = 0;

            foreach (var message in messages)
            {
                var location = string.IsNullOrEmpty(message.file) ? path : $"{message.file}:{message.line}";
                var text = $"{location} [{message.platform}] {message.message} {message.messageDetails}".Trim();

                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    errorCount++;
                    failures.Add(text);
                }
                else
                {
                    Debug.LogWarning("[ShaderCompileChecker] warning: " + text);
                }
            }

            if (errorCount == 0 && ShaderUtil.ShaderHasError(shader))
            {
                failures.Add($"{path}: ShaderHasError が true ですが詳細メッセージが取得できませんでした");
            }
        }

        private static void CollectPassFailures(
            string path,
            Shader shader,
            IEnumerable<string> expectedPasses,
            List<string> failures)
        {
            var found = PassNames(shader);
            Debug.Log($"[ShaderCompileChecker] {path}: パス [{string.Join(", ", found)}]");

            foreach (var expected in expectedPasses)
            {
                if (!found.Contains(expected))
                {
                    failures.Add($"{path}: パス {expected} がありません（実際: {string.Join(", ", found)}）");
                }
            }
        }

        private static void CollectMaterialFailures(
            string path,
            Shader shader,
            IEnumerable<string> requiredProperties,
            List<string> failures)
        {
            Material material = null;
            try
            {
                material = new Material(shader);
                foreach (var property in requiredProperties)
                {
                    if (!material.HasProperty(property))
                    {
                        failures.Add($"{path}: プロパティ {property} がマテリアルに生成されていません");
                    }
                }
            }
            finally
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
        }
    }
}
