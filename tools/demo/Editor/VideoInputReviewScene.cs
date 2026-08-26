using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaShader.Demo
{
    /// <summary>
    /// Video Input モジュールを実際の RenderTexture で確認するレビュー用シーンを作る。
    /// 左から素の入力、Tint と UV 反転、CRT との連結を同じ入力で比較する。
    /// </summary>
    public static class VideoInputReviewScene
    {
        const string ShaderPath =
            "Packages/io.github.sabas0ba.sabashader/Shaders/Illust2D/Illust2D.scshader";
        const string ScenePath = "Assets/Demo/VideoInputReview.unity";
        const string ReviewDir = "Assets/Demo/VideoInputReview";
        const string RenderTexturePath = ReviewDir + "/VideoInput.renderTexture";
        const int SourceLayer = 31;

        const string VideoPrefix = "_io_github_sabas0ba_videoinput_";
        const string CrtPrefix = "_io_github_sabas0ba_crtglitch_";

        [MenuItem("SabaShader/Demo/Build Video Input Review")]
        public static void Build()
        {
            var shader = ImportShader();
            ReportShaderMessages(shader);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RecreateReviewDirectory();

            var input = CreateRenderTexture();
            var sourceCamera = CreateSourceStage(input);
            CreateReviewStage(shader, input);

            sourceCamera.Render();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.InvalidOperationException("レビューシーンを保存できませんでした: " + ScenePath);

            Debug.Log("[VideoInputReviewScene] 準備完了: " + ScenePath);

            if (!Application.isBatchMode)
            {
                var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType != null) EditorWindow.GetWindow(gameViewType).Show();
            }
        }

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        static Shader ImportShader()
        {
            AssetDatabase.ImportAsset(
                ShaderPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                throw new System.InvalidOperationException("Illust2D を読み込めませんでした: " + ShaderPath);
            return shader;
        }

        static void ReportShaderMessages(Shader shader)
        {
            var report = new StringBuilder();
            var errors = 0;
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) errors++;
                report.AppendLine(
                    $"{message.severity} {message.file}:{message.line} [{message.platform}] {message.message}");
            }

            if (ShaderUtil.ShaderHasError(shader) || errors > 0)
                throw new System.InvalidOperationException(
                    "Illust2D のコンパイルエラーを検出しました。\n" + report);

            if (report.Length == 0)
                Debug.Log("[VideoInputReviewScene] シェーダーのコンパイルメッセージはありません。");
            else
                Debug.LogWarning("[VideoInputReviewScene] シェーダーの警告:\n" + report);
        }

        static void RecreateReviewDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Demo"))
                AssetDatabase.CreateFolder("Assets", "Demo");
            if (AssetDatabase.IsValidFolder(ReviewDir))
                AssetDatabase.DeleteAsset(ReviewDir);
            AssetDatabase.CreateFolder("Assets/Demo", "VideoInputReview");
        }

        static RenderTexture CreateRenderTexture()
        {
            var texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                name = "Video Input Review",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
            };
            AssetDatabase.CreateAsset(texture, RenderTexturePath);
            texture.Create();
            return texture;
        }

        static Camera CreateSourceStage(RenderTexture target)
        {
            var background = CreatePrimitive(
                PrimitiveType.Quad,
                "Source Background",
                new Vector3(0.0f, 0.0f, 1.2f),
                new Vector3(6.0f, 3.5f, 1.0f),
                Quaternion.identity,
                CreateStandardMaterial("SourceBackground", new Color(0.04f, 0.06f, 0.10f, 1.0f)));
            SetLayerRecursively(background, SourceLayer);

            var cube = CreatePrimitive(
                PrimitiveType.Cube,
                "Source Cube",
                new Vector3(-1.15f, 0.15f, 0.0f),
                new Vector3(1.25f, 1.25f, 1.25f),
                Quaternion.Euler(18.0f, -28.0f, 8.0f),
                CreateStandardMaterial("SourceRed", new Color(0.92f, 0.20f, 0.16f, 1.0f)));
            SetLayerRecursively(cube, SourceLayer);

            var sphere = CreatePrimitive(
                PrimitiveType.Sphere,
                "Source Sphere",
                new Vector3(0.95f, 0.25f, -0.1f),
                new Vector3(1.45f, 1.45f, 1.45f),
                Quaternion.identity,
                CreateStandardMaterial("SourceCyan", new Color(0.08f, 0.72f, 0.82f, 1.0f)));
            SetLayerRecursively(sphere, SourceLayer);

            var floor = CreatePrimitive(
                PrimitiveType.Cube,
                "Source Floor",
                new Vector3(0.0f, -1.0f, 0.2f),
                new Vector3(4.8f, 0.25f, 2.5f),
                Quaternion.identity,
                CreateStandardMaterial("SourceFloor", new Color(0.84f, 0.68f, 0.18f, 1.0f)));
            SetLayerRecursively(floor, SourceLayer);

            var lightObject = new GameObject("Source Light") { layer = SourceLayer };
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1.0f, 0.93f, 0.82f, 1.0f);
            light.cullingMask = 1 << SourceLayer;
            lightObject.transform.rotation = Quaternion.Euler(35.0f, -30.0f, 0.0f);

            var cameraObject = new GameObject("RenderTexture Source Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.025f, 0.04f, 1.0f);
            camera.cullingMask = 1 << SourceLayer;
            camera.fieldOfView = 42.0f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20.0f;
            camera.transform.position = new Vector3(0.0f, 0.4f, -5.8f);
            camera.transform.rotation = Quaternion.identity;
            return camera;
        }

        static void CreateReviewStage(Shader shader, RenderTexture input)
        {
            var mainCameraObject = new GameObject("Main Camera");
            mainCameraObject.tag = "MainCamera";
            var camera = mainCameraObject.AddComponent<Camera>();
            mainCameraObject.AddComponent<AudioListener>();
            camera.orthographic = true;
            camera.orthographicSize = 3.25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.06f, 0.075f, 1.0f);
            camera.cullingMask = ~(1 << SourceLayer);
            camera.transform.position = new Vector3(0.0f, 0.0f, -10.0f);
            camera.transform.rotation = Quaternion.identity;

            var direct = CreateVideoMaterial(shader, input, "VideoInput", Color.white, false, false, false);
            var flipped = CreateVideoMaterial(
                shader,
                input,
                "VideoInputTintFlip",
                new Color(0.55f, 1.0f, 1.25f, 0.82f),
                true,
                true,
                false);
            var crt = CreateVideoMaterial(shader, input, "VideoInputCrt", Color.white, false, false, true);

            CreatePanel("Input", new Vector3(-3.4f, 0.1f, 0.0f), direct, "INPUT");
            CreatePanel("TintFlip", new Vector3(0.0f, 0.1f, 0.0f), flipped, "TINT + FLIP");
            CreatePanel("CrtChain", new Vector3(3.4f, 0.1f, 0.0f), crt, "CRT CHAIN");
            CreateLabel("Video Input / RenderTexture review", new Vector3(0.0f, 2.35f, -0.05f), 0.08f);
            CreateLabel("same live input", new Vector3(0.0f, -1.85f, -0.05f), 0.055f);
        }

        static Material CreateVideoMaterial(
            Shader shader,
            RenderTexture input,
            string name,
            Color tint,
            bool mirrorX,
            bool flipY,
            bool withCrt)
        {
            var material = new Material(shader) { name = name };
            RequireProperty(material, VideoPrefix + "Amount");
            RequireProperty(material, VideoPrefix + "VideoTexture");

            material.SetTexture(VideoPrefix + "VideoTexture", input);
            material.SetFloat(VideoPrefix + "Amount", 1.0f);
            material.SetColor(VideoPrefix + "Tint", tint);
            material.SetFloat(VideoPrefix + "Brightness", 1.0f);
            material.SetInteger(VideoPrefix + "MirrorX", mirrorX ? 1 : 0);
            material.SetInteger(VideoPrefix + "FlipY", flipY ? 1 : 0);

            if (withCrt)
            {
                RequireProperty(material, CrtPrefix + "Amount");
                material.SetFloat(CrtPrefix + "Amount", 1.0f);
                material.SetFloat(CrtPrefix + "Scanline", 0.55f);
                material.SetFloat(CrtPrefix + "ScanlinePitch", 4.0f);
                material.SetFloat(CrtPrefix + "Mask", 0.32f);
                material.SetFloat(CrtPrefix + "MaskPitch", 6.0f);
                material.SetFloat(CrtPrefix + "Vignette", 0.25f);
                material.SetFloat(CrtPrefix + "Noise", 0.025f);
            }

            var path = ReviewDir + "/" + name + ".mat";
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material CreateStandardMaterial(string name, Color color)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) throw new System.InvalidOperationException("Standard shader が見つかりません。");
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, ReviewDir + "/" + name + ".mat");
            return material;
        }

        static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.localScale = scale;
            instance.transform.rotation = rotation;
            instance.GetComponent<Renderer>().sharedMaterial = material;

            var collider = instance.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return instance;
        }

        static void CreatePanel(string name, Vector3 position, Material material, string label)
        {
            CreatePrimitive(
                PrimitiveType.Quad,
                name,
                position,
                new Vector3(3.0f, 1.75f, 1.0f),
                Quaternion.identity,
                material);
            CreateLabel(label, position + new Vector3(0.0f, -1.2f, -0.05f), 0.055f);
        }

        static void CreateLabel(string value, Vector3 position, float size)
        {
            var instance = new GameObject("Label " + value);
            instance.transform.position = position;
            var text = instance.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = size;
            text.fontSize = 64;
            text.color = new Color(0.92f, 0.94f, 1.0f, 1.0f);
        }

        static void RequireProperty(Material material, string property)
        {
            if (!material.HasProperty(property))
                throw new System.InvalidOperationException(
                    $"{material.shader.name} に {property} がありません。Video Input モジュールの有効化を確認してください。");
        }

        static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
