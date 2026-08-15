using System;
using System.Runtime.InteropServices;
using Scene.DesktopPet.Interface;
using UnityEngine;

namespace Scene.DesktopPet
{
    /// <summary>
    /// Windows向けに枠なし透明最前面の小窓を制御する
    /// </summary>
    public sealed class WindowsDesktopPetWindow : IDesktopPetWindow
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
        private static readonly IntPtr HwndBottom = new IntPtr(1);

        private IntPtr hwnd;
        private bool isPetMode;
        private bool clickThrough;
        private readonly bool stayOnTop;
        private int savedStyle;
        private int savedExStyle;
        private FullScreenMode savedFullScreenMode;
        private int savedWidth;
        private int savedHeight;
        private Color32 chromaKey = new Color32(255, 0, 255, 255);

        /// <summary>
        /// 重ね順を指定して生成する
        /// </summary>
        /// <param name="stayOnTop">最前面ならtrue最背面ならfalse</param>
        public WindowsDesktopPetWindow(bool stayOnTop)
        {
            this.stayOnTop = stayOnTop;
        }

        /// <inheritdoc/>
        public IntPtr Handle => hwnd;

        /// <inheritdoc/>
        public bool ClickThrough => clickThrough;

        /// <inheritdoc/>
        public Color ChromaKeyColor => new Color(chromaKey.r / 255f, chromaKey.g / 255f, chromaKey.b / 255f, 1f);

        /// <inheritdoc/>
        public void EnterPetMode(int width, int height)
        {
#if UNITY_EDITOR
            Debug.LogWarning(
                "[WindowsDesktopPetWindow] Editorでは透明小窓を適用せずGameビュー内表示のみ行います");
            isPetMode = true;
            return;
#elif UNITY_STANDALONE_WIN
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
                stayOnTop ? HwndTopmost : HwndBottom,
                0,
                0,
                width,
                height,
                SwpNoactivate | SwpShowwindow);
            isPetMode = true;
#else
            Debug.LogError("[WindowsDesktopPetWindow] Windows以外では使えません");
            isPetMode = true;
#endif
        }

        /// <inheritdoc/>
        public void ApplyNativeChrome()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!isPetMode || hwnd == IntPtr.Zero)
            {
                return;
            }

            ApplyPetExStyle();
#endif
        }

        /// <inheritdoc/>
        public void SetClickThrough(bool enabled)
        {
            clickThrough = enabled;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!isPetMode || hwnd == IntPtr.Zero)
            {
                return;
            }

            ApplyPetExStyle();
#endif
        }

        /// <inheritdoc/>
        public void ToggleClickThrough()
        {
            SetClickThrough(!clickThrough);
        }

        /// <inheritdoc/>
        public void SetScreenPosition(int screenX, int screenY)
        {
            if (!isPetMode)
            {
                return;
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
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
                stayOnTop ? HwndTopmost : HwndBottom,
                screenX,
                screenY,
                0,
                0,
                SwpNosize | SwpNoactivate | SwpShowwindow);
#endif
        }

        /// <inheritdoc/>
        public void BeginDragMove()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
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

        /// <inheritdoc/>
        public void Restore()
        {
            if (!isPetMode)
            {
                return;
            }

            isPetMode = false;
            clickThrough = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
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
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            int exStyle = WsExLayered | WsExToolwindow | WsExNoactivate;
            if (stayOnTop)
            {
                exStyle |= WsExTopmost;
            }

            if (clickThrough)
            {
                exStyle |= WsExTransparent;
            }

            SetWindowLong(hwnd, GwlExstyle, exStyle);
            SetLayeredWindowAttributes(hwnd, ColorToColorRef(chromaKey), 0, LwaColorkey);
            SetWindowPos(
                hwnd,
                stayOnTop ? HwndTopmost : HwndBottom,
                0,
                0,
                0,
                0,
                SwpNosize | SwpNoactivate | SwpShowwindow);
#endif
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
