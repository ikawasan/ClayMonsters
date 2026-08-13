using System.Threading;
using Cysharp.Threading.Tasks;

namespace UI.ClayEditor.Interface
{
    /// <summary>
    /// プレイヤーモデル保存追加処理の空実装
    /// </summary>
    public sealed class NullPlayerModelSaveSideEffect : IPlayerModelSaveSideEffect
    {
        /// <inheritdoc/>
        public UniTask OnPlayerModelSavedAsync(int slotIndex, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        public void OnPlayerModelDeleted(int slotIndex)
        {
        }
    }
}
