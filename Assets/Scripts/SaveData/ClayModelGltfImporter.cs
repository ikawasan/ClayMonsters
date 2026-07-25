using Cysharp.Threading.Tasks;
using GLTFast;
using GLTFast.Logging;
using SaveData.Interface;
using System.IO;
using System.Threading;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// glTFast を用いて glb を読み込み、シーンへ生成する実装。
    /// </summary>
    public sealed class ClayModelGltfImporter : IClayModelImporter
    {
        private readonly SemaphoreSlim importGate = new SemaphoreSlim(1, 1);

        /// <inheritdoc />
        public async UniTask<GameObject> ImportFromGlbAsync(string filePath, Transform parent, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.LogWarning($"[ClayModelGltfImporter] インポート対象のファイルが見つかりません: {filePath}");
                return null;
            }

            await importGate.WaitAsync(cancellationToken);
            try
            {
                return await ImportFromGlbCoreAsync(filePath, parent, cancellationToken);
            }
            finally
            {
                importGate.Release();
            }
        }

        private static async UniTask<GameObject> ImportFromGlbCoreAsync(
            string filePath,
            Transform parent,
            CancellationToken cancellationToken)
        {
            var gltf = new GltfImport(logger: new ConsoleLogger());

            bool loaded = await gltf
                .LoadFile(filePath, cancellationToken: cancellationToken)
                .AsUniTask();

            if (!loaded)
            {
                Debug.LogError($"[ClayModelGltfImporter] glbの読み込みに失敗しました: {filePath}");
                gltf.Dispose();
                return null;
            }

            var container = new GameObject(Path.GetFileNameWithoutExtension(filePath));
            if (parent != null)
            {
                container.transform.SetParent(parent, false);
            }

            bool instantiated = await gltf
                .InstantiateMainSceneAsync(container.transform, cancellationToken)
                .AsUniTask();

            if (!instantiated)
            {
                Debug.LogError($"[ClayModelGltfImporter] モデルの生成に失敗しました: {filePath}");
                Object.Destroy(container);
                gltf.Dispose();
                return null;
            }

            RemoveDuplicatedSkinnedMeshes(container, filePath);
            return container;
        }

        /// <summary>
        /// 過去の書き出し不具合でボーン階層内に複製メッシュが焼き込まれたglbを修復する
        /// 本ゲームのモデルはメッシュ1枚が正でありエクスポーター正規出力の浅い階層側を残す
        /// </summary>
        private static void RemoveDuplicatedSkinnedMeshes(GameObject container, string filePath)
        {
            SkinnedMeshRenderer[] renderers = container.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length <= 1)
            {
                return;
            }

            SkinnedMeshRenderer keep = renderers[0];
            int keepDepth = ResolveDepth(keep.transform, container.transform);
            for (int i = 1; i < renderers.Length; i++)
            {
                int depth = ResolveDepth(renderers[i].transform, container.transform);
                if (depth < keepDepth)
                {
                    keep = renderers[i];
                    keepDepth = depth;
                }
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = renderers[i];
                if (renderer == keep)
                {
                    continue;
                }

                Debug.LogWarning(
                    "[ClayModelGltfImporter] 重複メッシュを除去しました"
                    + $" node={renderer.name}"
                    + $" file={Path.GetFileName(filePath)}");
                if (renderer.transform.childCount > 0)
                {
                    Object.Destroy(renderer);
                }
                else
                {
                    Object.Destroy(renderer.gameObject);
                }
            }
        }

        private static int ResolveDepth(Transform target, Transform root)
        {
            int depth = 0;
            Transform current = target;
            while (current != null && current != root)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }
    }
}
