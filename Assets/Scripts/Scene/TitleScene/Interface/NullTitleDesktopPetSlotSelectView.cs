using R3;
using System.Collections.Generic;

namespace Scene.TitleScene.Interface
{
    /// <summary>
    /// 未配線時のデスクトップペットスロット選択ダミー
    /// </summary>
    public sealed class NullTitleDesktopPetSlotSelectView : ITitleDesktopPetSlotSelectView
    {
        /// <inheritdoc/>
        public Observable<IReadOnlyList<int>> OnSelectionConfirmed => Observable.Empty<IReadOnlyList<int>>();

        /// <inheritdoc/>
        public Observable<Unit> OnCancelled => Observable.Empty<Unit>();

        /// <inheritdoc/>
        public void Show()
        {
        }

        /// <inheritdoc/>
        public void Hide()
        {
        }
    }
}
