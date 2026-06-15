using R3;
using UnityEngine;

namespace ClayEditor.Input.Interface
{
    /// <summary>
    /// 入力情報を提供するインターフェース
    /// </summary>
    public interface IClayInputProvider
    {
        /// <summary>
        /// Undo 操作（Ctrl+Z）が要求された瞬間に発火
        /// </summary>
        Observable<Unit> OnUndo { get; }

        /// <summary>
        /// Redo 操作（Ctrl+Y）が要求された瞬間に発火
        /// </summary>
        Observable<Unit> OnRedo { get; }

        /// <summary>
        /// 全削除（Delete）が要求された瞬間に発火
        /// </summary>
        Observable<Unit> OnDelete { get; }

        /// <summary>
        /// 主ボタン（左クリック）が押された瞬間に発火
        /// </summary>
        Observable<Unit> OnPrimaryPressed { get; }

        /// <summary>
        /// 副ボタン（右クリック）が押された瞬間に発火
        /// </summary>
        Observable<Unit> OnSecondaryPressed { get; }

        /// <summary>
        /// ホイールのスクロール量
        /// </summary>
        Observable<float> OnScroll { get; }

        /// <summary>
        /// 現在のポインタのスクリーン座標
        /// </summary>
        Vector2 PointerPosition { get; }

        /// <summary>
        /// ポインタがUI上にあるかどうかの状態変更イベント
        /// </summary>
        Observable<bool> OnPointerOverUIChanged { get; }

        /// <summary>
        /// ポインタがUI上にあるか
        /// </summary>
        bool IsPointerOverUI { get; }

        /// <summary>
        /// 主ボタン（左）が押され続けているか
        /// </summary>
        bool IsPrimaryHeld { get; }

        /// <summary>
        /// 副ボタン（右）が押され続けているか
        /// </summary>
        bool IsSecondaryHeld { get; }

        /// <summary>
        /// Shiftキーが押されているか
        /// </summary>
        bool IsShiftPressed { get; }

        /// <summary>
        /// Ctrlキーが押されているか
        /// </summary>
        bool IsCtrlPressed { get; }

        /// <summary>
        /// Altキーが押されているか
        /// </summary>
        bool IsAltPressed { get; }
    }
}
