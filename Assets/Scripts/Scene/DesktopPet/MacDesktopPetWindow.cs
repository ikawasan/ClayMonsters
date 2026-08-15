using System;
using Scene.DesktopPet.Interface;
using UnityEngine;

namespace Scene.DesktopPet
{
    /// <summary>
    /// macOS向けに枠なし透明最前面の小窓を制御する
    /// </summary>
    public sealed class MacDesktopPetWindow : IDesktopPetWindow
    {
        private bool isPetMode;
        private bool clickThrough;
        private int windowWidth = 330;
        private int windowHeight = 330;
        private FullScreenMode savedFullScreenMode;
        private int savedWidth;
        private int savedHeight;
        private bool nativeReady;
        private readonly bool stayOnTop;

        /// <summary>
        /// 重ね順を指定して生成する
        /// </summary>
        /// <param name="stayOnTop">最前面ならtrue最背面ならfalse</param>
        public MacDesktopPetWindow(bool stayOnTop)
        {
            this.stayOnTop = stayOnTop;
        }

        /// <inheritdoc/>
        public IntPtr Handle => IntPtr.Zero;

        /// <inheritdoc/>
        public bool ClickThrough => clickThrough;

        /// <inheritdoc/>
        public Color ChromaKeyColor => Color.clear;

        /// <inheritdoc/>
        public void EnterPetMode(int width, int height)
        {
            windowWidth = Mathf.Max(64, width);
            windowHeight = Mathf.Max(64, height);
#if UNITY_EDITOR
            Debug.LogWarning(
                "[MacDesktopPetWindow] Editorでは透明小窓を適用せずGameビュー内表示のみ行います");
            isPetMode = true;
            return;
#else
            savedFullScreenMode = Screen.fullScreenMode;
            savedWidth = Screen.width;
            savedHeight = Screen.height;
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(windowWidth, windowHeight, false);
            isPetMode = true;
#endif
        }

        /// <inheritdoc/>
        public void ApplyNativeChrome()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (!isPetMode)
            {
                return;
            }

            try
            {
                nativeReady = DesktopPetMacNative.CMPet_ConfigurePetWindow(
                    windowWidth,
                    windowHeight,
                    stayOnTop ? 1 : 0) != 0;
                if (nativeReady)
                {
                    DesktopPetMacNative.CMPet_SetClickThrough(clickThrough ? 1 : 0);
                }
                else
                {
                    Debug.LogWarning("[MacDesktopPetWindow] NSWindowを取得できませんでした");
                }
            }
            catch (Exception exception)
            {
                nativeReady = false;
                Debug.LogWarning(
                    "[MacDesktopPetWindow] ネイティブ透明化に失敗しました " + exception.Message);
            }
#endif
        }

        /// <inheritdoc/>
        public void SetClickThrough(bool enabled)
        {
            clickThrough = enabled;
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (!isPetMode || !nativeReady)
            {
                return;
            }

            try
            {
                DesktopPetMacNative.CMPet_SetClickThrough(enabled ? 1 : 0);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MacDesktopPetWindow] クリック透過の設定に失敗しました " + exception.Message);
            }
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

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (!nativeReady)
            {
                return;
            }

            try
            {
                DesktopPetMacNative.CMPet_SetScreenPosition(
                    screenX,
                    screenY,
                    windowWidth,
                    windowHeight);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MacDesktopPetWindow] 窓移動に失敗しました " + exception.Message);
            }
#endif
        }

        /// <inheritdoc/>
        public void BeginDragMove()
        {
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
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (nativeReady)
            {
                try
                {
                    DesktopPetMacNative.CMPet_RestoreWindow();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[MacDesktopPetWindow] 窓復元に失敗しました " + exception.Message);
                }
            }

            nativeReady = false;
            Screen.fullScreenMode = savedFullScreenMode;
            if (savedWidth > 0 && savedHeight > 0)
            {
                Screen.SetResolution(
                    savedWidth,
                    savedHeight,
                    savedFullScreenMode != FullScreenMode.Windowed);
            }
#endif
        }
    }
}
