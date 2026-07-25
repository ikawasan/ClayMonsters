using Cysharp.Threading.Tasks;
using GLTFast.Export;
using SaveData.Interface;
using System.Threading;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// glTFastを用いてモデルをglbへエクスポートする
    /// 実行中のSkinnedMeshRendererを直接渡すのではなく、
    /// 「Root > ClayModel > { 複製ボーン, Mesh(新規SMR) }」という独立階層を組み立て、
    /// ボーンを名前で再バインドしbindposesを再計算してからエクスポートする
    /// </summary>
    public sealed class ClayModelGltfExporter : IClayModelExporter
    {
        /// <inheritdoc />
        public async UniTask<bool> ExportToGlbAsync(
            SkinnedMeshRenderer runtimeRenderer,
            Transform boneRoot,
            string filePath,
            CancellationToken cancellationToken)
        {
            if (runtimeRenderer == null || runtimeRenderer.sharedMesh == null || boneRoot == null)
            {
                Debug.LogError("[Exporter] runtimeRenderer / sharedMesh / boneRoot が不足しています。");
                return false;
            }

            // メッシュを複製する
            Mesh exportMesh = Object.Instantiate(runtimeRenderer.sharedMesh);
            exportMesh.name = $"{runtimeRenderer.gameObject.name}_Mesh";
            var exportSettings = new ExportSettings
            {
                Format = GltfFormat.Binary,
                FileConflictResolution = FileConflictResolution.Overwrite,
                PreservedVertexAttributes = VertexAttributeUsage.Color
            };

            var gameObjectExportSettings = new GameObjectExportSettings
            {
                OnlyActiveInHierarchy = true,
                DisabledComponents = false
            };

            var export = new GameObjectExport(exportSettings, gameObjectExportSettings);

            // 組み立てたRootのみをシーンのルートとして追加する
            var bonesInHierarchy = CloneBonesHierarchy(boneRoot);
            // ボーン階層に元メッシュが含まれるとglbへ二重に書き出されるため複製側から取り除く
            StripRenderers(bonesInHierarchy);
            var meshObject = CreateMeshObject(runtimeRenderer, exportMesh, bonesInHierarchy.transform);
            export.AddScene(new[] { bonesInHierarchy, meshObject });

            bool success = await export
                .SaveToFileAndDispose(filePath, cancellationToken)
                .AsUniTask();

            // テンポラリ階層と複製メッシュを破棄する
            Object.Destroy(bonesInHierarchy);
            Object.Destroy(exportMesh);

            return success;
        }

        /// <summary>
        /// 複製ボーン階層からレンダラーを取り除く
        /// AddSceneが同フレームで参照するためDestroyImmediateを使う
        /// </summary>
        private static void StripRenderers(GameObject clonedRoot)
        {
            Renderer[] renderers = clonedRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Object.DestroyImmediate(renderers[i]);
            }

            MeshFilter[] meshFilters = clonedRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                Object.DestroyImmediate(meshFilters[i]);
            }
        }

        /// <summary>
        /// ボーン階層を複製して返します。
        /// </summary>
        private GameObject CloneBonesHierarchy(Transform boneRoot)
        {
            GameObject clonedBones = Object.Instantiate(boneRoot.gameObject);
            clonedBones.name = boneRoot.name;
            clonedBones.transform.localPosition = boneRoot.localPosition;
            clonedBones.transform.localRotation = boneRoot.localRotation;
            clonedBones.transform.localScale = boneRoot.localScale;

            return clonedBones;
        }

        /// <summary>
        /// メッシュオブジェクトを生成し、複製されたボーンに再バインドして返します。
        /// </summary>
        private GameObject CreateMeshObject(SkinnedMeshRenderer runtimeRenderer, Mesh exportMesh, Transform clonedBonesTransform)
        {
            GameObject meshObject = new GameObject("Mesh");
            meshObject.transform.localPosition = runtimeRenderer.transform.localPosition;
            meshObject.transform.localRotation = runtimeRenderer.transform.localRotation;
            meshObject.transform.localScale = runtimeRenderer.transform.localScale;

            var skinnedMeshRenderer = meshObject.AddComponent<SkinnedMeshRenderer>();
            skinnedMeshRenderer.sharedMesh = exportMesh;
            skinnedMeshRenderer.sharedMaterial = runtimeRenderer.sharedMaterial;

            // ルートボーンを複製側から名前で再バインドする
            if (runtimeRenderer.rootBone != null)
            {
                Transform mappedRoot = FindChildRecursive(clonedBonesTransform, runtimeRenderer.rootBone.name);
                skinnedMeshRenderer.rootBone = mappedRoot != null ? mappedRoot : clonedBonesTransform;
            }
            else
            {
                skinnedMeshRenderer.rootBone = clonedBonesTransform;
            }

            // 各ボーンを複製側から名前で再バインドし、bindposes を再計算する
            Transform[] originalBones = runtimeRenderer.bones;
            Transform[] newBones = new Transform[originalBones.Length];
            Matrix4x4[] newBindPoses = new Matrix4x4[originalBones.Length];

            for (int i = 0; i < originalBones.Length; i++)
            {
                Transform originalBone = originalBones[i];
                Transform foundBone = originalBone != null
                    ? FindChildRecursive(clonedBonesTransform, originalBone.name)
                    : null;

                if (foundBone == null)
                {
                    foundBone = skinnedMeshRenderer.rootBone != null ? skinnedMeshRenderer.rootBone : clonedBonesTransform;
                }

                newBones[i] = foundBone;
                newBindPoses[i] = foundBone.worldToLocalMatrix * meshObject.transform.localToWorldMatrix;
            }

            skinnedMeshRenderer.bones = newBones;
            exportMesh.bindposes = newBindPoses;

            return meshObject;
        }

        /// <summary>
        /// 指定の親階層から名前が一致するTransformを再帰的に探す
        /// </summary>
        private Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            foreach (Transform child in parent)
            {
                Transform found = FindChildRecursive(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}