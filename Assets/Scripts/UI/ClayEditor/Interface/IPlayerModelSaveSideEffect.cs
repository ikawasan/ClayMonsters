using System.Threading;
using Cysharp.Threading.Tasks;

namespace UI.ClayEditor.Interface
{
    /// <summary>
    /// プレイヤーモデル保存直後の追加処理
    /// </summary>
    public interface IPlayerModelSaveSideEffect
    {
        /// <summary>
        /// プレイヤーモデル保存直後に実行する
        /// </summary>
        /// <param name="slotIndex">保存先スロット</param>
        /// <param name="cancellationToken">取消トークン</param>
        UniTask OnPlayerModelSavedAsync(int slotIndex, CancellationToken cancellationToken);

        /// <summary>
        /// プレイヤーモデル削除直後に実行する
        /// </summary>
        /// <param name="slotIndex">削除したスロット</param>
        void OnPlayerModelDeleted(int slotIndex);
    }
}
