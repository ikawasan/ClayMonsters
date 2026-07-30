using Cysharp.Threading.Tasks;
using SaveData;
using System.IO;
using System.Threading;
using UnityEngine;
using VContainer;

namespace ClayEditor
{
    /// <summary>
    /// 保存済みモデルを造形用ボクセルへ復元する
    /// </summary>
    public sealed class ClayEditSavedModelImporter
    {
        private readonly ClayVoxelEngine engine;

        [Inject]
        public ClayEditSavedModelImporter(ClayVoxelEngine engine)
        {
            this.engine = engine;
        }

        /// <summary>
        /// スロットのボクセルスナップショットから造形データを復元する
        /// </summary>
        /// <param name="pool">保存プール</param>
        /// <param name="slotIndex">スロット番号</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功した場合trueと空文字失敗時はfalseと理由</returns>
        public UniTask<(bool success, string errorMessage)> TryImportFromSnapshotAsync(
            ModelSavePool pool,
            int slotIndex,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string filePath = Path.Combine(
                Application.persistentDataPath,
                ModelSavePoolSettings.GetVoxelFileName(pool, slotIndex));

            if (!ClayVoxelSnapshotFile.TryRead(filePath, out ClayVoxelSnapshotFile.Snapshot snapshot, out string readError))
            {
                return UniTask.FromResult((false, readError));
            }

            (bool success, string errorMessage) = engine.TryRestoreVoxelSnapshot(
                snapshot.Voxels,
                snapshot.Colors,
                snapshot.GridSize,
                snapshot.BoundsSize);
            return UniTask.FromResult((success, errorMessage));
        }

        /// <summary>
        /// ロード済みGLBモデルルートからメッシュをボクセル化する
        /// スナップショットが無い旧セーブデータ向けのフォールバック
        /// </summary>
        /// <param name="importedRoot">GLBインポート結果のルート</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功した場合trueと空文字失敗時はfalseと理由</returns>
        public async UniTask<(bool success, string errorMessage)> TryImportFromLoadedModelAsync(
            GameObject importedRoot,
            CancellationToken cancellationToken)
        {
            if (!ClayVoxelMeshImportSource.Handle.TryCreate(importedRoot, out ClayVoxelMeshImportSource.Handle meshSource, out string prepareError))
            {
                return (false, prepareError);
            }

            using (meshSource)
            {
                Color[] vertexColors = meshSource.VertexColors;
                if (vertexColors == null)
                {
                    Debug.LogWarning(
                        "[ClayEditSavedModelImporter] GLBメッシュに頂点カラーが無いため既定色でボクセル化します");
                }
                else
                {
                    Debug.Log(
                        $"[ClayEditSavedModelImporter] 頂点カラー取得 verts={meshSource.Mesh.vertexCount}"
                        + $" colors={vertexColors.Length}");
                }

                return await engine.TryImportFromWorldMeshAsync(
                    meshSource.Mesh,
                    engine.ClayModelTransform,
                    meshSource.MeshTransform,
                    vertexColors,
                    cancellationToken,
                    applyAutoOrientation: false,
                    fitToGrid: false);
            }
        }
    }
}
