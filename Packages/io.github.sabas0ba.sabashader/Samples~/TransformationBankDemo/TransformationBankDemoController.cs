using System.Collections.Generic;
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

        enum ParticleSilhouette
        {
            ArcaneRune,
            CyberPixel,
            AstralStar,
            GaiaLeaf,
            UmbraWisp,
            FlameTongue,
            Ember,
            ShardTriangle,
            ShardQuad,
            GlitchBar,
            Droplet,
            Bead,
            RiftShard,
            Sparkle,
            MistOrb,
        }

        const string ShaderName = "SabaShader/Illust2D";
        const string Bank = "_io_github_sabas0ba_transformationbank_";
        static readonly int FontColorProperty = Shader.PropertyToID("_Color");
        static readonly Dictionary<ParticleSilhouette, Mesh> ParticleMeshes =
            new Dictionary<ParticleSilhouette, Mesh>();

        [SerializeField] BankStyle style;
        [SerializeField, Range(0.0f, 1.0f)] float progress = 0.5f;
        [SerializeField] bool animateInPlayMode = true;
        [SerializeField, Min(0.01f)] float animationSpeed = 0.2f;
        [SerializeField, Range(0.0f, 4.0f)] float effectIntensity = 1.6f;
        [SerializeField, Range(0.0f, 4.0f)] float particleIntensity = 0.75f;
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
            public readonly ParticleSilhouette Silhouette;

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
                ParticleSilhouette silhouette)
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
                Silhouette = silhouette;
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
            main.startSize = new ParticleSystem.MinMaxCurve(
                profile.Size * particleSize * 0.7f,
                profile.Size * particleSize * 1.15f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            var colorA = accent ? palette.pattern : palette.edge;
            var colorB = accent ? palette.edge : palette.pattern;
            if (style == BankStyle.ManaMist)
            {
                colorA.a = accent ? 0.08f : 0.025f;
                colorB.a = accent ? 0.05f : 0.018f;
            }
            else
            {
                colorA.a = accent ? 0.72f : 0.62f;
                colorB.a = accent ? 0.62f : 0.52f;
            }
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = profile.Gravity;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0.0f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = profile.Shape;
            shape.radius = profile.Radius;
            shape.radiusThickness = style == BankStyle.ManaMist ? 0.35f : 0.05f;
            shape.angle = style == BankStyle.Flame ? 18.0f : 25.0f;

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

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1.0f,
                new AnimationCurve(
                    new Keyframe(0.0f, 0.12f),
                    new Keyframe(0.16f, 1.0f),
                    new Keyframe(0.72f, 0.82f),
                    new Keyframe(1.0f, 0.05f)));

            var rotation = particles.rotationOverLifetime;
            rotation.enabled = style == BankStyle.Shatter || style == BankStyle.Glitch ||
                style == BankStyle.CosmicRift || style == BankStyle.MagicalSparkle;
            rotation.z = new ParticleSystem.MinMaxCurve(-2.4f, 2.4f);

            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            var expectedMeshName = "Transformation Bank Particle / " + profile.Silhouette;
            if (particleRenderer.mesh != null && particleRenderer.mesh.name == expectedMeshName)
            {
                ParticleMeshes[profile.Silhouette] = particleRenderer.mesh;
            }
            else
            {
                particleRenderer.mesh = ParticleMesh(profile.Silhouette);
            }
            particleRenderer.lengthScale = 1.0f;
            particleRenderer.velocityScale = 0.0f;
        }

        void ApplyParticleProgress()
        {
            UpdateParticleSystem(primaryParticles, false, ParticleActivity(style, false, progress));
            UpdateParticleSystem(accentParticles, true, ParticleActivity(style, true, progress));
        }

        void UpdateParticleSystem(ParticleSystem particles, bool accent, float activity)
        {
            if (particles == null)
            {
                return;
            }

            var profile = ParticleProfileFor(style, accent);
            var main = particles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(
                profile.Size * particleSize * 0.7f,
                profile.Size * particleSize * 1.15f);
            var noise = particles.noise;
            noise.strength = profile.Noise * effectIntensity;
            var emission = particles.emission;
            emission.rateOverTime = profile.Rate * particleIntensity * activity * 0.45f;

            if (Application.isPlaying)
            {
                if (activity <= 0.001f && (progress <= 0.01f || progress >= 0.99f))
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    return;
                }
                if (!particles.isPlaying)
                {
                    particles.Play(true);
                }
                return;
            }

            particles.Simulate(0.22f + progress * 0.9f, true, true, true);
            particles.Pause(true);
        }

        static float ParticleActivity(BankStyle value, bool accent, float value01)
        {
            switch (value)
            {
                case BankStyle.Arcane:
                    return ParticleBand(value01, accent ? 0.32f : 0.2f, accent ? 0.62f : 0.48f, accent ? 0.88f : 0.74f);
                case BankStyle.Cyber:
                    return ParticleBand(value01, accent ? 0.18f : 0.28f, accent ? 0.58f : 0.5f, accent ? 0.84f : 0.72f);
                case BankStyle.Astral:
                    return ParticleBand(value01, accent ? 0.08f : 0.18f, accent ? 0.58f : 0.5f, accent ? 0.94f : 0.86f);
                case BankStyle.Gaia:
                    return ParticleBand(value01, accent ? 0.34f : 0.22f, accent ? 0.68f : 0.55f, accent ? 0.92f : 0.85f);
                case BankStyle.Umbra:
                    return ParticleBand(value01, accent ? 0.12f : 0.2f, accent ? 0.55f : 0.48f, accent ? 0.86f : 0.78f);
                case BankStyle.Flame:
                    return ParticleBand(value01, accent ? 0.12f : 0.2f, accent ? 0.55f : 0.5f, accent ? 0.9f : 0.78f);
                case BankStyle.Shatter:
                    return ParticleBand(value01, accent ? 0.46f : 0.28f, accent ? 0.69f : 0.48f, accent ? 0.9f : 0.64f);
                case BankStyle.Glitch:
                    return ParticleBand(value01, accent ? 0.18f : 0.25f, accent ? 0.6f : 0.5f, accent ? 0.84f : 0.72f);
                case BankStyle.Melt:
                    return ParticleBand(value01, accent ? 0.44f : 0.35f, accent ? 0.66f : 0.56f, accent ? 0.84f : 0.76f);
                case BankStyle.CosmicRift:
                    return ParticleBand(value01, accent ? 0.08f : 0.18f, accent ? 0.58f : 0.5f, accent ? 0.94f : 0.82f);
                case BankStyle.MagicalSparkle:
                    return ParticleBand(value01, accent ? 0.08f : 0.18f, accent ? 0.64f : 0.56f, accent ? 0.96f : 0.88f);
                case BankStyle.ManaMist:
                    return ParticleBand(value01, accent ? 0.3f : 0.06f, accent ? 0.72f : 0.48f, accent ? 0.98f : 0.8f);
                default:
                    return ParticleBand(value01, 0.2f, 0.5f, 0.82f);
            }
        }

        static float ParticleBand(float value, float start, float peak, float end)
        {
            var rise = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(start, peak, value));
            var fall = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(peak, end, value));
            return rise * fall;
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
                case BankStyle.Arcane:
                    return accent
                        ? new ParticleProfile(76.0f, 0.055f, 1.0f, 0.08f, 0.0f, 0.22f, 0.12f, 0.82f, 0.18f, ParticleSystemShapeType.Sphere, ParticleSilhouette.Sparkle)
                        : new ParticleProfile(34.0f, 0.16f, 1.5f, 0.04f, 0.0f, 0.12f, 0.08f, 0.76f, 0.16f, ParticleSystemShapeType.Circle, ParticleSilhouette.ArcaneRune);
                case BankStyle.Cyber:
                    return accent
                        ? new ParticleProfile(120.0f, 0.045f, 0.42f, 0.0f, 0.0f, 0.0f, 0.12f, 0.78f, 0.36f, ParticleSystemShapeType.Box, ParticleSilhouette.GlitchBar)
                        : new ParticleProfile(54.0f, 0.14f, 0.78f, 0.02f, 0.0f, 0.08f, 0.16f, 0.72f, 0.24f, ParticleSystemShapeType.Box, ParticleSilhouette.CyberPixel);
                case BankStyle.Astral:
                    return accent
                        ? new ParticleProfile(105.0f, 0.045f, 1.2f, 0.05f, -0.02f, 0.16f, 0.18f, 0.92f, 0.14f, ParticleSystemShapeType.Sphere, ParticleSilhouette.Sparkle)
                        : new ParticleProfile(42.0f, 0.17f, 1.75f, 0.04f, 0.0f, 0.1f, 0.1f, 0.86f, 0.12f, ParticleSystemShapeType.Sphere, ParticleSilhouette.AstralStar);
                case BankStyle.Gaia:
                    return accent
                        ? new ParticleProfile(58.0f, 0.07f, 1.15f, 0.05f, 0.08f, -0.18f, 0.12f, 0.78f, 0.2f, ParticleSystemShapeType.Sphere, ParticleSilhouette.Bead)
                        : new ParticleProfile(38.0f, 0.19f, 1.65f, 0.04f, 0.06f, -0.08f, 0.16f, 0.82f, 0.22f, ParticleSystemShapeType.Sphere, ParticleSilhouette.GaiaLeaf);
                case BankStyle.Umbra:
                    return accent
                        ? new ParticleProfile(68.0f, 0.06f, 1.2f, 0.02f, 0.0f, 0.12f, -0.14f, 0.94f, 0.42f, ParticleSystemShapeType.Sphere, ParticleSilhouette.RiftShard)
                        : new ParticleProfile(32.0f, 0.2f, 1.8f, -0.04f, 0.0f, 0.04f, -0.22f, 1.0f, 0.52f, ParticleSystemShapeType.Sphere, ParticleSilhouette.UmbraWisp);
                case BankStyle.Flame:
                    return accent
                        ? new ParticleProfile(72.0f, 0.04f, 0.72f, 0.28f, -0.1f, 0.7f, 0.18f, 0.72f, 0.24f, ParticleSystemShapeType.Cone, ParticleSilhouette.Ember)
                        : new ParticleProfile(30.0f, 0.17f, 0.72f, 0.12f, -0.05f, 0.52f, 0.05f, 0.66f, 0.32f, ParticleSystemShapeType.Cone, ParticleSilhouette.FlameTongue);
                case BankStyle.Shatter:
                    return accent
                        ? new ParticleProfile(22.0f, 0.065f, 0.72f, -0.05f, 0.02f, 0.0f, -0.72f, 0.94f, 0.08f, ParticleSystemShapeType.Sphere, ParticleSilhouette.ShardQuad)
                        : new ParticleProfile(28.0f, 0.11f, 0.82f, 0.14f, 0.05f, 0.0f, 0.72f, 0.62f, 0.1f, ParticleSystemShapeType.Sphere, ParticleSilhouette.ShardTriangle);
                case BankStyle.Glitch:
                    return accent
                        ? new ParticleProfile(155.0f, 0.045f, 0.3f, 0.0f, 0.0f, 0.0f, 0.14f, 0.82f, 0.7f, ParticleSystemShapeType.Box, ParticleSilhouette.CyberPixel)
                        : new ParticleProfile(102.0f, 0.13f, 0.46f, 0.0f, 0.0f, 0.0f, 0.32f, 0.78f, 0.82f, ParticleSystemShapeType.Box, ParticleSilhouette.GlitchBar);
                case BankStyle.Melt:
                    return accent
                        ? new ParticleProfile(34.0f, 0.04f, 0.58f, 0.0f, 0.32f, -0.62f, 0.07f, 0.64f, 0.12f, ParticleSystemShapeType.Sphere, ParticleSilhouette.Bead)
                        : new ParticleProfile(28.0f, 0.14f, 0.76f, 0.0f, 0.42f, -0.5f, 0.04f, 0.62f, 0.18f, ParticleSystemShapeType.Sphere, ParticleSilhouette.Droplet);
                case BankStyle.CosmicRift:
                    return accent
                        ? new ParticleProfile(84.0f, 0.045f, 1.5f, 0.04f, 0.0f, 0.0f, 0.08f, 1.1f, 0.1f, ParticleSystemShapeType.Circle, ParticleSilhouette.AstralStar)
                        : new ParticleProfile(36.0f, 0.15f, 1.65f, 0.02f, 0.0f, 0.0f, 0.04f, 1.0f, 0.16f, ParticleSystemShapeType.Circle, ParticleSilhouette.RiftShard);
                case BankStyle.MagicalSparkle:
                    return accent
                        ? new ParticleProfile(112.0f, 0.04f, 0.9f, 0.06f, -0.03f, 0.22f, 0.16f, 0.94f, 0.14f, ParticleSystemShapeType.Sphere, ParticleSilhouette.AstralStar)
                        : new ParticleProfile(54.0f, 0.14f, 1.12f, 0.05f, -0.04f, 0.16f, 0.11f, 0.84f, 0.14f, ParticleSystemShapeType.Sphere, ParticleSilhouette.Sparkle);
                case BankStyle.ManaMist:
                    return accent
                        ? new ParticleProfile(20.0f, 0.025f, 0.7f, -0.03f, -0.01f, 0.04f, -0.12f, 1.0f, 0.28f, ParticleSystemShapeType.Sphere, ParticleSilhouette.Sparkle)
                        : new ParticleProfile(12.0f, 0.045f, 0.82f, -0.035f, -0.01f, 0.02f, -0.14f, 0.96f, 0.3f, ParticleSystemShapeType.Sphere, ParticleSilhouette.MistOrb);
                default:
                    return accent
                        ? new ParticleProfile(72.0f, 0.05f, 1.1f, 0.08f, 0.0f, 0.18f, 0.18f, 0.8f, 0.2f, ParticleSystemShapeType.Sphere, ParticleSilhouette.Sparkle)
                        : new ParticleProfile(32.0f, 0.14f, 1.5f, 0.05f, 0.0f, 0.1f, 0.1f, 0.72f, 0.22f, ParticleSystemShapeType.Sphere, ParticleSilhouette.ArcaneRune);
            }
        }

        public static Mesh CreateParticleSilhouetteMesh(string silhouetteName)
        {
            if (!System.Enum.TryParse(silhouetteName, out ParticleSilhouette silhouette))
            {
                throw new System.ArgumentException("Unknown particle silhouette: " + silhouetteName, nameof(silhouetteName));
            }

            ParticleMeshes.Remove(silhouette);
            return ParticleMesh(silhouette);
        }

        static Mesh ParticleMesh(ParticleSilhouette silhouette)
        {
            if (ParticleMeshes.TryGetValue(silhouette, out var cached) && cached != null)
            {
                return cached;
            }

            Vector2[] outline;
            switch (silhouette)
            {
                case ParticleSilhouette.ArcaneRune:
                    outline = new[]
                    {
                        new Vector2(0.0f, -1.0f), new Vector2(0.28f, -0.38f),
                        new Vector2(0.56f, 0.0f), new Vector2(0.28f, 0.38f),
                        new Vector2(0.0f, 1.0f), new Vector2(-0.28f, 0.38f),
                        new Vector2(-0.56f, 0.0f), new Vector2(-0.28f, -0.38f),
                    };
                    break;
                case ParticleSilhouette.CyberPixel:
                    outline = new[]
                    {
                        new Vector2(-0.72f, -0.42f), new Vector2(0.52f, -0.42f),
                        new Vector2(0.72f, -0.2f), new Vector2(0.72f, 0.42f),
                        new Vector2(-0.52f, 0.42f), new Vector2(-0.72f, 0.2f),
                    };
                    break;
                case ParticleSilhouette.AstralStar:
                    outline = RadialOutline(5, 1.0f, 0.38f, -Mathf.PI * 0.5f);
                    break;
                case ParticleSilhouette.GaiaLeaf:
                    outline = new[]
                    {
                        new Vector2(0.0f, -1.0f), new Vector2(0.38f, -0.58f),
                        new Vector2(0.54f, 0.0f), new Vector2(0.32f, 0.58f),
                        new Vector2(0.0f, 1.0f), new Vector2(-0.32f, 0.58f),
                        new Vector2(-0.54f, 0.0f), new Vector2(-0.38f, -0.58f),
                    };
                    break;
                case ParticleSilhouette.UmbraWisp:
                    outline = new[]
                    {
                        new Vector2(0.0f, -1.05f), new Vector2(0.24f, -0.48f),
                        new Vector2(0.48f, -0.08f), new Vector2(0.3f, 0.38f),
                        new Vector2(0.06f, 1.0f), new Vector2(-0.12f, 0.4f),
                        new Vector2(-0.5f, 0.12f), new Vector2(-0.34f, -0.48f),
                    };
                    break;
                case ParticleSilhouette.FlameTongue:
                    outline = new[]
                    {
                        new Vector2(0.0f, -1.0f), new Vector2(0.42f, -0.58f),
                        new Vector2(0.48f, -0.12f), new Vector2(0.28f, 0.36f),
                        new Vector2(0.16f, 1.18f), new Vector2(-0.04f, 0.68f),
                        new Vector2(-0.28f, 0.98f), new Vector2(-0.22f, 0.34f),
                        new Vector2(-0.48f, -0.12f), new Vector2(-0.4f, -0.62f),
                    };
                    break;
                case ParticleSilhouette.Ember:
                    outline = RadialOutline(4, 1.0f, 0.48f, Mathf.PI * 0.25f);
                    break;
                case ParticleSilhouette.ShardTriangle:
                    outline = new[]
                    {
                        new Vector2(-0.68f, -0.72f), new Vector2(0.82f, -0.38f),
                        new Vector2(-0.12f, 1.0f),
                    };
                    break;
                case ParticleSilhouette.ShardQuad:
                    outline = new[]
                    {
                        new Vector2(-0.72f, -0.56f), new Vector2(0.48f, -0.82f),
                        new Vector2(0.82f, 0.48f), new Vector2(-0.36f, 0.94f),
                    };
                    break;
                case ParticleSilhouette.GlitchBar:
                    outline = new[]
                    {
                        new Vector2(-1.0f, -0.3f), new Vector2(-0.18f, -0.3f),
                        new Vector2(-0.18f, -0.52f), new Vector2(0.36f, -0.52f),
                        new Vector2(0.36f, -0.18f), new Vector2(1.0f, -0.18f),
                        new Vector2(1.0f, 0.3f), new Vector2(0.14f, 0.3f),
                        new Vector2(0.14f, 0.52f), new Vector2(-0.42f, 0.52f),
                        new Vector2(-0.42f, 0.18f), new Vector2(-1.0f, 0.18f),
                    };
                    break;
                case ParticleSilhouette.Droplet:
                    outline = new[]
                    {
                        new Vector2(0.0f, -0.95f), new Vector2(0.38f, -0.66f),
                        new Vector2(0.52f, -0.18f), new Vector2(0.42f, 0.28f),
                        new Vector2(0.18f, 0.7f), new Vector2(0.0f, 1.22f),
                        new Vector2(-0.18f, 0.7f), new Vector2(-0.42f, 0.28f),
                        new Vector2(-0.52f, -0.18f), new Vector2(-0.38f, -0.66f),
                    };
                    break;
                case ParticleSilhouette.Bead:
                    outline = CircleOutline(10, 0.08f);
                    break;
                case ParticleSilhouette.RiftShard:
                    outline = new[]
                    {
                        new Vector2(0.0f, -1.25f), new Vector2(0.28f, -0.18f),
                        new Vector2(0.16f, 1.25f), new Vector2(-0.3f, 0.18f),
                    };
                    break;
                case ParticleSilhouette.Sparkle:
                    outline = RadialOutline(4, 1.0f, 0.13f, 0.0f);
                    break;
                case ParticleSilhouette.MistOrb:
                    outline = CircleOutline(20, 0.05f);
                    break;
                default:
                    outline = CircleOutline(8, 0.0f);
                    break;
            }

            var mesh = CreateParticleMesh(silhouette.ToString(), outline);
            ParticleMeshes[silhouette] = mesh;
            return mesh;
        }

        static Vector2[] RadialOutline(int points, float outerRadius, float innerRadius, float rotation)
        {
            var outline = new Vector2[points * 2];
            for (var index = 0; index < outline.Length; index++)
            {
                var radius = index % 2 == 0 ? outerRadius : innerRadius;
                var angle = rotation + Mathf.PI * 2.0f * index / outline.Length;
                outline[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return outline;
        }

        static Vector2[] CircleOutline(int points, float irregularity)
        {
            var outline = new Vector2[points];
            for (var index = 0; index < points; index++)
            {
                var angle = -Mathf.PI * 0.5f + Mathf.PI * 2.0f * index / points;
                var radius = 1.0f + Mathf.Sin(index * 2.37f) * irregularity;
                outline[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return outline;
        }

        static Mesh CreateParticleMesh(string name, Vector2[] outline)
        {
            var vertices = new Vector3[outline.Length + 1];
            var uv = new Vector2[vertices.Length];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (var index = 0; index < outline.Length; index++)
            {
                vertices[index + 1] = new Vector3(outline[index].x, outline[index].y, 0.0f);
                uv[index + 1] = outline[index] * 0.4f + new Vector2(0.5f, 0.5f);
            }

            var triangles = new int[outline.Length * 3];
            for (var index = 0; index < outline.Length; index++)
            {
                var triangle = index * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = (index + 1) % outline.Length + 1;
                triangles[triangle + 2] = index + 1;
            }

            var mesh = new Mesh
            {
                name = "Transformation Bank Particle / " + name,
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                uv = uv,
                triangles = triangles,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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
