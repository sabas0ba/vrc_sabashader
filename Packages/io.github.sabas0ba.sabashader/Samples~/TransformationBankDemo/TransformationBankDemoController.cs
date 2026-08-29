using UnityEngine;

namespace SabaShader.Samples
{
    /// <summary>
    /// Transformation Bankの旧衣装と新衣装を1つのProgressで表示し、Particleで補助する。
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
            Flame,
            Shatter,
            Glitch,
            Melt,
            CosmicRift,
            MagicalSparkle,
            ManaMist,
        }

        const string ShaderName = "SabaShader/Illust2D";
        const string Bank = "_io_github_sabas0ba_transformationbank_";
        static readonly int FontColorProperty = Shader.PropertyToID("_Color");

        [SerializeField] BankStyle style;
        [SerializeField, Range(0.0f, 1.0f)] float progress = 0.5f;
        [SerializeField] bool animateInPlayMode = true;
        [SerializeField, Min(0.01f)] float animationSpeed = 0.2f;
        [SerializeField, Range(0.0f, 4.0f)] float effectIntensity = 1.6f;
        [SerializeField, Range(0.0f, 4.0f)] float particleIntensity = 1.4f;
        [SerializeField, Range(0.1f, 3.0f)] float particleSize = 1.0f;
        [SerializeField, HideInInspector] Renderer outgoingRenderer;
        [SerializeField, HideInInspector] Renderer incomingRenderer;
        [SerializeField, HideInInspector] ParticleSystem primaryParticles;
        [SerializeField, HideInInspector] ParticleSystem accentParticles;
        [SerializeField, HideInInspector] TextMesh progressLabel;

        Material outgoingSource;
        Material incomingSource;
        Material outgoingPreview;
        Material incomingPreview;
        BankStyle appliedStyle;
        bool hasAppliedStyle;

        readonly struct ParticleProfile
        {
            public readonly float Rate;
            public readonly float Size;
            public readonly float Lifetime;
            public readonly float Speed;
            public readonly float Gravity;
            public readonly float Vertical;
            public readonly float Radial;
            public readonly float Radius;
            public readonly float Noise;
            public readonly ParticleSystemShapeType Shape;
            public readonly ParticleSystemRenderMode RenderMode;

            public ParticleProfile(
                float rate,
                float size,
                float lifetime,
                float speed,
                float gravity,
                float vertical,
                float radial,
                float radius,
                float noise,
                ParticleSystemShapeType shape,
                ParticleSystemRenderMode renderMode)
            {
                Rate = rate;
                Size = size;
                Lifetime = lifetime;
                Speed = speed;
                Gravity = gravity;
                Vertical = vertical;
                Radial = radial;
                Radius = radius;
                Noise = noise;
                Shape = shape;
                RenderMode = renderMode;
            }
        }

        public void Apply()
        {
            if (outgoingRenderer == null || incomingRenderer == null)
            {
                return;
            }

            CaptureSource(outgoingRenderer, outgoingPreview, ref outgoingSource);
            CaptureSource(incomingRenderer, incomingPreview, ref incomingSource);
            DestroyGenerated(outgoingPreview);
            DestroyGenerated(incomingPreview);

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                ClearRenderers();
                Debug.LogError($"[{nameof(TransformationBankDemoController)}] {ShaderName} が見つかりません。", this);
                return;
            }

            outgoingPreview = CreateRoleMaterial(shader, 1, "Outgoing");
            incomingPreview = CreateRoleMaterial(shader, 0, "Incoming");
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
            ConfigureParticles();
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
            effectIntensity = Mathf.Clamp(effectIntensity, 0.0f, 4.0f);
            particleIntensity = Mathf.Clamp(particleIntensity, 0.0f, 4.0f);
            particleSize = Mathf.Clamp(particleSize, 0.1f, 3.0f);
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (outgoingPreview == null || incomingPreview == null ||
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
            DestroyGenerated(outgoingPreview);
            DestroyGenerated(incomingPreview);
            outgoingPreview = null;
            incomingPreview = null;
            StopParticles(primaryParticles);
            StopParticles(accentParticles);
            hasAppliedStyle = false;
        }

        void ApplyProgress()
        {
            SetProgress(outgoingPreview);
            SetProgress(incomingPreview);
            ApplyParticleProgress();
            if (progressLabel != null)
            {
                progressLabel.text = $"{StyleLabel(style)}\nProgress {progress:0.00}\n{CoverageLabel(progress)}";
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
            var roughness = style == BankStyle.Cyber || style == BankStyle.Glitch ? 0.28f : 0.48f;
            if (style == BankStyle.Melt)
            {
                roughness = 0.18f;
            }
            material.SetFloat("_Roughness", roughness);
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
            material.SetFloat(Bank + "EffectIntensity", effectIntensity);
            material.SetVector(Bank + "IncomingOutgoingWindow", new Vector4(0.25f, 0.65f, 0.35f, 0.75f));
            material.SetVector(Bank + "Direction", new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
            material.SetVector(Bank + "Bounds", new Vector4(-1.0f, 1.0f, 0.0f, 0.0f));
            var noiseScale = style == BankStyle.Astral ? 10.0f : 7.0f;
            var noise = style == BankStyle.Gaia ? 0.58f : 0.36f;
            var edgeWidth = 0.09f;
            var edgeEmission = 3.8f;
            var displacement = style == BankStyle.Cyber ? 0.16f : 0.1f;
            var blockScale = 8.0f;
            var patternScale = style == BankStyle.Astral ? 8.0f : 6.0f;
            var patternSpeed = 1.0f;
            var patternEmission = 3.0f;
            switch (style)
            {
                case BankStyle.Flame:
                    noiseScale = 4.5f;
                    noise = 0.78f;
                    edgeWidth = 0.13f;
                    edgeEmission = 5.5f;
                    displacement = 0.24f;
                    patternScale = 4.2f;
                    patternSpeed = 1.8f;
                    patternEmission = 5.2f;
                    break;
                case BankStyle.Shatter:
                    noiseScale = 6.0f;
                    noise = 0.46f;
                    edgeWidth = 0.07f;
                    edgeEmission = 4.2f;
                    displacement = 0.46f;
                    blockScale = 4.2f;
                    patternScale = 7.0f;
                    patternSpeed = 0.55f;
                    patternEmission = 3.8f;
                    break;
                case BankStyle.Glitch:
                    noiseScale = 12.0f;
                    noise = 0.82f;
                    edgeWidth = 0.065f;
                    edgeEmission = 5.0f;
                    displacement = 0.28f;
                    blockScale = 10.0f;
                    patternScale = 12.0f;
                    patternSpeed = 2.2f;
                    patternEmission = 4.8f;
                    break;
                case BankStyle.Melt:
                    noiseScale = 5.0f;
                    noise = 0.72f;
                    edgeWidth = 0.11f;
                    edgeEmission = 3.4f;
                    displacement = 0.3f;
                    blockScale = 6.0f;
                    patternScale = 5.0f;
                    patternSpeed = 0.7f;
                    patternEmission = 2.8f;
                    break;
                case BankStyle.CosmicRift:
                    noiseScale = 9.0f;
                    noise = 0.64f;
                    edgeWidth = 0.12f;
                    edgeEmission = 6.0f;
                    displacement = 0.34f;
                    blockScale = 7.0f;
                    patternScale = 9.0f;
                    patternSpeed = 0.65f;
                    patternEmission = 6.2f;
                    break;
                case BankStyle.MagicalSparkle:
                    noiseScale = 7.5f;
                    noise = 0.48f;
                    edgeWidth = 0.1f;
                    edgeEmission = 6.5f;
                    displacement = 0.3f;
                    blockScale = 9.0f;
                    patternScale = 11.0f;
                    patternSpeed = 1.6f;
                    patternEmission = 7.0f;
                    break;
                case BankStyle.ManaMist:
                    noiseScale = 5.5f;
                    noise = 0.82f;
                    edgeWidth = 0.14f;
                    edgeEmission = 4.8f;
                    displacement = 0.42f;
                    blockScale = 6.0f;
                    patternScale = 5.5f;
                    patternSpeed = 0.55f;
                    patternEmission = 4.6f;
                    break;
            }
            material.SetFloat(Bank + "NoiseScale", noiseScale);
            material.SetFloat(Bank + "Noise", noise);
            material.SetFloat(Bank + "EdgeWidth", edgeWidth);
            material.SetColor(Bank + "EdgeColor", palette.edge);
            material.SetFloat(Bank + "EdgeEmission", edgeEmission);
            material.SetFloat(Bank + "Displacement", displacement);
            material.SetFloat(Bank + "BlockScale", blockScale);
            material.SetColor(Bank + "PatternColor", palette.pattern);
            material.SetFloat(Bank + "PatternScale", patternScale);
            material.SetFloat(Bank + "PatternSpeed", patternSpeed);
            material.SetFloat(Bank + "PatternEmission", patternEmission);
        }

        void SetProgress(Material material)
        {
            if (material != null)
            {
                material.SetFloat(Bank + "Progress", progress);
                material.SetFloat(Bank + "EffectIntensity", effectIntensity);
            }
        }

        void ConfigureParticles()
        {
            ConfigureParticleSystem(primaryParticles, false);
            ConfigureParticleSystem(accentParticles, true);
            ApplyParticleProgress();
        }

        void ConfigureParticleSystem(ParticleSystem particles, bool accent)
        {
            if (particles == null)
            {
                return;
            }

            var profile = ParticleProfileFor(style, accent);
            var palette = StylePalette(style);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = accent ? 768 : 512;
            main.startLifetime = profile.Lifetime;
            main.startSpeed = profile.Speed;
            main.startSize = profile.Size * particleSize;
            main.startColor = new ParticleSystem.MinMaxGradient(
                accent ? palette.pattern : palette.edge,
                accent ? palette.edge : palette.pattern);
            main.gravityModifier = profile.Gravity;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0.0f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = profile.Shape;
            shape.radius = profile.Radius;
            shape.radiusThickness = style == BankStyle.CosmicRift ? 0.08f : 1.0f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = Mathf.Abs(profile.Vertical) > 0.001f || Mathf.Abs(profile.Radial) > 0.001f;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = profile.Vertical;
            velocity.radial = profile.Radial;

            var noise = particles.noise;
            noise.enabled = profile.Noise > 0.001f;
            noise.strength = profile.Noise * effectIntensity;
            noise.frequency = 0.65f;
            noise.scrollSpeed = 0.45f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(accent ? palette.pattern : palette.edge, 1.0f),
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.15f),
                    new GradientAlphaKey(0.0f, 1.0f),
                });
            colorOverLifetime.color = gradient;

            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = profile.RenderMode;
            particleRenderer.lengthScale = style == BankStyle.Flame || style == BankStyle.Melt ? 3.5f : 1.0f;
            particleRenderer.velocityScale = 0.25f;
        }

        void ApplyParticleProgress()
        {
            var activity = 4.0f * progress * (1.0f - progress);
            UpdateParticleSystem(primaryParticles, false, activity);
            UpdateParticleSystem(accentParticles, true, activity);
        }

        void UpdateParticleSystem(ParticleSystem particles, bool accent, float activity)
        {
            if (particles == null)
            {
                return;
            }

            var profile = ParticleProfileFor(style, accent);
            var main = particles.main;
            main.startSize = profile.Size * particleSize;
            var noise = particles.noise;
            noise.strength = profile.Noise * effectIntensity;
            var emission = particles.emission;
            emission.rateOverTime = profile.Rate * particleIntensity * activity;

            if (Application.isPlaying)
            {
                if (!particles.isPlaying)
                {
                    particles.Play(true);
                }
                return;
            }

            particles.Simulate(0.35f + progress * 1.8f, true, true, true);
            particles.Pause(true);
        }

        static void StopParticles(ParticleSystem particles)
        {
            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        static ParticleProfile ParticleProfileFor(BankStyle value, bool accent)
        {
            switch (value)
            {
                case BankStyle.Flame:
                    return accent
                        ? new ParticleProfile(120.0f, 0.045f, 1.1f, 0.4f, -0.15f, 1.1f, 0.2f, 0.72f, 0.22f, ParticleSystemShapeType.Cone, ParticleSystemRenderMode.Billboard)
                        : new ParticleProfile(52.0f, 0.24f, 0.9f, 0.2f, -0.1f, 0.75f, 0.05f, 0.66f, 0.35f, ParticleSystemShapeType.Cone, ParticleSystemRenderMode.Stretch);
                case BankStyle.Shatter:
                    return accent
                        ? new ParticleProfile(45.0f, 0.09f, 1.2f, 0.4f, 0.05f, 0.0f, 1.3f, 0.68f, 0.08f, ParticleSystemShapeType.Sphere, ParticleSystemRenderMode.Mesh)
                        : new ParticleProfile(72.0f, 0.17f, 1.5f, 0.25f, 0.08f, 0.0f, 1.05f, 0.62f, 0.12f, ParticleSystemShapeType.Sphere, ParticleSystemRenderMode.Mesh);
                case BankStyle.Glitch:
                    return accent
                        ? new ParticleProfile(150.0f, 0.035f, 0.28f, 0.0f, 0.0f, 0.0f, 0.1f, 0.8f, 0.65f, ParticleSystemShapeType.Box, ParticleSystemRenderMode.Mesh)
                        : new ParticleProfile(95.0f, 0.11f, 0.42f, 0.0f, 0.0f, 0.0f, 0.28f, 0.75f, 0.8f, ParticleSystemShapeType.Box, ParticleSystemRenderMode.Mesh);
                case BankStyle.Melt:
                    return accent
                        ? new ParticleProfile(88.0f, 0.045f, 0.8f, 0.0f, 0.35f, -0.9f, 0.12f, 0.64f, 0.18f, ParticleSystemShapeType.Sphere, ParticleSystemRenderMode.Stretch)
                        : new ParticleProfile(48.0f, 0.14f, 1.15f, 0.0f, 0.45f, -0.7f, 0.05f, 0.62f, 0.22f, ParticleSystemShapeType.Sphere, ParticleSystemRenderMode.Stretch);
                case BankStyle.CosmicRift:
                    return accent
                        ? new ParticleProfile(110.0f, 0.04f, 1.8f, 0.05f, 0.0f, 0.0f, 0.08f, 1.15f, 0.12f, ParticleSystemShapeType.Circle, ParticleSystemRenderMode.Billboard)
                        : new ParticleProfile(44.0f, 0.16f, 2.1f, 0.03f, 0.0f, 0.0f, 0.04f, 1.0f, 0.18f, ParticleSystemShapeType.Circle, ParticleSystemRenderMode.Billboard);
                case BankStyle.MagicalSparkle:
                    return accent
                        ? new ParticleProfile(150.0f, 0.04f, 1.0f, 0.1f, -0.05f, 0.28f, 0.18f, 0.95f, 0.16f, ParticleSystemShapeType.Sphere, ParticleSystemRenderMode.Billboard)
                        : new ParticleProfile(64.0f, 0.14f, 1.35f, 0.08f, -0.08f, 0.2f, 0.12f, 0.82f, 0.14f, ParticleSystemShapeType.Sphere, ParticleSystemRenderMode.Billboard);
                case BankStyle.ManaMist:
                    return accent
                        ? new ParticleProfile(90.0f, 0.08f, 1.8f, -0.08f, -0.02f, 0.08f, -0.35f, 1.45f, 0.55f, ParticleSystemShapeType.Sphere, ParticleSystemRenderMode.Billboard)
                        : new ParticleProfile(36.0f, 0.32f, 2.4f, -0.12f, -0.03f, 0.05f, -0.45f, 1.35f, 0.7f, ParticleSystemShapeType.Sphere, ParticleSystemRenderMode.Billboard);
                default:
                    return accent
                        ? new ParticleProfile(72.0f, 0.045f, 1.1f, 0.08f, 0.0f, 0.18f, 0.18f, 0.8f, 0.2f, ParticleSystemShapeType.Sphere, ParticleSystemRenderMode.Billboard)
                        : new ParticleProfile(32.0f, 0.12f, 1.5f, 0.05f, 0.0f, 0.1f, 0.1f, 0.72f, 0.22f, ParticleSystemShapeType.Sphere, ParticleSystemRenderMode.Billboard);
            }
        }

        void ClearRenderers()
        {
            outgoingRenderer.sharedMaterial = null;
            incomingRenderer.sharedMaterial = null;
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
            if (value < 0.35f)
            {
                return "OLD / CAPSULE";
            }

            if (value < 0.65f)
            {
                return "CROSS TRANSITION";
            }

            return "NEW / CYLINDER";
        }

        static string StyleLabel(BankStyle value)
        {
            switch (value)
            {
                case BankStyle.CosmicRift:
                    return "Cosmic Rift";
                case BankStyle.MagicalSparkle:
                    return "Magical Sparkle";
                case BankStyle.ManaMist:
                    return "Mana Mist";
                default:
                    return value.ToString();
            }
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

        static (Color outgoing, Color incoming, Color edge, Color pattern) StylePalette(BankStyle value)
        {
            switch (value)
            {
                case BankStyle.Cyber:
                    return (
                        new Color(0.04f, 0.22f, 0.52f, 1.0f),
                        new Color(0.08f, 0.78f, 0.68f, 1.0f),
                        new Color(0.05f, 1.1f, 1.5f, 1.0f),
                        new Color(0.08f, 0.9f, 1.4f, 1.0f));
                case BankStyle.Astral:
                    return (
                        new Color(0.24f, 0.08f, 0.52f, 1.0f),
                        new Color(0.72f, 0.18f, 0.58f, 1.0f),
                        new Color(0.48f, 0.62f, 1.6f, 1.0f),
                        new Color(0.75f, 0.82f, 1.5f, 1.0f));
                case BankStyle.Gaia:
                    return (
                        new Color(0.34f, 0.13f, 0.045f, 1.0f),
                        new Color(0.16f, 0.48f, 0.17f, 1.0f),
                        new Color(1.2f, 0.48f, 0.08f, 1.0f),
                        new Color(1.15f, 0.58f, 0.12f, 1.0f));
                case BankStyle.Umbra:
                    return (
                        new Color(0.16f, 0.12f, 0.25f, 1.0f),
                        new Color(0.36f, 0.08f, 0.42f, 1.0f),
                        new Color(0.62f, 0.12f, 1.3f, 1.0f),
                        new Color(0.72f, 0.18f, 1.35f, 1.0f));
                case BankStyle.Flame:
                    return (
                        new Color(0.34f, 0.025f, 0.008f, 1.0f),
                        new Color(0.92f, 0.25f, 0.025f, 1.0f),
                        new Color(2.0f, 0.28f, 0.015f, 1.0f),
                        new Color(2.2f, 0.75f, 0.06f, 1.0f));
                case BankStyle.Shatter:
                    return (
                        new Color(0.09f, 0.18f, 0.3f, 1.0f),
                        new Color(0.5f, 0.78f, 0.92f, 1.0f),
                        new Color(0.65f, 1.2f, 1.7f, 1.0f),
                        new Color(0.8f, 1.4f, 1.8f, 1.0f));
                case BankStyle.Glitch:
                    return (
                        new Color(0.42f, 0.015f, 0.38f, 1.0f),
                        new Color(0.025f, 0.62f, 0.48f, 1.0f),
                        new Color(1.7f, 0.05f, 1.3f, 1.0f),
                        new Color(0.05f, 1.8f, 0.7f, 1.0f));
                case BankStyle.Melt:
                    return (
                        new Color(0.035f, 0.16f, 0.38f, 1.0f),
                        new Color(0.15f, 0.68f, 0.64f, 1.0f),
                        new Color(0.08f, 1.0f, 1.6f, 1.0f),
                        new Color(0.08f, 0.72f, 1.5f, 1.0f));
                case BankStyle.CosmicRift:
                    return (
                        new Color(0.035f, 0.02f, 0.11f, 1.0f),
                        new Color(0.18f, 0.08f, 0.48f, 1.0f),
                        new Color(0.35f, 0.55f, 2.0f, 1.0f),
                        new Color(0.9f, 0.75f, 2.1f, 1.0f));
                case BankStyle.MagicalSparkle:
                    return (
                        new Color(0.7f, 0.12f, 0.42f, 1.0f),
                        new Color(0.95f, 0.48f, 0.82f, 1.0f),
                        new Color(2.2f, 0.55f, 1.45f, 1.0f),
                        new Color(2.3f, 1.6f, 0.7f, 1.0f));
                case BankStyle.ManaMist:
                    return (
                        new Color(0.055f, 0.18f, 0.2f, 1.0f),
                        new Color(0.18f, 0.62f, 0.52f, 1.0f),
                        new Color(0.25f, 1.5f, 1.15f, 1.0f),
                        new Color(0.5f, 1.2f, 1.8f, 1.0f));
                default:
                    return (
                        new Color(0.28f, 0.08f, 0.48f, 1.0f),
                        new Color(0.06f, 0.56f, 0.78f, 1.0f),
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
