using System.Collections.Generic;
using UnityEngine;

namespace SabaShader.Samples
{
    /// <summary>
    /// Debug shader のサンプル表示に必要な頂点データと一時マテリアルを生成する。
    /// 生成物はシーンやプロジェクトへ保存しない。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DebugShaderDemoObject : MonoBehaviour
    {
        const string ShaderName = "SabaShader/Debug";

        [SerializeField, Range(0, 17)] int mode;
        [SerializeField, Min(0.001f)] float coordinateScale = 0.25f;
        [SerializeField, Range(0.25f, 4.0f)] float wireWidth = 1.25f;
        [SerializeField, HideInInspector] Mesh sourceMesh;

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

        void OnDisable()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh == previewMesh)
            {
                meshFilter.sharedMesh = sourceMesh;
            }

            DestroyGenerated(previewMesh);
            DestroyGenerated(previewMaterial);
            previewMesh = null;
            previewMaterial = null;
        }

        void RebuildMesh(MeshFilter meshFilter)
        {
            if (sourceMesh == null)
            {
                return;
            }

            DestroyGenerated(previewMesh);
            previewMesh = Instantiate(sourceMesh);
            previewMesh.name = sourceMesh.name + " (Debug Demo)";
            previewMesh.hideFlags = HideFlags.HideAndDontSave;

            var vertices = previewMesh.vertices;
            var colors = new Color[vertices.Length];
            var uv1 = new List<Vector4>(vertices.Length);
            var uv2 = new List<Vector4>(vertices.Length);
            var uv3 = new List<Vector4>(vertices.Length);

            for (var index = 0; index < vertices.Length; index++)
            {
                var vertex = vertices[index];
                var normalized = vertex.normalized * 0.5f + Vector3.one * 0.5f;
                colors[index] = new Color(normalized.x, normalized.y, normalized.z, 1.0f);
                uv1.Add(new Vector4(normalized.y, normalized.x, 0.0f, 0.0f));
                uv2.Add(new Vector4(normalized.x, normalized.z, 0.0f, 0.0f));
                uv3.Add(new Vector4(normalized.z, normalized.y, 0.0f, 0.0f));
            }

            previewMesh.colors = colors;
            previewMesh.SetUVs(1, uv1);
            previewMesh.SetUVs(2, uv2);
            previewMesh.SetUVs(3, uv3);
            meshFilter.sharedMesh = previewMesh;
        }

        void RebuildMaterial(MeshRenderer meshRenderer)
        {
            DestroyGenerated(previewMaterial);

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                meshRenderer.sharedMaterial = null;
                Debug.LogError($"[{nameof(DebugShaderDemoObject)}] {ShaderName} が見つかりません。", this);
                return;
            }

            previewMaterial = new Material(shader)
            {
                name = $"Debug Mode {mode}",
                hideFlags = HideFlags.HideAndDontSave,
            };
            previewMaterial.SetInteger("_Mode", mode);
            previewMaterial.SetInteger("_Cull", mode == 12 ? 0 : 2);
            previewMaterial.SetFloat("_CoordinateScale", coordinateScale);
            previewMaterial.SetFloat("_WireWidth", wireWidth);
            previewMaterial.SetColor("_WireColor", new Color(0.1f, 0.9f, 1.0f, 1.0f));
            previewMaterial.SetColor("_BackgroundColor", new Color(0.025f, 0.03f, 0.04f, 1.0f));
            meshRenderer.sharedMaterial = previewMaterial;
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
