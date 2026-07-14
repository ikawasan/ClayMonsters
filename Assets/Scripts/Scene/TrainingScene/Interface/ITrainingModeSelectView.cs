using Cysharp.Threading.Tasks;
using Scene.TrainingScene.Domain;
using System.Threading;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 育成方式選択ウィンドウの表示と入力
    /// </summary>
    public interface ITrainingModeSelectView
    {
        /// <summary>
        /// 育成方式選択を表示する
        /// </summary>
        /// <param name="modelName">モデル名</param>
        void Show(string modelName);

        /// <summary>
        /// ウィンドウを隠す
        /// </summary>
        void Hide();

        /// <summary>
        /// 育成方式が選ばれるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>選ばれた育成方式</returns>
        UniTask<TrainingPlayMode> WaitChoiceAsync(CancellationToken cancellationToken);
    }
}
