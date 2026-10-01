using UnityEngine;

namespace SabaShader.Samples
{
    /// <summary>
    /// Mochi Skinの4接触点を、VRCSDKに依存せずWorld展示シーンで確認する。
    /// 生成するmesh、material、textureはシーンやプロジェクトへ保存しない。
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MochiSkinWorldDemoObject : MonoBehaviour
    {
        public enum SkinFinish
        {
            Dry,
            Glossy,
        }

        readonly struct ContactSpec
        {
            public ContactSpec(
                string name,
                PrimitiveType primitive,
                Vector4 point,
                Vector4 shape,
                Vector3 visualScale,
                Vector3 visualRotation,
                float supportDistance)
            {
                Name = name;
                Primitive = primitive;
                Point = point;
                Shape = shape;
                VisualScale = visualScale;
                VisualRotation = visualRotation;
                SupportDistance = supportDistance;
            }

            public string Name { get; }
            public PrimitiveType Primitive { get; }
            public Vector4 Point { get; }
            public Vector4 Shape { get; }
            public Vector3 VisualScale { get; }
            public Vector3 VisualRotation { get; }
            public float SupportDistance { get; }
        }

        const string PreferredShaderName = "NonToon";
        const string FallbackShaderName = "SabaShader/Illust2D";
        const string MochiSkin = "_io_github_sabas0ba_mochiskin_";
        const int HorizontalSegments = 72;
        const int VerticalSegments = 54;
        const float PatchWidth = 1.65f;
        const float PatchHeight = 1.25f;
        const float ApproachGap = 0.16f;
        const float MaximumPenetration = 0.055f;

        static readonly ContactSpec[] Contacts =
        {
            new ContactSpec(
                "Sphere Probe",
                PrimitiveType.Sphere,
                new Vector4(0.27f, 0.69f, 0.13f, 0.17f),
                new Vector4(0.0f, 2.0f, 0.17f, 0.0f),
                new Vector3(0.15f, 0.15f, 0.15f),
                Vector3.zero,
                0.075f),
            new ContactSpec(
                "Cylinder Probe",
                PrimitiveType.Cylinder,
                new Vector4(0.70f, 0.68f, 0.09f, 0.22f),
                new Vector4(32.0f, 2.8f, 0.41f, 0.0f),
                new Vector3(0.12f, 0.25f, 0.12f),
                new Vector3(11.0f, -15.0f, 32.0f),
                0.067f),
            new ContactSpec(
                "Plate Probe",
                PrimitiveType.Cube,
                new Vector4(0.29f, 0.30f, 0.17f, 0.12f),
                new Vector4(-24.0f, 9.0f, 0.67f, 0.0f),
                new Vector3(0.34f, 0.20f, 0.06f),
                new Vector3(8.0f, 13.0f, -24.0f),
                0.046f),
            new ContactSpec(
                "Capsule Probe",
                PrimitiveType.Capsule,
                new Vector4(0.70f, 0.30f, 0.10f, 0.21f),
                new Vector4(-52.0f, 3.5f, 0.89f, 0.0f),
                new Vector3(0.12f, 0.24f, 0.12f),
                new Vector3(-11.0f, 18.0f, -52.0f),
                0.069f),
        };

        [SerializeField] SkinFinish skinFinish;
        [SerializeField, Range(0.0f, 1.0f)] float pressure0;
        [SerializeField, Range(0.0f, 1.0f)] float pressure1;
        [SerializeField, Range(0.0f, 1.0f)] float pressure2;
        [SerializeField, Range(0.0f, 1.0f)] float pressure3;
        [SerializeField, Range(0.0f, 0.03f)] float depth = 0.028f;
        [SerializeField, Range(0.0f, 1.0f)] float compliance = 1.0f;
        [SerializeField] Texture2D complianceMask;
        [SerializeField, Range(0.0f, 1.0f)] float outerBulge = 0.22f;
        [SerializeField, Range(0.15f, 0.85f)] float indentSpread = 0.60f;
        [SerializeField, Range(0.0f, 1.0f)] float edgeSoftness = 0.78f;
        [SerializeField, Range(0.0f, 0.3f)] float contourIrregularity = 0.075f;
        [SerializeField, Range(1.0f, 10.0f)] float irregularityScale = 3.2f;
        [SerializeField, Range(0.0f, 0.95f)] float contactThreshold = 0.72f;
        [SerializeField, Range(0.0f, 1.0f)] float contactSoftness = 0.82f;
        [SerializeField, Range(0.0f, 4.0f)] float normalStrength = 1.80f;
        [SerializeField] bool animateInPlayMode;
        [SerializeField, Min(0.01f)] float animationSpeed = 0.24f;
        [SerializeField, HideInInspector] Transform probe0;
        [SerializeField, HideInInspector] Transform probe1;
        [SerializeField, HideInInspector] Transform probe2;
        [SerializeField, HideInInspector] Transform probe3;

        Mesh sourceMesh;
        Material sourceMaterial;
        Mesh previewMesh;
        Material previewMaterial;
        Texture2D previewBaseTexture;
        Texture2DArray previewShadeGradients;

        public static GameObject CreateProbePreview(Transform parent, int index)
        {
            var spec = Contacts[Mathf.Clamp(index, 0, Contacts.Length - 1)];
            var probe = GameObject.CreatePrimitive(spec.Primitive);
            probe.name = spec.Name;
            probe.transform.SetParent(parent, false);
            probe.transform.localScale = spec.VisualScale;
            probe.transform.localRotation = Quaternion.Euler(spec.VisualRotation);
            // 最小構成の検証projectでもsampleをcompileできるよう、PhysicsModuleへ型参照しない。
            var collider = probe.GetComponent("Collider");
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            return probe;
        }

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
            ApplyPressures(CurrentPressures());
        }

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
            pressure0 = Mathf.Clamp01(pressure0);
            pressure1 = Mathf.Clamp01(pressure1);
            pressure2 = Mathf.Clamp01(pressure2);
            pressure3 = Mathf.Clamp01(pressure3);
            depth = Mathf.Clamp(depth, 0.0f, 0.03f);
            outerBulge = Mathf.Clamp01(outerBulge);
            indentSpread = Mathf.Clamp(indentSpread, 0.15f, 0.85f);
            edgeSoftness = Mathf.Clamp01(edgeSoftness);
            contourIrregularity = Mathf.Clamp(contourIrregularity, 0.0f, 0.3f);
            irregularityScale = Mathf.Clamp(irregularityScale, 1.0f, 10.0f);
            contactThreshold = Mathf.Clamp(contactThreshold, 0.0f, 0.95f);
            contactSoftness = Mathf.Clamp01(contactSoftness);
            normalStrength = Mathf.Clamp(normalStrength, 0.0f, 4.0f);
            animationSpeed = Mathf.Max(0.01f, animationSpeed);
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (previewMesh == null || previewMaterial == null)
            {
                Apply();
                return;
            }

            ConfigureMaterial();
            ApplyPressures(CurrentPressures());
        }

        void Update()
        {
            if (!Application.isPlaying || !animateInPlayMode || previewMaterial == null)
            {
                return;
            }

            var cycle = Time.time * animationSpeed;
            var proximity = new Vector4(
                Pulse(cycle, 0.00f),
                Pulse(cycle, 0.27f),
                Pulse(cycle, 0.53f),
                Pulse(cycle, 0.79f));
            ApplyPressures(proximity);
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
            DestroyGenerated(previewBaseTexture);
            DestroyGenerated(previewShadeGradients);
            previewMesh = null;
            previewMaterial = null;
            previewBaseTexture = null;
            previewShadeGradients = null;
        }

        static float Pulse(float cycle, float phase)
        {
            var wave = 0.5f - 0.5f * Mathf.Cos((cycle + phase) * Mathf.PI * 2.0f);
            return Mathf.SmoothStep(0.0f, 1.0f, wave);
        }

        Vector4 CurrentPressures()
        {
            return new Vector4(pressure0, pressure1, pressure2, pressure3);
        }

        void RebuildMesh(MeshFilter meshFilter)
        {
            DestroyGenerated(previewMesh);
            previewMesh = CreatePatchMesh();
            previewMesh.name = "Mochi Skin World Demo Patch";
            previewMesh.hideFlags = HideFlags.HideAndDontSave;
            meshFilter.sharedMesh = previewMesh;
        }

        void RebuildMaterial(MeshRenderer meshRenderer)
        {
            DestroyGenerated(previewMaterial);
            DestroyGenerated(previewBaseTexture);
            DestroyGenerated(previewShadeGradients);
            previewMaterial = null;
            previewBaseTexture = null;
            previewShadeGradients = null;

            var shader = Shader.Find(PreferredShaderName);
            if (shader == null)
            {
                shader = Shader.Find(FallbackShaderName);
            }

            if (shader == null)
            {
                meshRenderer.sharedMaterial = null;
                Debug.LogError(
                    $"[{nameof(MochiSkinWorldDemoObject)}] {PreferredShaderName}または" +
                    $"{FallbackShaderName}が見つかりません。",
                    this);
                return;
            }

            previewMaterial = new Material(shader)
            {
                name = $"Mochi Skin {skinFinish} Material ({shader.name})",
                hideFlags = HideFlags.HideAndDontSave,
            };
            meshRenderer.sharedMaterial = previewMaterial;

            if (!previewMaterial.HasProperty(MochiSkin + "Amount"))
            {
                Debug.LogError(
                    $"[{nameof(MochiSkinWorldDemoObject)}] {shader.name}にMochi Skinのmaterial propertyがありません。" +
                    "Shader CoreのProject Settingsで対象shaderへMochi Skinを追加してください。",
                    this);
                return;
            }

            ConfigureMaterial();
        }

        void ConfigureMaterial()
        {
            if (previewMaterial == null)
            {
                return;
            }

            var dry = skinFinish == SkinFinish.Dry;
            var skinColor = dry
                ? new Color(0.82f, 0.57f, 0.49f, 1.0f)
                : new Color(0.76f, 0.43f, 0.37f, 1.0f);
            var roughness = dry ? 0.78f : 0.13f;

            if (previewBaseTexture == null)
            {
                previewBaseTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = $"Mochi Skin {skinFinish} Base Color",
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            previewBaseTexture.SetPixel(0, 0, skinColor);
            previewBaseTexture.Apply(false, false);

            if (previewShadeGradients == null)
            {
                previewShadeGradients = new Texture2DArray(
                    128,
                    1,
                    1,
                    TextureFormat.RGBA32,
                    false)
                {
                    name = $"Mochi Skin {skinFinish} Shade Gradient",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                var ramp = new Color[128];
                var shadow = dry
                    ? new Color(0.25f, 0.22f, 0.21f, 1.0f)
                    : new Color(0.35f, 0.28f, 0.27f, 1.0f);
                for (var index = 0; index < ramp.Length; index++)
                {
                    var value = Mathf.SmoothStep(
                        0.0f,
                        1.0f,
                        index / (ramp.Length - 1.0f));
                    ramp[index] = Color.Lerp(shadow, Color.white, value);
                }
                previewShadeGradients.SetPixels(ramp, 0);
                previewShadeGradients.Apply(false, false);
            }

            SetTextureIfPresent("_BaseTexture", previewBaseTexture);
            SetTextureIfPresent("_SharedGradients", previewShadeGradients);
            SetColorIfPresent("_BaseColor", skinColor);
            SetFloatIfPresent("_Roughness", roughness);
            SetColorIfPresent(
                "_jp_lilxyzw_nontoon_specular_SpecularColor",
                dry
                    ? new Color(0.48f, 0.40f, 0.37f, 1.0f)
                    : new Color(0.68f, 0.50f, 0.46f, 1.0f));
            SetFloatIfPresent("_jp_lilxyzw_nontoon_specular_SpecularMultiplyAlbedo", dry ? 0.15f : 0.45f);
            SetIntegerIfPresent("_jp_lilxyzw_nontoon_shade_ShadeGradientIndex", 0);
            SetVectorIfPresent(
                "_jp_lilxyzw_nontoon_shade_ShadeGradientRange",
                new Vector4(0.08f, 1.0f, 0.0f, 0.0f));
            SetFloatIfPresent("_ShadeBorder1", 0.48f);
            SetFloatIfPresent("_ShadeBlur1", 0.24f);
            SetFloatIfPresent("_OutlineWidth", 0.0f);
            SetIntegerIfPresent("_OutlineEnabled", 0);

            previewMaterial.SetFloat(MochiSkin + "Amount", 1.0f);
            previewMaterial.SetInteger(MochiSkin + "UVChannel", 0);
            previewMaterial.SetFloat(MochiSkin + "Depth", depth);
            previewMaterial.SetFloat(MochiSkin + "Compliance", compliance);
            previewMaterial.SetTexture(MochiSkin + "ComplianceMask", complianceMask != null ? complianceMask : Texture2D.whiteTexture);
            previewMaterial.SetFloat(MochiSkin + "Bulge", outerBulge);
            previewMaterial.SetFloat(MochiSkin + "IndentSpread", indentSpread);
            previewMaterial.SetFloat(MochiSkin + "EdgeSoftness", edgeSoftness);
            previewMaterial.SetFloat(MochiSkin + "Irregularity", contourIrregularity);
            previewMaterial.SetFloat(MochiSkin + "IrregularityScale", irregularityScale);
            previewMaterial.SetFloat(MochiSkin + "ContactThreshold", contactThreshold);
            previewMaterial.SetFloat(MochiSkin + "ContactSoftness", contactSoftness);
            previewMaterial.SetFloat(MochiSkin + "NormalStrength", normalStrength);
            for (var index = 0; index < Contacts.Length; index++)
            {
                previewMaterial.SetVector(MochiSkin + "Point" + index, Contacts[index].Point);
                previewMaterial.SetVector(MochiSkin + "Shape" + index, Contacts[index].Shape);
            }
        }

        void ApplyPressures(Vector4 pressures)
        {
            if (previewMaterial == null)
            {
                return;
            }

            for (var index = 0; index < Contacts.Length; index++)
            {
                var proximity = Mathf.Clamp01(pressures[index]);
                previewMaterial.SetFloat(MochiSkin + "Pressure" + index, proximity);
                var point = UpdateProbe(Probe(index), Contacts[index], proximity);
                previewMaterial.SetVector(MochiSkin + "Point" + index, point);
            }
        }

        Transform Probe(int index)
        {
            switch (index)
            {
                case 0: return probe0;
                case 1: return probe1;
                case 2: return probe2;
                default: return probe3;
            }
        }

        Vector4 UpdateProbe(Transform probe, ContactSpec contact, float proximity)
        {
            if (probe == null)
            {
                return contact.Point;
            }

            float signedGap;
            if (proximity < contactThreshold)
            {
                signedGap = Mathf.Lerp(
                    ApproachGap,
                    0.0f,
                    proximity / Mathf.Max(contactThreshold, 1.0e-4f));
            }
            else
            {
                var penetration = (proximity - contactThreshold) /
                    Mathf.Max(1.0f - contactThreshold, 1.0e-4f);
                signedGap = -MaximumPenetration * penetration;
            }

            var surface = SurfacePosition(contact.Point.x, contact.Point.y);
            // 接平面ではなく曲面への最初の接触を求める。接触点も輪郭中心へ反映する。
            var touchZ = surface.z - contact.SupportDistance;
            var point = contact.Point;
            var meshFilter = probe.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                touchZ = float.PositiveInfinity;
                foreach (var vertex in meshFilter.sharedMesh.vertices)
                {
                    var offset = probe.localRotation * Vector3.Scale(vertex, probe.localScale);
                    var u = (surface.x + offset.x) / PatchWidth + 0.5f;
                    var v = (surface.y + offset.y) / PatchHeight + 0.5f;
                    var candidate = SurfacePosition(u, v).z - offset.z;
                    if (candidate < touchZ)
                    {
                        touchZ = candidate;
                        point.x = u;
                        point.y = v;
                    }
                }
            }
            probe.localPosition = new Vector3(surface.x, surface.y, touchZ - signedGap);
            return point;
        }

        void SetTextureIfPresent(string property, Texture texture)
        {
            if (previewMaterial.HasProperty(property))
            {
                previewMaterial.SetTexture(property, texture);
            }
        }

        void SetColorIfPresent(string property, Color value)
        {
            if (previewMaterial.HasProperty(property))
            {
                previewMaterial.SetColor(property, value);
            }
        }

        void SetFloatIfPresent(string property, float value)
        {
            if (previewMaterial.HasProperty(property))
            {
                previewMaterial.SetFloat(property, value);
            }
        }

        void SetVectorIfPresent(string property, Vector4 value)
        {
            if (previewMaterial.HasProperty(property))
            {
                previewMaterial.SetVector(property, value);
            }
        }

        void SetIntegerIfPresent(string property, int value)
        {
            if (previewMaterial.HasProperty(property))
            {
                previewMaterial.SetInteger(property, value);
            }
        }

        static Mesh CreatePatchMesh()
        {
            var columns = HorizontalSegments + 1;
            var rows = VerticalSegments + 1;
            var vertices = new Vector3[columns * rows];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[HorizontalSegments * VerticalSegments * 6];

            for (var y = 0; y < rows; y++)
            {
                var v = (float)y / VerticalSegments;
                for (var x = 0; x < columns; x++)
                {
                    var u = (float)x / HorizontalSegments;
                    var index = y * columns + x;
                    vertices[index] = SurfacePosition(u, v);
                    uv[index] = new Vector2(u, v);
                }
            }

            var triangle = 0;
            for (var y = 0; y < VerticalSegments; y++)
            {
                for (var x = 0; x < HorizontalSegments; x++)
                {
                    var lowerLeft = y * columns + x;
                    var lowerRight = lowerLeft + 1;
                    var upperLeft = lowerLeft + columns;
                    var upperRight = upperLeft + 1;
                    triangles[triangle++] = lowerLeft;
                    triangles[triangle++] = upperLeft;
                    triangles[triangle++] = lowerRight;
                    triangles[triangle++] = lowerRight;
                    triangles[triangle++] = upperLeft;
                    triangles[triangle++] = upperRight;
                }
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 SurfacePosition(float u, float v)
        {
            var x = (u - 0.5f) * PatchWidth;
            var y = (v - 0.5f) * PatchHeight;
            var normalizedX = x / (PatchWidth * 0.5f);
            var normalizedY = y / (PatchHeight * 0.5f);
            var z = -0.12f + 0.055f * (normalizedX * normalizedX + normalizedY * normalizedY);
            return new Vector3(x, y, z);
        }

        static Vector3 SurfaceNormal(float u, float v)
        {
            var x = (u - 0.5f) * PatchWidth;
            var y = (v - 0.5f) * PatchHeight;
            var derivativeX = 0.11f * x / Mathf.Pow(PatchWidth * 0.5f, 2.0f);
            var derivativeY = 0.11f * y / Mathf.Pow(PatchHeight * 0.5f, 2.0f);
            return new Vector3(derivativeX, derivativeY, -1.0f).normalized;
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
