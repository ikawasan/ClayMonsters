using System.Threading;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 訓練後の専用背景におけるモンスター徘徊
    /// </summary>
    public interface ITrainingMonsterRoamController
    {
        /// <summary>
        /// 指定範囲内で歩きと立ち止まりを繰り返す
        /// 非Roamからの初回開始時のみ範囲内のランダム位置へ配置する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        void StartRoam(CancellationToken cancellationToken);

        /// <summary>
        /// 徘徊を止め表示位置へ戻してIdleにする
        /// </summary>
        void StopRoam();
    }
}
