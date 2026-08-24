using System;
using System.Runtime.InteropServices;
using System.Text;
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
        private const int GwOwner = 4;
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
        private const uint SwpFramechanged = 0x0020;
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

            // SetResolution前にハンドルを掴むフォーカス喪失でGetActiveWindowが空になるのを避ける
            hwnd = FindMainWindowHandle();
            if (hwnd == IntPtr.Zero)
            {
                hwnd = GetActiveWindow();
            }

            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(width, height, false);

            IntPtr resolved = FindMainWindowHandle();
            if (resolved != IntPtr.Zero)
            {
                hwnd = resolved;
            }
            else if (hwnd == IntPtr.Zero)
            {
                hwnd = GetActiveWindow();
            }

            if (hwnd == IntPtr.Zero)
            {
                Debug.LogError(
                    "[WindowsDesktopPetWindow] ウィンドウハンドルを取得できません外部ペット起動を優先してください");
                return;
            }

            savedStyle = GetWindowLong(hwnd, GwlStyle);
            savedExStyle = GetWindowLong(hwnd, GwlExstyle);
            ApplyPetWindowChrome(width, height, resize: true);
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
            if (!isPetMode)
            {
                return;
            }

            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                hwnd = FindMainWindowHandle();
            }

            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            ApplyPetWindowChrome(0, 0, resize: false);
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

            ApplyPetWindowChrome(0, 0, resize: false);
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
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                hwnd = FindMainWindowHandle();
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

            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                hwnd = FindMainWindowHandle();
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
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
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
                SwpNosize | SwpNoactivate | SwpNozorder | SwpFramechanged);
            Screen.fullScreenMode = savedFullScreenMode;
            if (savedWidth > 0 && savedHeight > 0)
            {
                Screen.SetResolution(savedWidth, savedHeight, savedFullScreenMode != FullScreenMode.Windowed);
            }
#endif
        }

        private void ApplyPetWindowChrome(int width, int height, bool resize)
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

            SetWindowLong(hwnd, GwlStyle, unchecked((int)WsPopup));
            SetWindowLong(hwnd, GwlExstyle, exStyle);
            SetLayeredWindowAttributes(hwnd, ColorToColorRef(chromaKey), 0, LwaColorkey);

            uint flags = SwpNoactivate | SwpShowwindow | SwpFramechanged;
            int cx = 0;
            int cy = 0;
            if (!resize)
            {
                flags |= SwpNosize;
            }
            else
            {
                cx = width;
                cy = height;
            }

            SetWindowPos(
                hwnd,
                stayOnTop ? HwndTopmost : HwndBottom,
                0,
                0,
                cx,
                cy,
                flags);
#endif
        }

        private static IntPtr FindMainWindowHandle()
        {
            int currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            IntPtr found = IntPtr.Zero;
            EnumWindows(
                (hWnd, _) =>
                {
                    GetWindowThreadProcessId(hWnd, out uint windowPid);
                    if ((int)windowPid != currentPid)
                    {
                        return true;
                    }

                    if (!IsWindowVisible(hWnd))
                    {
                        return true;
                    }

                    if (GetWindow(hWnd, GwOwner) != IntPtr.Zero)
                    {
                        return true;
                    }

                    if (!IsUnityPlayerWindow(hWnd))
                    {
                        return true;
                    }

                    found = hWnd;
                    return false;
                },
                IntPtr.Zero);
            return found;
        }

        private static bool IsUnityPlayerWindow(IntPtr hWnd)
        {
            var className = new StringBuilder(256);
            if (GetClassName(hWnd, className, className.Capacity) <= 0)
            {
                return false;
            }

            string name = className.ToString();
            return name.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(name, "UnityWndClass", StringComparison.Ordinal);
        }

        private static uint ColorToColorRef(Color32 color)
        {
            return (uint)(color.r | (color.g << 8) | (color.b << 16));
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

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
