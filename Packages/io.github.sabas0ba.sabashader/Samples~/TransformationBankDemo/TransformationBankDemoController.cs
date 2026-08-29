using UnityEngine;

namespace SabaShader.Samples
{
    /// <summary>
    /// Transformation Bankの旧衣装、新衣装、Safety Coverを1つのProgressで表示する。
    /// 生成MaterialはSceneやProjectへ保存しない。
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class TransformationBankDemoController : MonoBehaviour
    {
        public enum BankStyle
        {
            Arcane,
            Cyber,
            Astral,
            Gaia,
            Umbra,
        }

        const string ShaderName = "SabaShader/Illust2D";
        const string Bank = "_io_github_sabas0ba_transformationbank_";
        static readonly int FontColorProperty = Shader.PropertyToID("_Color");

        [SerializeField] BankStyle style;
        [SerializeField, Range(0.0f, 1.0f)] float progress = 0.5f;
        [SerializeField] bool animateInPlayMode = true;
        [SerializeField, Min(0.01f)] float animationSpeed = 0.2f;
        [SerializeField, HideInInspector] Renderer outgoingRenderer;
        [SerializeField, HideInInspector] Renderer incomingRenderer;
        [SerializeField, HideInInspector] Renderer safetyCoverRenderer;
        [SerializeField, HideInInspector] TextMesh progressLabel;

        Material outgoingSource;
        Material incomingSource;
        Material safetyCoverSource;
        Material outgoingPreview;
        Material incomingPreview;
        Material safetyCoverPreview;
        BankStyle appliedStyle;
        bool hasAppliedStyle;

        public void Apply()
        {
            if (outgoingRenderer == null || incomingRenderer == null || safetyCoverRenderer == null)
            {
                return;
            }

            CaptureSource(outgoingRenderer, outgoingPreview, ref outgoingSource);
            CaptureSource(incomingRenderer, incomingPreview, ref incomingSource);
            CaptureSource(safetyCoverRenderer, safetyCoverPreview, ref safetyCoverSource);
            DestroyGenerated(outgoingPreview);
            DestroyGenerated(incomingPreview);
            DestroyGenerated(safetyCoverPreview);

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                ClearRenderers();
                Debug.LogError($"[{nameof(TransformationBankDemoController)}] {ShaderName} が見つかりません。", this);
                return;
            }

            outgoingPreview = CreateRoleMaterial(shader, 1, "Outgoing");
            incomingPreview = CreateRoleMaterial(shader, 0, "Incoming");
            safetyCoverPreview = CreateRoleMaterial(shader, 2, "Safety Cover");
            if (!outgoingPreview.HasProperty(Bank + "Progress"))
            {
                ClearRenderers();
                Debug.LogError(
                    $"[{nameof(TransformationBankDemoController)}] Transformation Bankが有効ではありません。" +
                    "マテリアルInspectorのSelect Modulesから有効にしてください。",
                    this);
                return;
            }

            outgoingRenderer.sharedMaterial = outgoingPreview;
            incomingRenderer.sharedMaterial = incomingPreview;
            safetyCoverRenderer.sharedMaterial = safetyCoverPreview;
            appliedStyle = style;
            hasAppliedStyle = true;
            ApplyProgress();
        }

        void OnEnable()
        {
            StabilizeTextRendering();
            Apply();
        }

        void OnValidate()
        {
            progress = Mathf.Clamp01(progress);
            animationSpeed = Mathf.Max(0.01f, animationSpeed);
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (outgoingPreview == null || incomingPreview == null || safetyCoverPreview == null ||
                !hasAppliedStyle || appliedStyle != style)
            {
                Apply();
                return;
            }

            ApplyProgress();
        }

        void Update()
        {
            if (!Application.isPlaying || !animateInPlayMode)
            {
                return;
            }

            progress = Mathf.PingPong(Time.time * animationSpeed, 1.0f);
            ApplyProgress();
        }

        void OnDisable()
        {
            RestoreRenderer(outgoingRenderer, outgoingPreview, outgoingSource);
            RestoreRenderer(incomingRenderer, incomingPreview, incomingSource);
            RestoreRenderer(safetyCoverRenderer, safetyCoverPreview, safetyCoverSource);
            DestroyGenerated(outgoingPreview);
            DestroyGenerated(incomingPreview);
            DestroyGenerated(safetyCoverPreview);
            outgoingPreview = null;
            incomingPreview = null;
            safetyCoverPreview = null;
            hasAppliedStyle = false;
        }

        void ApplyProgress()
        {
            SetProgress(outgoingPreview);
            SetProgress(incomingPreview);
            SetProgress(safetyCoverPreview);
            if (progressLabel != null)
            {
                progressLabel.text = $"{style}\nProgress {progress:0.00}\n{CoverageLabel(progress)}";
            }
        }

        Material CreateRoleMaterial(Shader shader, int role, string roleName)
        {
            var material = new Material(shader)
            {
                name = $"Transformation Bank Demo / {style} / {roleName}",
                hideFlags = HideFlags.HideAndDontSave,
            };
            ConfigureBase(material, role);
            ConfigureBank(material, role);
            return material;
        }

        void ConfigureBase(Material material, int role)
        {
            var palette = StylePalette(style);
            material.SetColor("_BaseColor", role == 1 ? palette.outgoing : palette.incoming);
            material.SetFloat("_Roughness", style == BankStyle.Cyber ? 0.28f : 0.48f);
            material.SetFloat("_ShadeBorder1", 0.52f);
            material.SetFloat("_ShadeBlur1", 0.14f);
            material.SetInteger("_OutlineEnabled", 0);
            material.SetInteger("_Cull", 2);
        }

        void ConfigureBank(Material material, int role)
        {
            var palette = StylePalette(style);
            material.SetFloat(Bank + "Progress", progress);
            material.SetInteger(Bank + "Role", role);
            material.SetInteger(Bank + "Style", (int)style);
            material.SetVector(Bank + "IncomingOutgoingWindow", new Vector4(0.25f, 0.65f, 0.35f, 0.75f));
            material.SetVector(Bank + "CoverWindow", new Vector4(0.1f, 0.3f, 0.7f, 0.9f));
            material.SetVector(Bank + "Direction", new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
            material.SetVector(Bank + "Bounds", new Vector4(-1.0f, 1.0f, 0.0f, 0.0f));
            material.SetFloat(Bank + "NoiseScale", style == BankStyle.Astral ? 10.0f : 7.0f);
            material.SetFloat(Bank + "Noise", style == BankStyle.Gaia ? 0.58f : 0.36f);
            material.SetFloat(Bank + "EdgeWidth", 0.09f);
            material.SetColor(Bank + "EdgeColor", palette.edge);
            material.SetFloat(Bank + "EdgeEmission", 3.8f);
            material.SetFloat(Bank + "Displacement", style == BankStyle.Cyber ? 0.16f : 0.1f);
            material.SetFloat(Bank + "BlockScale", 8.0f);
            material.SetColor(Bank + "CoverColor", palette.cover);
            material.SetColor(Bank + "PatternColor", palette.pattern);
            material.SetFloat(Bank + "PatternScale", style == BankStyle.Astral ? 8.0f : 6.0f);
            material.SetFloat(Bank + "PatternSpeed", 1.0f);
            material.SetFloat(Bank + "PatternEmission", 3.0f);
        }

        void SetProgress(Material material)
        {
            if (material != null)
            {
                material.SetFloat(Bank + "Progress", progress);
            }
        }

        void ClearRenderers()
        {
            outgoingRenderer.sharedMaterial = null;
            incomingRenderer.sharedMaterial = null;
            safetyCoverRenderer.sharedMaterial = null;
        }

        static void CaptureSource(Renderer renderer, Material preview, ref Material source)
        {
            if (renderer.sharedMaterial != preview)
            {
                source = renderer.sharedMaterial;
            }
        }

        static void RestoreRenderer(Renderer renderer, Material preview, Material source)
        {
            if (renderer != null && renderer.sharedMaterial == preview)
            {
                renderer.sharedMaterial = source;
            }
        }

        static string CoverageLabel(float value)
        {
            if (value < 0.3f)
            {
                return "OLD OUTFIT";
            }

            if (value < 0.7f)
            {
                return "SAFETY COVER";
            }

            return "NEW OUTFIT";
        }

        void StabilizeTextRendering()
        {
            foreach (var textMesh in transform.root.GetComponentsInChildren<TextMesh>(true))
            {
                var renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    continue;
                }

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(FontColorProperty, Color.white);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        static (Color outgoing, Color incoming, Color cover, Color edge, Color pattern) StylePalette(BankStyle value)
        {
            switch (value)
            {
                case BankStyle.Cyber:
                    return (
                        new Color(0.04f, 0.22f, 0.52f, 1.0f),
                        new Color(0.08f, 0.78f, 0.68f, 1.0f),
                        new Color(0.015f, 0.045f, 0.09f, 1.0f),
                        new Color(0.05f, 1.1f, 1.5f, 1.0f),
                        new Color(0.08f, 0.9f, 1.4f, 1.0f));
                case BankStyle.Astral:
                    return (
                        new Color(0.24f, 0.08f, 0.52f, 1.0f),
                        new Color(0.72f, 0.18f, 0.58f, 1.0f),
                        new Color(0.018f, 0.012f, 0.075f, 1.0f),
                        new Color(0.48f, 0.62f, 1.6f, 1.0f),
                        new Color(0.75f, 0.82f, 1.5f, 1.0f));
                case BankStyle.Gaia:
                    return (
                        new Color(0.34f, 0.13f, 0.045f, 1.0f),
                        new Color(0.16f, 0.48f, 0.17f, 1.0f),
                        new Color(0.095f, 0.055f, 0.025f, 1.0f),
                        new Color(1.2f, 0.48f, 0.08f, 1.0f),
                        new Color(1.15f, 0.58f, 0.12f, 1.0f));
                case BankStyle.Umbra:
                    return (
                        new Color(0.16f, 0.12f, 0.25f, 1.0f),
                        new Color(0.36f, 0.08f, 0.42f, 1.0f),
                        new Color(0.012f, 0.008f, 0.022f, 1.0f),
                        new Color(0.62f, 0.12f, 1.3f, 1.0f),
                        new Color(0.72f, 0.18f, 1.35f, 1.0f));
                default:
                    return (
                        new Color(0.28f, 0.08f, 0.48f, 1.0f),
                        new Color(0.06f, 0.56f, 0.78f, 1.0f),
                        new Color(0.025f, 0.025f, 0.09f, 1.0f),
                        new Color(0.12f, 0.82f, 1.5f, 1.0f),
                        new Color(0.82f, 0.22f, 1.25f, 1.0f));
            }
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
