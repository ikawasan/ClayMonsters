using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 育成開始時の継承演出
    /// </summary>
    public interface ITrainingInheritancePresentation
    {
        /// <summary>
        /// 暗転中に背景とモデル配置を完了する
        /// </summary>
        /// <param name="traineeModel">継承先モデル</param>
        /// <param name="parentSlotA">継承元スロット1</param>
        /// <param name="parentSlotB">継承元スロット2</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask PrepareAsync(
            GameObject traineeModel,
            int parentSlotA,
            int parentSlotB,
            CancellationToken cancellationToken);

        /// <summary>
        /// 明転後に継承演出をタイトル表示まで再生する
        /// 終了状態はFinishAsyncで解除する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask PlayAsync(CancellationToken cancellationToken);

        /// <summary>
        /// タイトル表示後の演出状態を解除する
        /// </summary>
        /// <param name="traineeModel">継承先モデル</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask FinishAsync(GameObject traineeModel, CancellationToken cancellationToken);

        /// <summary>
        /// シーン退場時に演出を止める
        /// </summary>
        void HideForLeave();
    }
}
