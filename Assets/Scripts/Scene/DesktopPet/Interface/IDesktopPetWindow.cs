using System;
using UnityEngine;

namespace Scene.DesktopPet.Interface
{
    /// <summary>
    /// デスクトップペット用のネイティブ小窓を制御する
    /// </summary>
    public interface IDesktopPetWindow
    {
        /// <summary>
        /// ネイティブ窓ハンドルを返す
        /// </summary>
        IntPtr Handle { get; }

        /// <summary>
        /// クリック透過中か
        /// </summary>
        bool ClickThrough { get; }

        /// <summary>
        /// 透明化に使う背景色を返す
        /// </summary>
        Color ChromaKeyColor { get; }

        /// <summary>
        /// ペット表示用の小窓モードへ入る
        /// </summary>
        /// <param name="width">窓幅</param>
        /// <param name="height">窓高</param>
        void EnterPetMode(int width, int height);

        /// <summary>
        /// ネイティブ透明化を再適用する
        /// </summary>
        void ApplyNativeChrome();

        /// <summary>
        /// クリック透過を切り替える
        /// </summary>
        /// <param name="enabled">透過するならtrue</param>
        void SetClickThrough(bool enabled);

        /// <summary>
        /// クリック透過をトグルする
        /// </summary>
        void ToggleClickThrough();

        /// <summary>
        /// 画面座標へ窓を移動する
        /// </summary>
        /// <param name="screenX">左上X</param>
        /// <param name="screenY">左上Y</param>
        void SetScreenPosition(int screenX, int screenY);

        /// <summary>
        /// 枠なし窓をタイトルバー相当のドラッグで移動する
        /// </summary>
        void BeginDragMove();

        /// <summary>
        /// 通常ウィンドウへ戻す
        /// </summary>
        void Restore();
    }
}
