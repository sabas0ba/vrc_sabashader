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
        const string MeshDir = RootDir + "/Meshes";
        const string ScenePath = RootDir + "/Illust2DCheck.unity";

        // カメラの構図はこの縦横比を前提に決める。撮影時に変えると構図がずれる。
        const int CaptureWidth = 2400;
        const int CaptureHeight = 1400;

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
            // ここから下はモジュール。プロパティ名には uniqueID が前置きされる。
            new Variant
            {
                Name = "snow",
                Floats =
                {
                    { "_io_github_sabas0ba_surfaceoverlay_Amount", 1.0f },
                    { "_io_github_sabas0ba_surfaceoverlay_UpBias", 1.0f },
                    { "_io_github_sabas0ba_surfaceoverlay_Border", 0.62f },
                    { "_io_github_sabas0ba_surfaceoverlay_Blur", 0.1f },
                    { "_io_github_sabas0ba_surfaceoverlay_Flatten", 0.8f },
                    { "_io_github_sabas0ba_surfaceoverlay_Thickness", 0.03f },
                },
                Colors = { { "_io_github_sabas0ba_surfaceoverlay_Color", new Color(0.94f, 0.96f, 1.0f, 1.0f) } },
            },
            new Variant
            {
                Name = "wet",
                Floats =
                {
                    { "_io_github_sabas0ba_surfaceoverlay_Amount", 1.0f },
                    { "_io_github_sabas0ba_surfaceoverlay_UpBias", 0.5f },
                    { "_io_github_sabas0ba_surfaceoverlay_Border", 0.4f },
                    { "_io_github_sabas0ba_surfaceoverlay_Blur", 0.5f },
                    { "_io_github_sabas0ba_surfaceoverlay_Darken", 1.0f },
                    { "_io_github_sabas0ba_surfaceoverlay_Droplet", 0.9f },
                    { "_io_github_sabas0ba_surfaceoverlay_DropletScale", 45.0f },
                    { "_io_github_sabas0ba_surfaceoverlay_DropletBump", 2.5f },
                    { "_io_github_sabas0ba_surfaceoverlay_Streak", 0.35f },
                    { "_io_github_sabas0ba_surfaceoverlay_StreakScale", 16.0f },
                },
                // アルファ 0 で色は置き換えず、沈みと垂れだけを効かせる
                Colors = { { "_io_github_sabas0ba_surfaceoverlay_Color", new Color(1.0f, 1.0f, 1.0f, 0.0f) } },
            },
            new Variant
            {
                Name = "pixel",
                Floats =
                {
                    { "_io_github_sabas0ba_pixelart_Amount", 1.0f },
                    { "_io_github_sabas0ba_pixelart_Levels", 6.0f },
                    { "_io_github_sabas0ba_pixelart_CellSize", 8.0f },
                    { "_io_github_sabas0ba_pixelart_Dither", 0.0f },
                },
            },
        };

        /// <summary>
        /// 並べる立体。球だけだと平らな面・鋭いエッジ・自己遮蔽での見え方が分からないので、
        /// 曲面（球）・平面と稜線（立方体）・押し出し曲面（カプセル / 円柱）・
        /// 自己遮蔽のある形（トーラス）を揃える。
        /// </summary>
        sealed class Shape
        {
            public string Name;
            // null のときは手続きで作るトーラス
            public PrimitiveType? Primitive;
            public Vector3 Scale = Vector3.one;
            public Vector3 Euler = Vector3.zero;
        }

        static readonly Shape[] Shapes =
        {
            // 球だけは tests/golden/sphere_*.png と直接比較できるよう向きを変えない
            new Shape { Name = "sphere", Primitive = PrimitiveType.Sphere },
            new Shape
            {
                Name = "cube",
                Primitive = PrimitiveType.Cube,
                Scale = Vector3.one * 0.58f,
                Euler = new Vector3(25.0f, 35.0f, 0.0f),
            },
            new Shape
            {
                Name = "capsule",
                Primitive = PrimitiveType.Capsule,
                Scale = Vector3.one * 0.40f,
                Euler = new Vector3(15.0f, 0.0f, 20.0f),
            },
            new Shape
            {
                Name = "cylinder",
                Primitive = PrimitiveType.Cylinder,
                Scale = new Vector3(0.45f, 0.40f, 0.45f),
                Euler = new Vector3(18.0f, 0.0f, 12.0f),
            },
            new Shape
            {
                Name = "torus",
                Primitive = null,
                Euler = new Vector3(62.0f, 0.0f, 10.0f),
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

            // グリッドの縦横比 (列 6 / 行 5) に合わせる
            const int width = CaptureWidth;
            const int height = CaptureHeight;
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

            // ProjectSettings は追跡していないので、見た目が環境に左右されないよう
            // VRChat と同じ Linear に固定する。Gamma のままだと陰の濃さが変わる。
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                Debug.Log("[Illust2DCheckScene] カラースペースを Linear に変更しました。");
            }

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
            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder(RootDir, "Meshes");
        }

        /// <summary>
        /// トーラス。Unity の組み込みプリミティブに無いので手続きで作る。
        /// 自己遮蔽と内側の曲面があり、トゥーン塗りの破綻が出やすい。
        /// </summary>
        static Mesh CreateTorusMesh(float major, float minor, int majorSegments, int minorSegments)
        {
            var stride = minorSegments + 1;
            var vertices = new Vector3[(majorSegments + 1) * stride];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[majorSegments * minorSegments * 6];

            var v = 0;
            for (var i = 0; i <= majorSegments; i++)
            {
                var u = (float)i / majorSegments;
                var theta = u * Mathf.PI * 2.0f;
                var outward = new Vector3(Mathf.Cos(theta), 0.0f, Mathf.Sin(theta));
                var center = outward * major;

                for (var j = 0; j <= minorSegments; j++)
                {
                    var w = (float)j / minorSegments;
                    var phi = w * Mathf.PI * 2.0f;
                    var n = outward * Mathf.Cos(phi) + Vector3.up * Mathf.Sin(phi);

                    vertices[v] = center + n * minor;
                    normals[v] = n;
                    uv[v] = new Vector2(u, w);
                    v++;
                }
            }

            var t = 0;
            for (var i = 0; i < majorSegments; i++)
            {
                for (var j = 0; j < minorSegments; j++)
                {
                    var a = i * stride + j;
                    var b = a + stride;

                    triangles[t++] = a;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;

                    triangles[t++] = a + 1;
                    triangles[t++] = b + 1;
                    triangles[t++] = b;
                }
            }

            var mesh = new Mesh { name = "Torus" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        static Mesh EnsureTorusAsset()
        {
            const string path = MeshDir + "/Torus.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(CreateTorusMesh(0.34f, 0.14f, 72, 28), path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
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

        static GameObject CreateSubject(Shape shape, Mesh torus)
        {
            if (shape.Primitive.HasValue) return GameObject.CreatePrimitive(shape.Primitive.Value);

            var go = new GameObject();
            go.AddComponent<MeshFilter>().sharedMesh = torus;
            go.AddComponent<MeshRenderer>();
            return go;
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

            // 列 = バリエーション、行 = 立体。左上が sphere / default。
            const float columnPitch = 1.5f;
            const float rowPitch = 1.5f;
            const float radius = 0.5f;

            var torus = EnsureTorusAsset();
            var columnOffset = columnPitch * (materials.Count - 1) * 0.5f;
            var rowOffset = rowPitch * (Shapes.Length - 1) * 0.5f;

            for (var row = 0; row < Shapes.Length; row++)
            {
                var shape = Shapes[row];

                for (var column = 0; column < materials.Count; column++)
                {
                    var position = new Vector3(
                        column * columnPitch - columnOffset,
                        rowOffset - row * rowPitch,
                        0.0f);

                    var subject = CreateSubject(shape, torus);
                    subject.name = "Illust2D_" + shape.Name + "_" + Variants[column].Name;
                    subject.transform.position = position;
                    subject.transform.localScale = shape.Scale;
                    subject.transform.rotation = Quaternion.Euler(shape.Euler);
                    subject.GetComponent<MeshRenderer>().sharedMaterial = materials[column];

                    // 落ち影を作る遮蔽物。_ShadowStrength の違いを見るために必ず置く。
                    // 位置と大きさは tests/harness/scene.frag の
                    // step(0.34, length(pos.xy - vec2(-0.42, 0.30))) に合わせてある。
                    var blocker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    blocker.name = subject.name + "_Blocker";
                    blocker.transform.position =
                        position
                        + new Vector3(-0.42f, 0.30f, 0.0f) * radius
                        + LightDirection.normalized * 1.4f;
                    blocker.transform.localScale = Vector3.one * (0.34f * radius * 2.0f);
                    blocker.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // ゴールデン画像と同じ背景色にして見比べやすくする。
            camera.backgroundColor = new Color(0.34f, 0.35f, 0.42f);

            // アウトラインは頂点をビュー方向に押し出すパスなので、
            // 正射投影だと _OutlineFixedWidth の経路が実運用と変わってしまう。
            // 歪みを抑えた狭い画角の透視投影にして、遠くから引きで撮る。
            const float fieldOfView = 20.0f;
            // 縦だけで合わせると列が増えたときに横がはみ出す。両方から必要量を取る。
            var aspect = CaptureWidth / (float)CaptureHeight;
            var halfHeight = Mathf.Max(rowOffset + 0.75f, (columnOffset + 0.75f) / aspect);
            var distance = halfHeight / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);

            camera.orthographic = false;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = distance * 2.0f;
            camera.transform.position = new Vector3(0.0f, 0.0f, -distance);
            camera.transform.rotation = Quaternion.identity;
        }
    }
}
