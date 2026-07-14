using Cysharp.Threading.Tasks;
using Scene.TrainingScene.Domain;
using System.Threading;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 育成再開確認ウィンドウの表示と入力
    /// </summary>
    public interface ITrainingResumeWindowView
    {
        /// <summary>
        /// 育成途中データを表示する
        /// </summary>
        /// <param name="presentation">表示データ</param>
        void Show(TrainingResumeProgressPresentation presentation);

        /// <summary>
        /// ウィンドウを隠す
        /// </summary>
        void Hide();

        /// <summary>
        /// 続きからか最初からかが選ばれるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>続きからならtrue</returns>
        UniTask<bool> WaitChoiceAsync(CancellationToken cancellationToken);
    }
}
