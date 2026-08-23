using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaShader.Demo
{
    /// <summary>
    /// 取り込んだアバターに Illust2D を適用し、元のマテリアルと並べて比較する。
    ///
    /// アバターの実体はリポジトリに含めない。利用者が自分で入手したものを
    /// .demo/UnityProject/Assets/Avatar/ に置き、このスクリプトはそれを読むだけ。
    /// 生成物もすべて .demo/ の下に閉じる。
    /// </summary>
    public static class AvatarDemoScene
    {
        const string ShaderName = "SabaShader/Illust2D";
        const string ShaderPath = "Packages/jp.sabas0ba.sabashader/Shaders/Illust2D/Illust2D.scshader";
        const string AvatarDir = "Assets/Avatar";
        const string RootDir = "Assets/Demo";
        const string MaterialDir = RootDir + "/Materials";
        const string ScenePath = RootDir + "/AvatarDemo.unity";

        // カメラの画角はこの縦横比を前提に決める。撮影時に -captureWidth /
        // -captureHeight で変えると構図がずれるので、変えるならここも合わせる。
        const int CaptureWidth = 1920;
        const int CaptureHeight = 960;

        // .ci の確認シーンと同じライティング条件（tests/cases.py 由来）
        static readonly Vector3 LightDirection = new Vector3(0.55f, 0.62f, -0.56f);
        static readonly Color LightColor = new Color(1.00f, 0.97f, 0.92f);
        static readonly Color AmbientColor = new Color(0.16f, 0.18f, 0.24f);

        // 元マテリアルからベースカラーテクスチャを引き継ぐときに探すプロパティ名。
        // 上から順に見て最初に見つかったものを使う。
        static readonly string[] SourceTextureProperties =
        {
            "_MainTex", "_BaseMap", "_BaseColorMap", "_BaseTexture", "_MainTexture",
        };

        static readonly string[] SourceColorProperties =
        {
            "_Color", "_BaseColor", "_MainColor",
        };

        static bool Failed;

        [MenuItem("Tools/SabaShader/アバターデモを生成")]
        public static void BuildFromMenu()
        {
            Build();
            if (!Failed) EditorSceneManager.OpenScene(ScenePath);
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

            var camera = Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                Fail("シーンにカメラがありません。");
                EditorApplication.Exit(1);
                return;
            }

            var width = IntArgument("-captureWidth", CaptureWidth);
            var height = IntArgument("-captureHeight", CaptureHeight);

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
            Debug.Log("[AvatarDemoScene] 書き出しました: " + output);

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader != null) ReportShaderMessages(shader);

            EditorApplication.Exit(Failed ? 1 : 0);
        }

        public static void Build()
        {
            Failed = false;

            // VRChat と同じ Linear に固定する。ProjectSettings は追跡していない。
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                Debug.Log("[AvatarDemoScene] カラースペースを Linear に変更しました。");
            }

            AssetDatabase.Refresh();

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Fail("シェーダーを読み込めませんでした: " + ShaderName);
                return;
            }

            var sources = FindAvatarAssets();
            if (sources.Count == 0) return;

            EnsureFolders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PopulateScene(shader, sources);
            if (Failed) return;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[AvatarDemoScene] 生成しました: " + ScenePath);
        }

        /// <summary>
        /// 並べるアバターを決める。-avatarAsset にセミコロン区切りで明示できる。
        /// 指定が無ければ Assets/Avatar 配下のモデルをすべて使う。
        /// </summary>
        static List<GameObject> FindAvatarAssets()
        {
            var explicitPaths = ArgumentValue("-avatarAsset");
            if (!string.IsNullOrEmpty(explicitPaths))
            {
                var assets = new List<GameObject>();
                foreach (var path in explicitPaths.Split(';'))
                {
                    var trimmed = path.Trim();
                    if (trimmed.Length == 0) continue;

                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(trimmed);
                    if (asset == null) Fail("-avatarAsset を読み込めませんでした: " + trimmed);
                    else assets.Add(asset);
                }

                return assets;
            }

            if (!AssetDatabase.IsValidFolder(AvatarDir))
            {
                Fail($"{AvatarDir} がありません。tools/setup_demo_project.py を先に実行してください。");
                return new List<GameObject>();
            }

            var candidates = AssetDatabase.FindAssets("t:GameObject", new[] { AvatarDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Where(path => path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)
                               || path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToList();

            if (candidates.Count == 0)
            {
                Fail($"{AvatarDir} にモデル (fbx / prefab) が見つかりませんでした。");
                return new List<GameObject>();
            }

            Debug.Log("[AvatarDemoScene] アバター:\n  " + string.Join("\n  ", candidates));
            return candidates.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(a => a != null).ToList();
        }

        static void PopulateScene(Shader shader, IReadOnlyList<GameObject> sources)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = LightColor;
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.LookRotation(-LightDirection.normalized);

            // アバターごとに「元のマテリアル」「Illust2D」の 2 体を並べる。
            // 体型が違っても比べられるよう、足元を Y=0 に揃えて横に詰めていく。
            var placed = new List<GameObject>();
            var cursor = 0.0f;
            var gap = 0.0f;

            foreach (var source in sources)
            {
                var before = InstantiateAvatar(source, source.name + "_Original");
                var after = InstantiateAvatar(source, source.name + "_Illust2D");
                if (before == null || after == null) return;

                var fallback = FallbackTexture(source);
                PrepareOriginalMaterials(before, fallback);
                var converted = ConvertMaterials(shader, after, fallback);
                Debug.Log($"[AvatarDemoScene] {source.name}: マテリアル {converted} 個を Illust2D に差し替えました。");

                var reference = RendererBounds(after);
                if (reference.size == Vector3.zero)
                {
                    Fail("描画対象が見つかりませんでした: " + source.name);
                    return;
                }

                // 体格に対して自然な間隔になるよう、いちばん大きい個体に合わせる。
                gap = Mathf.Max(gap, reference.size.x * 0.35f);

                cursor = PlaceInstance(before, cursor) + reference.size.x * 0.2f;
                cursor = PlaceInstance(after, cursor) + gap;

                placed.Add(before);
                placed.Add(after);
            }

            var total = UnionBounds(placed);
            CenterHorizontally(placed, total.center.x);
            PlaceCamera(UnionBounds(placed));
        }

        /// <summary>足元を Y=0、奥行きを Z=0 に揃えつつ、cursorX から右へ詰める。</summary>
        static float PlaceInstance(GameObject instance, float cursorX)
        {
            var bounds = RendererBounds(instance);
            if (bounds.size == Vector3.zero) return cursorX;

            instance.transform.position += new Vector3(
                cursorX + bounds.size.x * 0.5f - bounds.center.x,
                -bounds.min.y,
                -bounds.center.z);

            return cursorX + bounds.size.x;
        }

        static void CenterHorizontally(IReadOnlyList<GameObject> instances, float centerX)
        {
            foreach (var instance in instances)
            {
                instance.transform.position -= new Vector3(centerX, 0.0f, 0.0f);
            }
        }

        static Bounds UnionBounds(IReadOnlyList<GameObject> instances)
        {
            var result = new Bounds();
            var initialized = false;

            foreach (var instance in instances)
            {
                var bounds = RendererBounds(instance);
                if (bounds.size == Vector3.zero) continue;

                if (!initialized)
                {
                    result = bounds;
                    initialized = true;
                }
                else
                {
                    result.Encapsulate(bounds);
                }
            }

            return result;
        }

        static GameObject InstantiateAvatar(GameObject source, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (instance == null) instance = Object.Instantiate(source);
            if (instance == null)
            {
                Fail("アバターをインスタンス化できませんでした: " + source.name);
                return null;
            }

            // シーンに保存したいので prefab との繋がりは切る。
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = name;

            // FBX の正面は +Z。カメラは -Z から +Z を向いているので、
            // そのままだと背中しか映らない。
            instance.transform.rotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            return instance;
        }

        /// <summary>
        /// 元マテリアルが参照するシェーダーがこのプロジェクトに無いと
        /// エラーシェーダー（ピンク）になる。比較の左側が潰れるので Standard に逃がす。
        /// </summary>
        /// <summary>
        /// FBX に付いてくるマテリアルがテクスチャ未設定のことがある
        /// （SD ユニティちゃんの def_mat がそれ）。その場合の逃げ道として、
        /// アバターのフォルダに置かれた色テクスチャを使う。
        /// 一意に決まらないときは何もしない。
        /// </summary>
        static Texture FallbackTexture(GameObject source)
        {
            var assetPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(AvatarDir + "/")) return null;

            // モデルの置き場から順に外へ広げ、テクスチャが 1 枚だけに定まった時点で採用する。
            // アバターのルートまで広げるとライセンス同梱の画像まで拾うので、
            // 探索はルートの手前で打ち切る。
            var relative = assetPath.Substring(AvatarDir.Length + 1);
            var separator = relative.IndexOf('/');
            if (separator < 0) return null;

            var avatarRoot = AvatarDir + "/" + relative.Substring(0, separator);
            var folder = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');

            while (folder.Length > avatarRoot.Length && folder.StartsWith(avatarRoot))
            {
                var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Distinct()
                    .OrderBy(path => path, System.StringComparer.Ordinal)
                    .ToList();

                if (textures.Count == 1)
                {
                    Debug.Log("[AvatarDemoScene] フォールバックのベーステクスチャ: " + textures[0]);
                    return AssetDatabase.LoadAssetAtPath<Texture>(textures[0]);
                }

                if (textures.Count > 1)
                {
                    Debug.Log($"[AvatarDemoScene] {folder}: テクスチャが {textures.Count} 枚あり一意でないためフォールバックしません。");
                    return null;
                }

                folder = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            }

            return null;
        }

        /// <summary>
        /// 比較の左側（元マテリアル）を描画できる状態にする。
        ///
        /// 差が Illust2D によるものだと言えるように、両側で同じベーステクスチャが
        /// 乗っている状態に揃える。手を入れるのは次の 2 つだけ。
        /// - 参照シェーダーがこのプロジェクトに無い（ピンクになる）
        /// - テクスチャが 1 枚も設定されていない（FBX 付属の既定マテリアル）
        /// </summary>
        static void PrepareOriginalMaterials(GameObject root, Texture fallback)
        {
            var standard = Shader.Find("Standard");
            var missingShader = 0;
            var missingTexture = 0;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null) continue;

                    var shaderMissing = material.shader == null
                                        || material.shader.name == "Hidden/InternalErrorShader";

                    if (shaderMissing)
                    {
                        var substitute = new Material(standard) { name = material.name + "_Standard" };
                        CopyBaseTexture(material, substitute, "_MainTex", "_Color", fallback);
                        materials[i] = substitute;
                        missingShader++;
                        continue;
                    }

                    if (fallback == null || HasAnyTexture(material)) continue;

                    var textured = new Material(material) { name = material.name + "_Textured" };
                    CopyBaseTexture(material, textured, "_MainTex", "_Color", fallback);
                    materials[i] = textured;
                    missingTexture++;
                }

                renderer.sharedMaterials = materials;
            }

            if (missingShader > 0)
            {
                Debug.LogWarning(
                    $"[AvatarDemoScene] 元マテリアル {missingShader} 個のシェーダーが見つからないため Standard に置き換えました。"
                    + " 左側の見た目は元の作者の意図とは異なります。");
            }

            if (missingTexture > 0)
            {
                Debug.Log(
                    $"[AvatarDemoScene] 元マテリアル {missingTexture} 個にテクスチャが無いため、"
                    + "右側と同じベーステクスチャを設定しました。");
            }
        }

        static bool HasAnyTexture(Material material)
        {
            var texEnvs = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
            return FindFirstEntry(texEnvs, HasTexture) != null;
        }

        static int ConvertMaterials(Shader shader, GameObject root, Texture fallback)
        {
            var cache = new Dictionary<Material, Material>();
            var converted = 0;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null) continue;

                    if (!cache.TryGetValue(original, out var replacement))
                    {
                        replacement = CreateIllust2DMaterial(shader, original, fallback);
                        cache[original] = replacement;
                        converted++;
                    }

                    materials[i] = replacement;
                }

                renderer.sharedMaterials = materials;
            }

            return converted;
        }

        static Material CreateIllust2DMaterial(Shader shader, Material original, Texture fallback)
        {
            var material = new Material(shader) { name = "Illust2D_" + original.name };
            CopyBaseTexture(original, material, "_BaseTexture", "_BaseColor", fallback);

            // 透過が要るマテリアルは Cutout に寄せる。Illust2D に半透明モードは無い。
            if (original.renderQueue >= (int)RenderQueue.AlphaTest
                || (original.shader != null && original.shader.name.IndexOf("cutout", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (material.HasProperty("_AlphaMode")) material.SetFloat("_AlphaMode", 1.0f);
            }

            var path = AssetDatabase.GenerateUniqueAssetPath(MaterialDir + "/" + SanitizeName(material.name) + ".mat");
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        /// <summary>
        /// 元マテリアルからベースカラーとテクスチャを引き継ぐ。
        ///
        /// Material.HasProperty / GetTexture はシェーダーのプロパティ定義を引くので、
        /// シェーダーが見つからないマテリアル（このプロジェクトに無い第三者シェーダーを
        /// 参照している場合）では常に空振りする。シリアライズされた値を直接読む。
        /// </summary>
        static void CopyBaseTexture(Material from, Material to, string textureProperty, string colorProperty, Texture fallback)
        {
            var serialized = new SerializedObject(from);

            var texEnvs = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
            var entry = FindSerializedEntry(texEnvs, SourceTextureProperties, HasTexture)
                        ?? FindFirstEntry(texEnvs, HasTexture);

            if (entry != null)
            {
                to.SetTexture(textureProperty, (Texture)entry.FindPropertyRelative("second.m_Texture").objectReferenceValue);
                to.SetTextureScale(textureProperty, entry.FindPropertyRelative("second.m_Scale").vector2Value);
                to.SetTextureOffset(textureProperty, entry.FindPropertyRelative("second.m_Offset").vector2Value);
            }
            else if (fallback != null)
            {
                to.SetTexture(textureProperty, fallback);
            }

            var colors = serialized.FindProperty("m_SavedProperties.m_Colors");
            var color = FindSerializedEntry(colors, SourceColorProperties, null);
            if (color != null)
            {
                to.SetColor(colorProperty, color.FindPropertyRelative("second").colorValue);
            }
        }

        static bool HasTexture(SerializedProperty entry)
        {
            return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue != null;
        }

        static SerializedProperty FindSerializedEntry(
            SerializedProperty array, IReadOnlyList<string> names, System.Func<SerializedProperty, bool> accept)
        {
            if (array == null) return null;

            foreach (var name in names)
            {
                for (var i = 0; i < array.arraySize; i++)
                {
                    var entry = array.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("first").stringValue != name) continue;
                    if (accept != null && !accept(entry)) continue;
                    return entry;
                }
            }

            return null;
        }

        static SerializedProperty FindFirstEntry(SerializedProperty array, System.Func<SerializedProperty, bool> accept)
        {
            if (array == null) return null;

            for (var i = 0; i < array.arraySize; i++)
            {
                var entry = array.GetArrayElementAtIndex(i);
                if (accept == null || accept(entry)) return entry;
            }

            return null;
        }

        static string SanitizeName(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                builder.Append(System.Array.IndexOf(System.IO.Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            }

            return builder.ToString();
        }

        static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy)
                .ToList();

            if (renderers.Count == 0) return new Bounds(Vector3.zero, Vector3.zero);

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static void PlaceCamera(Bounds bounds)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.34f, 0.35f, 0.42f);

            // .ci の確認シーンと同じく、歪みを抑えた狭い画角で引きで撮る。
            const float fieldOfView = 22.0f;
            var aspect = CaptureWidth / (float)CaptureHeight;
            var halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * 1.1f;
            var distance = halfHeight / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);

            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = distance * 3.0f;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -distance);
            camera.transform.rotation = Quaternion.identity;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(RootDir)) AssetDatabase.CreateFolder("Assets", "Demo");
            if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder(RootDir, "Materials");
        }

        static void ReportShaderMessages(Shader shader)
        {
            if (ShaderUtil.GetShaderMessageCount(shader) == 0)
            {
                Debug.Log("[AvatarDemoScene] シェーダーのコンパイルメッセージはありません。");
                return;
            }

            var report = new StringBuilder();
            var errors = 0;
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) errors++;
                report.AppendLine($"{message.severity} {message.file}:{message.line} [{message.platform}] {message.message}");
            }

            if (errors > 0) Fail("コンパイルエラー " + errors + " 件\n" + report);
            else Debug.LogWarning("[AvatarDemoScene] 警告のみ\n" + report);
        }

        static void Fail(string message)
        {
            Failed = true;
            Debug.LogError("[AvatarDemoScene] " + message);
        }

        static string ArgumentValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static int IntArgument(string name, int fallback)
        {
            var raw = ArgumentValue(name);
            return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
        }
    }
}
