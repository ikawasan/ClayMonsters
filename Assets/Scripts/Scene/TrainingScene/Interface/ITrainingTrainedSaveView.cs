using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData;
using System.Collections.Generic;
using System.Threading;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 育成完了時の育成済みスロット選択UI
    /// </summary>
    public interface ITrainingTrainedSaveView
    {
        /// <summary>
        /// 保存先スロットが確定するまで待機する
        /// </summary>
        /// <param name="modelName">モデル名</param>
        /// <param name="status">育成後ステータス</param>
        /// <param name="attackMotions">育成後攻撃構成</param>
        /// <param name="thumbnailPng">プレビュー用サムネイル</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>確定した育成済みスロット番号。タイトルへ戻る操作時は-1</returns>
        UniTask<int> WaitForConfirmedSlotAsync(
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions,
            byte[] thumbnailPng,
            CancellationToken cancellationToken);

        /// <summary>
        /// シーン退場時にUIを隠す
        /// </summary>
        void HideForLeave();
    }
}
