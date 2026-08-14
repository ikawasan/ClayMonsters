using System;
#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif
using Localization;

namespace Scene.DesktopPet
{
    /// <summary>
    /// デスクトップペットのクリックメニュー
    /// </summary>
    public sealed class DesktopPetContextMenu
    {
        public const int CommandQuitDesktopPet = 1;
        public const int CommandLaunchClayMonsters = 2;

#if UNITY_STANDALONE_WIN
        private const uint MfString = 0x00000000;
        private const uint TpmLeftalign = 0x0000;
        private const uint TpmRightbutton = 0x0002;
        private const uint TpmReturncmd = 0x0100;
#endif

        /// <summary>
        /// メニューを出して選ばれたコマンドを返す(0はキャンセル)
        /// </summary>
        public int Show(IntPtr hwnd)
        {
#if UNITY_EDITOR
            return 0;
#elif UNITY_STANDALONE_OSX
            return ShowMacMenu();
#elif UNITY_STANDALONE_WIN
            if (hwnd == IntPtr.Zero)
            {
                return 0;
            }

            IntPtr menu = CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                ResolveMenuLabels(out string quitLabel, out string launchLabel);
                AppendMenu(menu, MfString, new UIntPtr(CommandQuitDesktopPet), quitLabel);
                AppendMenu(menu, MfString, new UIntPtr(CommandLaunchClayMonsters), launchLabel);

                GetCursorPos(out Point point);
                SetForegroundWindow(hwnd);
                return TrackPopupMenu(
                    menu,
                    TpmLeftalign | TpmRightbutton | TpmReturncmd,
                    point.X,
                    point.Y,
                    0,
                    hwnd,
                    IntPtr.Zero);
            }
            finally
            {
                DestroyMenu(menu);
            }
#else
            return 0;
#endif
        }

        private static void ResolveMenuLabels(out string quitLabel, out string launchLabel)
        {
            quitLabel = LocalizedText.GetOrFallback(
                GameTextKeys.DesktopPetQuitMenu,
                "デスクトップペットを終了");
            launchLabel = LocalizedText.GetOrFallback(
                GameTextKeys.DesktopPetLaunchGameMenu,
                "ClayMonstersを起動");
        }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        private static int ShowMacMenu()
        {
            ResolveMenuLabels(out string quitLabel, out string launchLabel);
            try
            {
                return DesktopPetMacNative.ShowContextMenu(quitLabel, launchLabel);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[DesktopPetContextMenu] Macメニューの表示に失敗しました " + exception.Message);
                return 0;
            }
        }
#endif

#if UNITY_STANDALONE_WIN
        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(
            IntPtr hMenu,
            uint uFlags,
            UIntPtr uIDNewItem,
            string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(
            IntPtr hMenu,
            uint uFlags,
            int x,
            int y,
            int nReserved,
            IntPtr hWnd,
            IntPtr prcRect);
#endif
    }
}
