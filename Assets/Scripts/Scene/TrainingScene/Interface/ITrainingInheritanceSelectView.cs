using Cysharp.Threading.Tasks;
using System.Threading;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 育成開始時の継承元選択UI
    /// </summary>
    public interface ITrainingInheritanceSelectView
    {
        /// <summary>
        /// 継承元2体の選択が完了するまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>選択した育成済みスロット番号の組戻る操作時は両方-1</returns>
        UniTask<(int parentSlotA, int parentSlotB)> WaitForParentsAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// シーン退場時にUIを隠す
        /// </summary>
        void HideForLeave();
    }
}
