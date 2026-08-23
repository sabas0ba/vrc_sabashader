using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaShader.CI
{
    /// <summary>
    /// Illust2D を目視確認するためのシーンを組み立てる。
    ///
    /// ShaderCompileChecker が「コンパイルが通るか」を見るのに対し、
    /// こちらは「実際にどう見えるか」を見るためのもの。
    /// 並べるバリエーションは tests/cases.py の sphere_* と同じパラメータで、
    /// ライト方向・ライト色・環境光・落ち影の位置もヘッドレス回帰テストに揃えてある。
    /// tests/golden/sphere_*.png と直接見比べられる。
    /// </summary>
    public static class Illust2DCheckScene
    {
        const string ShaderPath = ShaderCompileChecker.Illust2DPath;
        const string RootDir = "Assets/Illust2DCheck";
        const string MaterialDir = RootDir + "/Materials";
        const string ScenePath = RootDir + "/Illust2DCheck.unity";

        // tests/cases.py の Case 既定値
        static readonly Vector3 LightDirection = new Vector3(0.55f, 0.62f, -0.56f);
        static readonly Color LightColor = new Color(1.00f, 0.97f, 0.92f);
        static readonly Color AmbientColor = new Color(0.16f, 0.18f, 0.24f);

        // ヘッドレステストは球の緯度でベースカラーを変えているが、
        // ここではテクスチャ無しの単色にして陰の色作りだけを見る。
        static readonly Color BaseColor = new Color(0.82f, 0.66f, 0.62f);

        static bool Failed;

        sealed class Variant
        {
            public string Name;
            public Dictionary<string, float> Floats = new Dictionary<string, float>();
            public Dictionary<string, Color> Colors = new Dictionary<string, Color>();
        }

        static readonly Variant[] Variants =
        {
            new Variant { Name = "default" },
            new Variant
            {
                Name = "hard_cel",
                Floats =
                {
                    { "_ShadeBlur1", 0.0f }, { "_ShadeBlur2", 0.0f },
                    { "_ShadeBorder1", 0.58f }, { "_ShadeBorder2", 0.34f },
                },
            },
            new Variant
            {
                Name = "posterized",
                Floats = { { "_ShadeBlur1", 0.55f }, { "_ShadeBlur2", 0.55f }, { "_ShadeSteps", 4.0f } },
            },
            new Variant
            {
                Name = "rim_specular",
                Floats = { { "_RimBorder", 0.6f }, { "_RimBlur", 0.25f }, { "_SpecularBorder", 0.35f } },
                Colors =
                {
                    { "_RimColor", new Color(0.85f, 0.72f, 1.00f) },
                    { "_SpecularColor", new Color(1.40f, 1.35f, 1.20f) },
                },
            },
            new Variant
            {
                Name = "flat_lighting",
                Floats = { { "_MonochromeLighting", 1.0f }, { "_AsUnlit", 0.4f }, { "_LightMinLimit", 0.35f } },
            },
            new Variant
            {
                Name = "no_shadow",
                Floats = { { "_ShadowStrength", 0.0f } },
            },
        };

        [MenuItem("Tools/SabaShader/Illust2D 確認シーンを生成")]
        public static void BuildFromMenu()
        {
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>batchmode 用。失敗があれば終了コード 1 で落とす。</summary>
        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(Failed ? 1 : 0);
        }

        /// <summary>
        /// batchmode 用。シーンを開いてカメラの絵を PNG に書き出す。
        /// 出力先は -captureOutput &lt;path&gt; で渡す。
        /// -shadowsOff を付けるとライト側の影を切って描画する。
        /// 実際に描画するので、シェーダーのバリアントが本当にコンパイルできるかもここで分かる。
        /// </summary>
        public static void CaptureBatch()
        {
            Failed = false;

            var output = ArgumentValue("-captureOutput");
            if (string.IsNullOrEmpty(output))
            {
                Fail("-captureOutput が指定されていません。");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (ArgumentValue("-shadowsOff") != null)
            {
                foreach (var l in Object.FindObjectsOfType<Light>()) l.shadows = LightShadows.None;
                Debug.Log("[Illust2DCheckScene] ライトの影を無効にして描画します。");
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            var camera = Object.FindObjectOfType<Camera>();
            if (shader == null || camera == null)
            {
                Fail(shader == null ? "シェーダーを読み込めませんでした。" : "シーンにカメラがありません。");
                EditorApplication.Exit(1);
                return;
            }

            const int width = 1600;
            const int height = 400;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            camera.targetTexture = target;
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;

            System.IO.File.WriteAllBytes(output, texture.EncodeToPNG());
            Debug.Log("[Illust2DCheckScene] 書き出しました: " + output);

            // 描画後なら実コンパイルの結果がメッセージに乗る。
            ReportShaderMessages(shader);

            EditorApplication.Exit(Failed ? 1 : 0);
        }

        public static void Build()
        {
            Failed = false;
            AssetDatabase.Refresh();

            var shader = ShaderCompileChecker.ImportAndLoad(ShaderPath);
            if (shader == null)
            {
                Fail("シェーダーを読み込めませんでした: " + ShaderPath);
                return;
            }

            ReportShaderMessages(shader);

            EnsureFolders();
            var materials = Variants.Select(v => CreateMaterial(shader, v)).ToArray();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PopulateScene(materials);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[Illust2DCheckScene] 生成しました: " + ScenePath);
        }

        static void Fail(string message)
        {
            Failed = true;
            Debug.LogError("[Illust2DCheckScene] " + message);
        }

        static string ArgumentValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static void ReportShaderMessages(Shader shader)
        {
            if (ShaderUtil.GetShaderMessageCount(shader) == 0)
            {
                Debug.Log("[Illust2DCheckScene] シェーダーのコンパイルメッセージはありません。");
                return;
            }

            var report = new StringBuilder();
            var errors = 0;
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) errors++;
                report.AppendLine(string.Format(
                    "{0} {1}:{2} [{3}] {4}",
                    message.severity, message.file, message.line, message.platform, message.message));
            }

            if (errors > 0) Fail("コンパイルエラー " + errors + " 件\n" + report);
            else Debug.LogWarning("[Illust2DCheckScene] 警告のみ\n" + report);
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(RootDir)) AssetDatabase.CreateFolder("Assets", "Illust2DCheck");
            if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder(RootDir, "Materials");
        }

        static Material CreateMaterial(Shader shader, Variant variant)
        {
            var path = MaterialDir + "/Illust2D_" + variant.Name + ".mat";
            var material = new Material(shader) { name = "Illust2D_" + variant.Name };
            material.SetColor("_BaseColor", BaseColor);

            foreach (var entry in variant.Floats)
            {
                if (!material.HasProperty(entry.Key)) { Fail("未定義のプロパティ: " + entry.Key); continue; }
                material.SetFloat(entry.Key, entry.Value);
            }
            foreach (var entry in variant.Colors)
            {
                if (!material.HasProperty(entry.Key)) { Fail("未定義のプロパティ: " + entry.Key); continue; }
                material.SetColor(entry.Key, entry.Value);
            }

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static void PopulateScene(IReadOnlyList<Material> materials)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = LightColor;
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;
            // cases.py の light_dir は面から光源へ向くベクトル。Unity の forward はその逆。
            lightObject.transform.rotation = Quaternion.LookRotation(-LightDirection.normalized);

            const float spacing = 1.3f;
            const float radius = 0.5f;
            var offset = spacing * (materials.Count - 1) * 0.5f;

            for (var i = 0; i < materials.Count; i++)
            {
                var position = new Vector3(i * spacing - offset, 0.0f, 0.0f);

                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Illust2D_" + Variants[i].Name;
                sphere.transform.position = position;
                sphere.GetComponent<MeshRenderer>().sharedMaterial = materials[i];

                // 落ち影を作る遮蔽物。_ShadowStrength の違いを見るために必ず置く。
                // 位置と大きさは tests/harness/scene.frag の
                // step(0.34, length(pos.xy - vec2(-0.42, 0.30))) に合わせてある。
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blocker.name = sphere.name + "_Blocker";
                blocker.transform.position =
                    position
                    + new Vector3(-0.42f, 0.30f, 0.0f) * radius
                    + LightDirection.normalized * 1.4f;
                blocker.transform.localScale = Vector3.one * (0.34f * radius * 2.0f);
                blocker.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // ゴールデン画像と同じ背景色にして見比べやすくする。
            camera.backgroundColor = new Color(0.34f, 0.35f, 0.42f);
            camera.orthographic = true;
            camera.orthographicSize = 1.0f;
            camera.nearClipPlane = 0.01f;
            camera.transform.position = new Vector3(0.0f, 0.0f, -5.0f);
            camera.transform.rotation = Quaternion.identity;
        }
    }
}
