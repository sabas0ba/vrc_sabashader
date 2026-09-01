using UnityEngine;

namespace SabaShader.Samples
{
    /// <summary>
    /// Mochi Skinの4接触点を、VRCSDKに依存せずWorld展示シーンで確認する。
    /// 生成するmeshとmaterialはシーンやプロジェクトへ保存しない。
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MochiSkinWorldDemoObject : MonoBehaviour
    {
        const string ShaderName = "SabaShader/Illust2D";
        const string MochiSkin = "_io_github_sabas0ba_mochiskin_";
        const int HorizontalSegments = 64;
        const int VerticalSegments = 48;
        const float PatchWidth = 1.5f;
        const float PatchHeight = 1.1f;

        static readonly Vector4[] ContactPoints =
        {
            new Vector4(0.32f, 0.63f, 0.15f, 0.19f),
            new Vector4(0.68f, 0.63f, 0.15f, 0.19f),
            new Vector4(0.35f, 0.34f, 0.14f, 0.17f),
            new Vector4(0.65f, 0.34f, 0.14f, 0.17f),
        };

        [SerializeField, Range(0.0f, 1.0f)] float pressure0;
        [SerializeField, Range(0.0f, 1.0f)] float pressure1;
        [SerializeField, Range(0.0f, 1.0f)] float pressure2;
        [SerializeField, Range(0.0f, 1.0f)] float pressure3;
        [SerializeField] bool animateInPlayMode;
        [SerializeField, Min(0.01f)] float animationSpeed = 0.35f;
        [SerializeField, HideInInspector] Transform probe0;
        [SerializeField, HideInInspector] Transform probe1;
        [SerializeField, HideInInspector] Transform probe2;
        [SerializeField, HideInInspector] Transform probe3;

        Mesh sourceMesh;
        Material sourceMaterial;
        Mesh previewMesh;
        Material previewMaterial;

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

            ApplyPressures(CurrentPressures());
        }

        void Update()
        {
            if (!Application.isPlaying || !animateInPlayMode || previewMaterial == null)
            {
                return;
            }

            var cycle = Time.time * animationSpeed;
            var pressures = new Vector4(
                Pulse(cycle, 0.00f),
                Pulse(cycle, 0.27f),
                Pulse(cycle, 0.53f),
                Pulse(cycle, 0.79f));
            ApplyPressures(pressures);
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
            previewMesh = null;
            previewMaterial = null;
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
            previewMaterial = null;

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                meshRenderer.sharedMaterial = null;
                Debug.LogError($"[{nameof(MochiSkinWorldDemoObject)}] {ShaderName} が見つかりません。", this);
                return;
            }

            previewMaterial = new Material(shader)
            {
                name = "Mochi Skin World Demo Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
            meshRenderer.sharedMaterial = previewMaterial;

            if (!previewMaterial.HasProperty(MochiSkin + "Amount"))
            {
                Debug.LogError(
                    $"[{nameof(MochiSkinWorldDemoObject)}] Mochi Skinのmaterial propertyがありません。" +
                    "Shader CoreのProject SettingsでIllust2DへMochi Skinを追加してください。",
                    this);
                return;
            }

            previewMaterial.SetColor("_BaseColor", new Color(0.84f, 0.51f, 0.44f, 1.0f));
            previewMaterial.SetFloat("_Roughness", 0.62f);
            previewMaterial.SetFloat("_ShadeBorder1", 0.48f);
            previewMaterial.SetFloat("_ShadeBlur1", 0.18f);
            previewMaterial.SetInteger("_OutlineEnabled", 0);
            previewMaterial.SetInteger("_Cull", 2);
            previewMaterial.SetFloat(MochiSkin + "Amount", 1.0f);
            previewMaterial.SetInteger(MochiSkin + "UVChannel", 0);
            previewMaterial.SetFloat(MochiSkin + "Depth", 0.03f);
            previewMaterial.SetFloat(MochiSkin + "Bulge", 0.38f);
            previewMaterial.SetFloat(MochiSkin + "NormalStrength", 3.4f);
            for (var index = 0; index < ContactPoints.Length; index++)
            {
                previewMaterial.SetVector(MochiSkin + "Point" + index, ContactPoints[index]);
            }
        }

        void ApplyPressures(Vector4 pressures)
        {
            if (previewMaterial == null)
            {
                return;
            }

            for (var index = 0; index < ContactPoints.Length; index++)
            {
                var pressure = Mathf.Clamp01(pressures[index]);
                previewMaterial.SetFloat(MochiSkin + "Pressure" + index, pressure);
                UpdateProbe(Probe(index), ContactPoints[index], pressure);
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

        static void UpdateProbe(Transform probe, Vector4 contactPoint, float pressure)
        {
            if (probe == null)
            {
                return;
            }

            var surface = SurfacePosition(contactPoint.x, contactPoint.y);
            var normal = SurfaceNormal(contactPoint.x, contactPoint.y);
            probe.localPosition = surface + normal * Mathf.Lerp(0.26f, 0.09f, pressure);
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
            var z = -0.12f + 0.065f * (normalizedX * normalizedX + normalizedY * normalizedY);
            return new Vector3(x, y, z);
        }

        static Vector3 SurfaceNormal(float u, float v)
        {
            var x = (u - 0.5f) * PatchWidth;
            var y = (v - 0.5f) * PatchHeight;
            var derivativeX = 0.13f * x / Mathf.Pow(PatchWidth * 0.5f, 2.0f);
            var derivativeY = 0.13f * y / Mathf.Pow(PatchHeight * 0.5f, 2.0f);
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
