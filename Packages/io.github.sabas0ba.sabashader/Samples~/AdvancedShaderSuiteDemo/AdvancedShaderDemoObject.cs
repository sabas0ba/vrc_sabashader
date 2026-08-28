using UnityEngine;

namespace SabaShader.Samples
{
    /// <summary>
    /// Decal、Surface Detail、Spatial Interior、Transition の表示用マテリアルを生成する。
    /// 生成するマテリアルとテクスチャはシーンやプロジェクトへ保存しない。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AdvancedShaderDemoObject : MonoBehaviour
    {
        public enum Feature
        {
            DecalUV,
            DecalProjection,
            SkinDetail,
            FabricDetail,
            SpatialUniverseRift,
            SpatialStarfield,
            SpatialCyberBack,
            SpatialMud,
            UpwardDissolve,
            GlitchSpawn,
            LiquidSolid,
        }

        const string ShaderName = "SabaShader/Illust2D";
        const string Decal = "_io_github_sabas0ba_decal_";
        const string SurfaceDetail = "_io_github_sabas0ba_surfacedetail_";
        const string SpatialInterior = "_io_github_sabas0ba_spatialinterior_";
        const string Transition = "_io_github_sabas0ba_transition_";

        [SerializeField] Feature feature;
        [SerializeField, Range(0.0f, 1.0f)] float progress = 0.5f;
        [SerializeField] bool animateInPlayMode;
        [SerializeField, Min(0.01f)] float animationSpeed = 0.25f;
        [SerializeField, HideInInspector] Mesh sourceMesh;

        Mesh previewMesh;
        Material sourceMaterial;
        Material previewMaterial;
        Texture2D decalTexture;

        public void Apply()
        {
            var meshFilter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            if (sourceMesh == null && meshFilter.sharedMesh != previewMesh)
            {
                sourceMesh = meshFilter.sharedMesh;
            }

            if (sourceMaterial == null && meshRenderer.sharedMaterial != previewMaterial)
            {
                sourceMaterial = meshRenderer.sharedMaterial;
            }

            RebuildMesh(meshFilter);
            RebuildMaterial(meshRenderer);
        }

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                RebuildMaterial(meshRenderer);
            }
        }

        void Update()
        {
            if (!Application.isPlaying || !animateInPlayMode || previewMaterial == null)
            {
                return;
            }

            progress = Mathf.PingPong(Time.time * animationSpeed, 1.0f);
            previewMaterial.SetFloat(Transition + "Progress", progress);
        }

        void OnDisable()
        {
            var meshFilter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshFilter != null && meshFilter.sharedMesh == previewMesh)
            {
                meshFilter.sharedMesh = sourceMesh;
            }

            if (meshRenderer != null && meshRenderer.sharedMaterial == previewMaterial)
            {
                meshRenderer.sharedMaterial = sourceMaterial;
            }

            DestroyGenerated(previewMesh);
            DestroyGenerated(previewMaterial);
            DestroyGenerated(decalTexture);
            previewMesh = null;
            previewMaterial = null;
            decalTexture = null;
        }

        void RebuildMesh(MeshFilter meshFilter)
        {
            if (feature != Feature.SpatialCyberBack)
            {
                if (meshFilter.sharedMesh == previewMesh)
                {
                    meshFilter.sharedMesh = sourceMesh;
                }

                DestroyGenerated(previewMesh);
                previewMesh = null;
                return;
            }

            if (sourceMesh == null)
            {
                return;
            }

            DestroyGenerated(previewMesh);
            previewMesh = Instantiate(sourceMesh);
            previewMesh.name = sourceMesh.name + " (Inward Normals Demo)";
            previewMesh.hideFlags = HideFlags.HideAndDontSave;
            var normals = previewMesh.normals;
            for (var index = 0; index < normals.Length; index++)
            {
                normals[index] = -normals[index];
            }

            previewMesh.normals = normals;
            meshFilter.sharedMesh = previewMesh;
        }

        void RebuildMaterial(MeshRenderer meshRenderer)
        {
            DestroyGenerated(previewMaterial);
            DestroyGenerated(decalTexture);
            previewMaterial = null;
            decalTexture = null;

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                meshRenderer.sharedMaterial = null;
                Debug.LogError($"[{nameof(AdvancedShaderDemoObject)}] {ShaderName} が見つかりません。", this);
                return;
            }

            previewMaterial = new Material(shader)
            {
                name = "Advanced Demo / " + feature,
                hideFlags = HideFlags.HideAndDontSave,
            };
            SetBaseProperties(previewMaterial);

            var requiredProperty = RequiredProperty(feature);
            if (!previewMaterial.HasProperty(requiredProperty))
            {
                meshRenderer.sharedMaterial = previewMaterial;
                Debug.LogError(
                    $"[{nameof(AdvancedShaderDemoObject)}] {requiredProperty} がありません。" +
                    "Shader Core の Project Settings で Illust2D の対象モジュールを有効にしてください。",
                    this);
                return;
            }

            switch (feature)
            {
                case Feature.DecalUV:
                    ConfigureDecal(previewMaterial, false);
                    break;
                case Feature.DecalProjection:
                    ConfigureDecal(previewMaterial, true);
                    break;
                case Feature.SkinDetail:
                    ConfigureSurfaceDetail(previewMaterial, false);
                    break;
                case Feature.FabricDetail:
                    ConfigureSurfaceDetail(previewMaterial, true);
                    break;
                case Feature.SpatialUniverseRift:
                    ConfigureSpatial(previewMaterial, 0, true, false);
                    break;
                case Feature.SpatialStarfield:
                    ConfigureSpatial(previewMaterial, 1, false, false);
                    break;
                case Feature.SpatialCyberBack:
                    ConfigureSpatial(previewMaterial, 2, false, true);
                    break;
                case Feature.SpatialMud:
                    ConfigureSpatial(previewMaterial, 3, false, false);
                    break;
                case Feature.UpwardDissolve:
                    ConfigureTransition(previewMaterial, 0);
                    break;
                case Feature.GlitchSpawn:
                    ConfigureTransition(previewMaterial, 1);
                    break;
                case Feature.LiquidSolid:
                    ConfigureTransition(previewMaterial, 2);
                    break;
            }

            meshRenderer.sharedMaterial = previewMaterial;
        }

        static string RequiredProperty(Feature value)
        {
            switch (value)
            {
                case Feature.DecalUV:
                case Feature.DecalProjection:
                    return Decal + "Amount";
                case Feature.SkinDetail:
                case Feature.FabricDetail:
                    return SurfaceDetail + "Amount";
                case Feature.SpatialUniverseRift:
                case Feature.SpatialStarfield:
                case Feature.SpatialCyberBack:
                case Feature.SpatialMud:
                    return SpatialInterior + "Amount";
                default:
                    return Transition + "Progress";
            }
        }

        static void SetBaseProperties(Material material)
        {
            material.SetColor("_BaseColor", new Color(0.72f, 0.76f, 0.84f, 1.0f));
            material.SetFloat("_Roughness", 0.52f);
            material.SetFloat("_ShadeBorder1", 0.5f);
            material.SetFloat("_ShadeBlur1", 0.12f);
            material.SetInteger("_OutlineEnabled", 0);
            material.SetInteger("_Cull", 2);
        }

        void ConfigureDecal(Material material, bool projection)
        {
            decalTexture = CreateDecalTexture();
            material.SetFloat(Decal + "Amount", 1.0f);
            material.SetTexture(Decal + "Texture", decalTexture);
            material.SetColor(Decal + "Tint", Color.white);
            material.SetInteger(Decal + "Mapping", projection ? 1 : 0);
            material.SetInteger(Decal + "UVChannel", 0);
            material.SetInteger(Decal + "BlendMode", 0);
            material.SetVector(Decal + "ProjectorCenter", new Vector4(0.0f, 0.0f, -0.45f, 0.0f));
            material.SetVector(Decal + "ProjectorRotation", Vector4.zero);
            material.SetVector(Decal + "ProjectorSize", new Vector4(1.35f, 1.35f, 1.3f, 0.0f));
            material.SetFloat(Decal + "AngleFade", -0.1f);
            material.SetFloat(Decal + "EdgeSoftness", 0.08f);
            material.SetColor("_BaseColor", projection
                ? new Color(0.18f, 0.23f, 0.34f, 1.0f)
                : new Color(0.25f, 0.29f, 0.39f, 1.0f));
        }

        static void ConfigureSurfaceDetail(Material material, bool fabric)
        {
            material.SetFloat(SurfaceDetail + "Amount", 1.0f);
            material.SetInteger(SurfaceDetail + "Mode", fabric ? 1 : 0);
            material.SetFloat(SurfaceDetail + "Scale", fabric ? 32.0f : 42.0f);
            material.SetFloat(SurfaceDetail + "AlbedoVariation", fabric ? 0.18f : 0.12f);
            material.SetFloat(SurfaceDetail + "NormalStrength", fabric ? 0.75f : 0.55f);
            material.SetFloat(SurfaceDetail + "RoughnessVariation", fabric ? 0.52f : 0.36f);
            material.SetFloat(SurfaceDetail + "Pore", 1.15f);
            material.SetFloat(SurfaceDetail + "Weave", 1.35f);
            material.SetFloat(SurfaceDetail + "Sheen", fabric ? 0.75f : 0.12f);
            material.SetColor(SurfaceDetail + "SheenColor", new Color(1.0f, 0.72f, 0.48f, 1.0f));
            material.SetColor("_BaseColor", fabric
                ? new Color(0.16f, 0.31f, 0.58f, 1.0f)
                : new Color(0.78f, 0.46f, 0.36f, 1.0f));
        }

        static void ConfigureSpatial(Material material, int preset, bool rift, bool backFace)
        {
            material.SetFloat(SpatialInterior + "Amount", 1.0f);
            material.SetInteger(SpatialInterior + "Preset", preset);
            material.SetInteger(SpatialInterior + "Side", backFace ? 1 : 0);
            material.SetInteger(SpatialInterior + "Region", rift ? 1 : 0);
            material.SetColor(SpatialInterior + "ColorA", new Color(0.01f, 0.018f, 0.08f, 1.0f));
            material.SetColor(SpatialInterior + "ColorB", new Color(0.55f, 0.045f, 0.72f, 1.0f));
            material.SetFloat(SpatialInterior + "Emission", 2.8f);
            material.SetFloat(SpatialInterior + "Scale", 7.0f);
            material.SetFloat(SpatialInterior + "Depth", 2.6f);
            material.SetFloat(SpatialInterior + "Parallax", 1.0f);
            material.SetFloat(SpatialInterior + "StarDensity", 0.65f);
            material.SetFloat(SpatialInterior + "StarSize", 0.26f);
            material.SetFloat(SpatialInterior + "Nebula", 1.2f);
            material.SetFloat(SpatialInterior + "NebulaScale", 0.65f);
            material.SetFloat(SpatialInterior + "TimeScale", 0.0f);
            material.SetVector(SpatialInterior + "RiftCenter", new Vector4(0.5f, 0.5f, 0.0f, 0.0f));
            material.SetVector(SpatialInterior + "RiftSize", new Vector4(0.92f, 0.72f, 0.0f, 0.0f));
            material.SetFloat(SpatialInterior + "RiftNoise", 0.32f);
            material.SetFloat(SpatialInterior + "EdgeWidth", 0.09f);
            material.SetColor(SpatialInterior + "EdgeColor", new Color(0.08f, 0.75f, 1.35f, 1.0f));
            material.SetColor("_BaseColor", new Color(0.06f, 0.075f, 0.12f, 1.0f));
            if (preset == 1)
            {
                material.SetFloat(SpatialInterior + "StarDensity", 1.0f);
                material.SetFloat(SpatialInterior + "StarSize", 0.38f);
                material.SetFloat(SpatialInterior + "Nebula", 0.55f);
                material.SetFloat(SpatialInterior + "Emission", 3.2f);
            }
            else if (preset == 2)
            {
                material.SetFloat(SpatialInterior + "Scale", 5.5f);
                material.SetFloat(SpatialInterior + "Emission", 3.4f);
            }
            else if (preset == 3)
            {
                material.SetFloat(SpatialInterior + "Scale", 4.0f);
                material.SetFloat(SpatialInterior + "NebulaScale", 0.8f);
                material.SetFloat(SpatialInterior + "Nebula", 1.45f);
                material.SetFloat(SpatialInterior + "Emission", 2.4f);
            }

            if (backFace)
            {
                // Demo meshの法線を内向きにして、Side Backを外側から観察する。
                // 実利用で表裏を同時表示する場合はCull Offを使用する。
                material.SetInteger("_Cull", 2);
                material.SetColor(SpatialInterior + "ColorA", new Color(0.05f, 0.1f, 0.42f, 1.0f));
                material.SetColor(SpatialInterior + "ColorB", new Color(0.82f, 0.12f, 1.1f, 1.0f));
                material.SetFloat(SpatialInterior + "Emission", 4.0f);
                material.SetFloat(SpatialInterior + "Nebula", 1.8f);
            }
        }

        void ConfigureTransition(Material material, int mode)
        {
            material.SetFloat(Transition + "Progress", progress);
            material.SetInteger(Transition + "Mode", mode);
            material.SetVector(Transition + "Direction", new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
            material.SetVector(Transition + "Bounds", new Vector4(-0.55f, 0.55f, 0.0f, 0.0f));
            material.SetFloat(Transition + "NoiseScale", 8.0f);
            material.SetFloat(Transition + "Noise", 0.42f);
            material.SetFloat(Transition + "EdgeWidth", 0.11f);
            material.SetColor(Transition + "EdgeColor", new Color(0.08f, 0.85f, 1.4f, 1.0f));
            material.SetFloat(Transition + "EdgeEmission", 3.0f);
            material.SetFloat(Transition + "Displacement", mode == 1 ? 0.32f : 0.22f);
            material.SetFloat(Transition + "BlockScale", 7.0f);
            material.SetFloat(Transition + "LiquidAmplitude", 0.16f);
            material.SetFloat(Transition + "LiquidFrequency", 6.5f);
            material.SetFloat(Transition + "LiquidSpeed", 0.0f);
            material.SetColor(Transition + "LiquidTint", new Color(0.12f, 0.55f, 1.0f, 0.75f));
            material.SetColor("_BaseColor", mode == 2
                ? new Color(0.42f, 0.72f, 0.9f, 1.0f)
                : new Color(0.65f, 0.34f, 0.78f, 1.0f));
        }

        static Texture2D CreateDecalTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                name = "Advanced Demo Decal",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var point = new Vector2(
                        (x + 0.5f) / size * 2.0f - 1.0f,
                        (y + 0.5f) / size * 2.0f - 1.0f);
                    var radius = point.magnitude;
                    var alpha = 1.0f - Mathf.SmoothStep(0.72f, 0.96f, radius);
                    var angle = Mathf.Atan2(point.y, point.x);
                    var stripe = Mathf.Sin(angle * 6.0f + radius * 18.0f) * 0.5f + 0.5f;
                    var inner = 1.0f - Mathf.SmoothStep(0.0f, 0.62f, radius);
                    var cyan = new Color(0.05f, 0.92f, 1.0f, alpha);
                    var magenta = new Color(1.0f, 0.12f, 0.62f, alpha);
                    pixels[y * size + x] = Color.Lerp(magenta, cyan, Mathf.Lerp(stripe, inner, 0.35f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static void DestroyGenerated(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
