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

            return container;
        }
    }
}
