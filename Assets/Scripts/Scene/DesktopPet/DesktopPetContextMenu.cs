using System;
using System.Runtime.InteropServices;
using Localization;

namespace Scene.DesktopPet
{
    /// <summary>
    /// Windows向けデスクトップペットのクリックメニュー
    /// </summary>
    public sealed class DesktopPetContextMenu
    {
        public const int CommandQuitDesktopPet = 1;
        public const int CommandLaunchClayMonsters = 2;

        private const uint MfString = 0x00000000;
        private const uint TpmLeftalign = 0x0000;
        private const uint TpmRightbutton = 0x0002;
        private const uint TpmReturncmd = 0x0100;

        /// <summary>
        /// メニューを出して選ばれたコマンドを返す(0はキャンセル)
        /// </summary>
        public int Show(IntPtr hwnd)
        {
#if UNITY_EDITOR || !UNITY_STANDALONE_WIN
            return 0;
#else
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
                string quitLabel = LocalizedText.GetOrFallback(
                    GameTextKeys.DesktopPetQuitMenu,
                    "デスクトップペットを終了");
                string launchLabel = LocalizedText.GetOrFallback(
                    GameTextKeys.DesktopPetLaunchGameMenu,
                    "ClayMonstersを起動");
                AppendMenu(menu, MfString, new UIntPtr(CommandQuitDesktopPet), quitLabel);
                AppendMenu(menu, MfString, new UIntPtr(CommandLaunchClayMonsters), launchLabel);

                GetCursorPos(out Point point);
                SetForegroundWindow(hwnd);
                int command = TrackPopupMenu(
                    menu,
                    TpmLeftalign | TpmRightbutton | TpmReturncmd,
                    point.X,
                    point.Y,
                    0,
                    hwnd,
                    IntPtr.Zero);
                return command;
            }
            finally
            {
                DestroyMenu(menu);
            }
#endif
        }

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
    }
}
