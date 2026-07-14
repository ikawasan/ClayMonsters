using Cysharp.Threading.Tasks;
using System.Threading;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 育成シーンのプレゼンター
    /// </summary>
    public interface ITrainingPresenter
    {
        /// <summary>
        /// 購読を設定する
        /// </summary>
        void Setup();

        /// <summary>
        /// シーン入場時処理
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask OnEnterAsync(CancellationToken cancellationToken);

        /// <summary>
        /// シーン退場時処理
        /// </summary>
        void OnLeave();
    }
}
