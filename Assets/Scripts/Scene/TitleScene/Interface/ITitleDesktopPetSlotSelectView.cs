using R3;
using System.Collections.Generic;

namespace Scene.TitleScene.Interface
{
    /// <summary>
    /// デスクトップペット用の未育成スロット選択UIを制御する
    /// </summary>
    public interface ITitleDesktopPetSlotSelectView
    {
        /// <summary>
        /// 選択が確定した通知(1〜5件)
        /// </summary>
        Observable<IReadOnlyList<int>> OnSelectionConfirmed { get; }

        /// <summary>
        /// 選択をキャンセルした通知
        /// </summary>
        Observable<Unit> OnCancelled { get; }

        /// <summary>
        /// 未育成スロット選択UIを表示する
        /// </summary>
        void Show();

        /// <summary>
        /// 未育成スロット選択UIを隠す
        /// </summary>
        void Hide();
    }
}
