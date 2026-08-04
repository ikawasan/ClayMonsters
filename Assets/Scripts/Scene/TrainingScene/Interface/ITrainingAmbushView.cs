using Cysharp.Threading.Tasks;
using Scene.TrainingScene.Domain;
using System.Threading;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 強敵急襲のアラート演出と戦う逃げる選択ウィンドウ
    /// </summary>
    public interface ITrainingAmbushView
    {
        /// <summary>
        /// 強敵急襲アラート演出を再生する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask PlayAlertAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 戦う逃げる選択ウィンドウを表示する
        /// </summary>
        /// <param name="enemySlotIndex">敵スロット</param>
        /// <param name="fallbackEnemyName">セーブ上の名前などフォールバック</param>
        void ShowChoice(int enemySlotIndex, string fallbackEnemyName);

        /// <summary>
        /// 選択が決まるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>選択結果</returns>
        UniTask<TrainingAmbushChoice> WaitChoiceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// アラートと選択ウィンドウを隠す
        /// </summary>
        void Hide();
    }
}
