using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaShader.Editor
{
    /// <summary>Humanoidのbind poseと全ウェイトから、UV3.xへ変形係数を保存する。</summary>
    public static class MochiSkinComplianceBaker
    {
        const string Prefix = "_io_github_sabas0ba_mochiskin_";
        const string OutputRoot = "Assets/MochiSkinGenerated";

        public static float SegmentCompliance(Vector3 vertex, Vector3 start, Vector3 end)
        {
            var axis = end - start;
            var length = axis.magnitude;
            if (length < 1.0e-6f) return 1.0f;
            var t = Mathf.Clamp01(Vector3.Dot(vertex - start, axis) / axis.sqrMagnitude);
            var shaftDistance = Vector3.Distance(vertex, start + axis * t) / length;
            var jointDistance = Mathf.Min(Vector3.Distance(vertex, start), Vector3.Distance(vertex, end)) / length;
            var shaft = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.05f, 0.25f, shaftDistance));
            var joint = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.12f, 0.35f, jointDistance));
            return Mathf.Lerp(0.15f, 1.0f, Mathf.Min(shaft, joint));
        }

        public static Mesh BakeMesh(SkinnedMeshRenderer renderer, Animator animator)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
                throw new InvalidOperationException("有効なHumanoid Animatorが必要です。");
            var source = renderer.sharedMesh;
            if (source == null || !source.isReadable)
                throw new InvalidOperationException(renderer.name + ": 読み取り可能なmeshが必要です。");
            if (source.HasVertexAttribute(VertexAttribute.TexCoord3))
                throw new InvalidOperationException(renderer.name + ": UV3は使用済みです。既存データは上書きしません。");

            var humans = new HashSet<Transform>();
            for (var index = 0; index < (int)HumanBodyBones.LastBone; index++)
            {
                var bone = animator.GetBoneTransform((HumanBodyBones)index);
                if (bone != null) humans.Add(bone);
            }
            return BakeWithBones(renderer, humans);
        }

        // Humanoid解決と数値処理を分離し、合成rigでも全ウェイト・bind poseを検証できるようにする。
        public static Mesh BakeWithBones(SkinnedMeshRenderer renderer, HashSet<Transform> humans)
        {
            var source = renderer.sharedMesh;
            if (source == null || !source.isReadable || source.HasVertexAttribute(VertexAttribute.TexCoord3))
                throw new InvalidOperationException("読み取り可能でUV3が未使用のmeshが必要です。");
            var bones = renderer.bones;
            var poses = source.bindposes;
            var indices = new Dictionary<Transform, int>();
            for (var index = 0; index < bones.Length && index < poses.Length; index++)
                if (bones[index] != null) indices[bones[index]] = index;

            var segments = new List<(Transform parent, Transform child, Vector3 start, Vector3 end)>();
            foreach (var child in humans)
            {
                var parent = child.parent;
                while (parent != null && !humans.Contains(parent)) parent = parent.parent;
                if (parent == null || !indices.TryGetValue(parent, out var p) || !indices.TryGetValue(child, out var c)) continue;
                segments.Add((parent, child, poses[p].inverse.MultiplyPoint3x4(Vector3.zero), poses[c].inverse.MultiplyPoint3x4(Vector3.zero)));
            }
            if (segments.Count == 0)
                throw new InvalidOperationException(renderer.name + ": bind pose内にHumanoidの骨区間がありません。");

            var mapped = new Transform[bones.Length];
            for (var index = 0; index < bones.Length; index++)
            {
                var bone = bones[index];
                while (bone != null && !humans.Contains(bone)) bone = bone.parent;
                mapped[index] = bone;
            }
            var vertices = source.vertices;
            var weights = source.GetAllBoneWeights();
            var counts = source.GetBonesPerVertex();
            if (counts.Length != vertices.Length)
                throw new InvalidOperationException(renderer.name + ": 頂点ウェイトがありません。");
            var values = new List<Vector2>(vertices.Length);
            var cursor = 0;
            for (var vertex = 0; vertex < vertices.Length; vertex++)
            {
                var value = 0.0f;
                var total = 0.0f;
                for (var influence = 0; influence < counts[vertex]; influence++)
                {
                    var weight = weights[cursor++];
                    var softness = 1.0f;
                    var bone = weight.boneIndex < mapped.Length ? mapped[weight.boneIndex] : null;
                    if (bone != null)
                        foreach (var segment in segments)
                            if (segment.parent == bone || segment.child == bone)
                                softness = Mathf.Min(softness, SegmentCompliance(vertices[vertex], segment.start, segment.end));
                    value += weight.weight * softness;
                    total += weight.weight;
                }
                values.Add(new Vector2(total > 0 ? Mathf.Clamp01(value / total) : 1, 0));
            }
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = source.name + " Mochi Compliance";
            mesh.SetUVs(3, values);
            return mesh;
        }

        [MenuItem("Tools/SabaShader/Mochi Skin/Bake Humanoid Compliance")]
        public static void BakeSelected()
        {
            var root = Selection.activeGameObject;
            var animator = root != null ? root.GetComponentInParent<Animator>() : null;
            if (animator == null && root != null) animator = root.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                EditorUtility.DisplayDialog("Mochi Skin", "HumanoidアバターをHierarchyで選択してください。", "OK");
                return;
            }
            var renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.sharedMaterials.Any(IsMochiMaterial)).ToArray();
            var prepared = new List<(SkinnedMeshRenderer renderer, Mesh mesh)>();
            try
            {
                // 全meshを検査・生成してから、asset作成とrendererの変更を開始する。
                foreach (var renderer in renderers)
                {
                    if (renderer.sharedMaterials.Any(material => IsMochiMaterial(material) && material.GetInteger(Prefix + "UVChannel") == 3))
                        throw new InvalidOperationException(renderer.name + ": 接触座標がUV3です。骨マスクとの併用にはUV0～2を使用してください。");
                    prepared.Add((renderer, BakeMesh(renderer, animator)));
                }
                if (prepared.Count == 0) throw new InvalidOperationException("Mochi Skin対応materialを使用するSkinnedMeshRendererがありません。");
                if (!AssetDatabase.IsValidFolder(OutputRoot)) AssetDatabase.CreateFolder("Assets", "MochiSkinGenerated");
                foreach (var entry in prepared)
                {
                    AssetDatabase.CreateAsset(entry.mesh, AssetDatabase.GenerateUniqueAssetPath(OutputRoot + "/Compliance.asset"));
                    var materials = entry.renderer.sharedMaterials;
                    for (var index = 0; index < materials.Length; index++)
                    {
                        if (!IsMochiMaterial(materials[index])) continue;
                        var material = new Material(materials[index]);
                        material.SetInteger(Prefix + "UseBoneCompliance", 1);
                        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(OutputRoot + "/Compliance.mat"));
                        materials[index] = material;
                    }
                    Undo.RecordObject(entry.renderer, "Bake Mochi Skin compliance");
                    entry.renderer.sharedMesh = entry.mesh;
                    entry.renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(entry.renderer);
                    EditorUtility.SetDirty(entry.renderer);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[Mochi Skin] {prepared.Count} meshの骨マスクを生成しました。元のmesh/materialは変更していません。");
            }
            catch (Exception exception)
            {
                foreach (var entry in prepared)
                    if (!AssetDatabase.Contains(entry.mesh)) UnityEngine.Object.DestroyImmediate(entry.mesh);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Mochi Skin", exception.Message, "OK");
            }
        }

        static bool IsMochiMaterial(Material material)
        {
            return material != null && material.HasProperty(Prefix + "UseBoneCompliance");
        }
    }
}
