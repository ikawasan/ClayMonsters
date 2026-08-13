using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Scene.DesktopPet
{
    /// <summary>
    /// Windows向けに枠なし透明最前面の小窓を制御する
    /// </summary>
    public sealed class WindowsDesktopPetWindow
    {
        private const int GwlExstyle = -20;
        private const int GwlStyle = -16;
        private const uint WsPopup = 0x80000000;
        private const int WsExLayered = 0x00080000;
        private const int WsExTopmost = 0x00000008;
        private const int WsExToolwindow = 0x00000080;
        private const int WsExTransparent = 0x00000020;
        private const int WsExNoactivate = 0x08000000;
        private const uint LwaColorkey = 0x00000001;
        private const uint SwpNosize = 0x0001;
        private const uint SwpNozorder = 0x0004;
        private const uint SwpNoactivate = 0x0010;
        private const uint SwpShowwindow = 0x0040;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr HwndNotopmost = new IntPtr(-2);

        private IntPtr hwnd;
        private bool isPetMode;
        private bool clickThrough;
        private int savedStyle;
        private int savedExStyle;
        private FullScreenMode savedFullScreenMode;
        private int savedWidth;
        private int savedHeight;
        private Color32 chromaKey = new Color32(255, 0, 255, 255);

        /// <summary>
        /// ウィンドウハンドル
        /// </summary>
        public IntPtr Handle => hwnd;

        /// <summary>
        /// クリック透過中か
        /// </summary>
        public bool ClickThrough => clickThrough;

        /// <summary>
        /// クロマキー色(完全一致ピクセルが透明になる)
        /// </summary>
        public Color ChromaKeyColor => new Color(chromaKey.r / 255f, chromaKey.g / 255f, chromaKey.b / 255f, 1f);

        /// <summary>
        /// ペット表示用の小窓モードへ入る
        /// </summary>
        /// <param name="width">窓幅</param>
        /// <param name="height">窓高</param>
        public void EnterPetMode(int width, int height)
        {
#if UNITY_EDITOR
            // Editorでは透明小窓も解像度変更もしない(Gameビューのレターボックス白帯を避ける)
            Debug.LogWarning(
                "[WindowsDesktopPetWindow] Editorでは透明小窓を適用せずGameビュー内表示のみ行います");
            isPetMode = true;
            return;
#else
            savedFullScreenMode = Screen.fullScreenMode;
            savedWidth = Screen.width;
            savedHeight = Screen.height;
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(width, height, false);

            hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero)
            {
                Debug.LogError("[WindowsDesktopPetWindow] ウィンドウハンドルを取得できません");
                return;
            }

            savedStyle = GetWindowLong(hwnd, GwlStyle);
            savedExStyle = GetWindowLong(hwnd, GwlExstyle);
            ApplyPetExStyle();
            SetWindowLong(hwnd, GwlStyle, unchecked((int)WsPopup));
            SetLayeredWindowAttributes(hwnd, ColorToColorRef(chromaKey), 0, LwaColorkey);
            SetWindowPos(
                hwnd,
                HwndTopmost,
                0,
                0,
                width,
                height,
                SwpNoactivate | SwpShowwindow);
            isPetMode = true;
#endif
        }

        /// <summary>
        /// クリック透過を切り替える
        /// </summary>
        public void SetClickThrough(bool enabled)
        {
            clickThrough = enabled;
#if !UNITY_EDITOR
            if (!isPetMode || hwnd == IntPtr.Zero)
            {
                return;
            }

            ApplyPetExStyle();
#endif
        }

        /// <summary>
        /// クリック透過をトグルする
        /// </summary>
        public void ToggleClickThrough()
        {
            SetClickThrough(!clickThrough);
        }

        /// <summary>
        /// 画面座標へ窓を移動する
        /// </summary>
        /// <param name="screenX">左上X</param>
        /// <param name="screenY">左上Y</param>
        public void SetScreenPosition(int screenX, int screenY)
        {
            if (!isPetMode)
            {
                return;
            }

#if UNITY_EDITOR
            return;
#else
            if (hwnd == IntPtr.Zero)
            {
                hwnd = GetActiveWindow();
            }

            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(
                hwnd,
                HwndTopmost,
                screenX,
                screenY,
                0,
                0,
                SwpNosize | SwpNoactivate | SwpShowwindow);
#endif
        }

        /// <summary>
        /// 枠なし窓をタイトルバー相当のドラッグで移動する
        /// </summary>
        public void BeginDragMove()
        {
#if UNITY_EDITOR
            return;
#else
            if (!isPetMode || clickThrough)
            {
                return;
            }

            if (hwnd == IntPtr.Zero)
            {
                hwnd = GetActiveWindow();
            }

            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(hwnd, 0x00A1, new IntPtr(2), IntPtr.Zero);
#endif
        }

        /// <summary>
        /// 通常ウィンドウへ戻す
        /// </summary>
        public void Restore()
        {
            if (!isPetMode)
            {
                return;
            }

            isPetMode = false;
            clickThrough = false;
#if UNITY_EDITOR
            return;
#else
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowLong(hwnd, GwlStyle, savedStyle);
            SetWindowLong(hwnd, GwlExstyle, savedExStyle);
            SetWindowPos(
                hwnd,
                HwndNotopmost,
                0,
                0,
                0,
                0,
                SwpNosize | SwpNoactivate | SwpNozorder);
            Screen.fullScreenMode = savedFullScreenMode;
            if (savedWidth > 0 && savedHeight > 0)
            {
                Screen.SetResolution(savedWidth, savedHeight, savedFullScreenMode != FullScreenMode.Windowed);
            }
#endif
        }

        private void ApplyPetExStyle()
        {
            int exStyle = WsExLayered | WsExTopmost | WsExToolwindow | WsExNoactivate;
            if (clickThrough)
            {
                exStyle |= WsExTransparent;
            }

            SetWindowLong(hwnd, GwlExstyle, exStyle);
            SetLayeredWindowAttributes(hwnd, ColorToColorRef(chromaKey), 0, LwaColorkey);
        }

        private static uint ColorToColorRef(Color32 color)
        {
            return (uint)(color.r | (color.g << 8) | (color.b << 16));
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(
            IntPtr hwnd,
            uint crKey,
            byte bAlpha,
            uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
