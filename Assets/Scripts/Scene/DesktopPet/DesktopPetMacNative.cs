#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Scene.DesktopPet
{
    /// <summary>
    /// macOSネイティブ窓とメニューのPInvoke
    /// </summary>
    internal static class DesktopPetMacNative
    {
        [DllImport("DesktopPetMacWindow", EntryPoint = "CMPet_ConfigurePetWindow", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ConfigureBundle(int width, int height, int stayOnTop);

        [DllImport("__Internal", EntryPoint = "CMPet_ConfigurePetWindow", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ConfigureInternal(int width, int height, int stayOnTop);

        [DllImport("DesktopPetMacWindow", EntryPoint = "CMPet_SetScreenPosition", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetPositionBundle(int x, int y, int width, int height);

        [DllImport("__Internal", EntryPoint = "CMPet_SetScreenPosition", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetPositionInternal(int x, int y, int width, int height);

        [DllImport("DesktopPetMacWindow", EntryPoint = "CMPet_SetClickThrough", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetClickThroughBundle(int enabled);

        [DllImport("__Internal", EntryPoint = "CMPet_SetClickThrough", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetClickThroughInternal(int enabled);

        [DllImport("DesktopPetMacWindow", EntryPoint = "CMPet_RestoreWindow", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RestoreBundle();

        [DllImport("__Internal", EntryPoint = "CMPet_RestoreWindow", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RestoreInternal();

        [DllImport("DesktopPetMacWindow", EntryPoint = "CMPet_ShowContextMenu", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ShowMenuBundle(IntPtr quitUtf8, IntPtr launchUtf8);

        [DllImport("__Internal", EntryPoint = "CMPet_ShowContextMenu", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ShowMenuInternal(IntPtr quitUtf8, IntPtr launchUtf8);

        [DllImport("DesktopPetMacWindow", EntryPoint = "CMPet_GetWorkingAreaCount", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetWorkingAreaCountBundle();

        [DllImport("__Internal", EntryPoint = "CMPet_GetWorkingAreaCount", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetWorkingAreaCountInternal();

        [DllImport("DesktopPetMacWindow", EntryPoint = "CMPet_GetWorkingArea", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetWorkingAreaBundle(int index, out int x, out int y, out int w, out int h);

        [DllImport("__Internal", EntryPoint = "CMPet_GetWorkingArea", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetWorkingAreaInternal(int index, out int x, out int y, out int w, out int h);

        private static bool useInternal;

        /// <summary>
        /// 枠なし透明最前面を適用する
        /// </summary>
        public static int CMPet_ConfigurePetWindow(int width, int height, int stayOnTop)
        {
            return Invoke(
                () => ConfigureBundle(width, height, stayOnTop),
                () => ConfigureInternal(width, height, stayOnTop));
        }

        /// <summary>
        /// 窓位置を更新する
        /// </summary>
        public static void CMPet_SetScreenPosition(int x, int y, int width, int height)
        {
            Invoke(
                () =>
                {
                    SetPositionBundle(x, y, width, height);
                    return 0;
                },
                () =>
                {
                    SetPositionInternal(x, y, width, height);
                    return 0;
                });
        }

        /// <summary>
        /// クリック透過を切り替える
        /// </summary>
        public static void CMPet_SetClickThrough(int enabled)
        {
            Invoke(
                () =>
                {
                    SetClickThroughBundle(enabled);
                    return 0;
                },
                () =>
                {
                    SetClickThroughInternal(enabled);
                    return 0;
                });
        }

        /// <summary>
        /// 通常窓へ戻す
        /// </summary>
        public static void CMPet_RestoreWindow()
        {
            Invoke(
                () =>
                {
                    RestoreBundle();
                    return 0;
                },
                () =>
                {
                    RestoreInternal();
                    return 0;
                });
        }

        /// <summary>
        /// 右クリックメニューを出して選ばれたコマンドを返す
        /// </summary>
        public static int ShowContextMenu(string quitLabel, string launchLabel)
        {
            IntPtr quitPtr = AllocUtf8(quitLabel);
            IntPtr launchPtr = AllocUtf8(launchLabel);
            try
            {
                return Invoke(
                    () => ShowMenuBundle(quitPtr, launchPtr),
                    () => ShowMenuInternal(quitPtr, launchPtr));
            }
            finally
            {
                if (quitPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(quitPtr);
                }

                if (launchPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(launchPtr);
                }
            }
        }

        /// <summary>
        /// 作業領域の数を返す
        /// </summary>
        public static int CMPet_GetWorkingAreaCount()
        {
            return Invoke(GetWorkingAreaCountBundle, GetWorkingAreaCountInternal);
        }

        /// <summary>
        /// 指定インデックスの作業領域を返す
        /// </summary>
        public static bool CMPet_TryGetWorkingArea(int index, out int x, out int y, out int w, out int h)
        {
            int result = Invoke(
                () => GetWorkingAreaBundle(index, out x, out y, out w, out h),
                () => GetWorkingAreaInternal(index, out x, out y, out w, out h));
            if (result == 0)
            {
                x = 0;
                y = 0;
                w = 0;
                h = 0;
                return false;
            }

            return true;
        }

        private static int Invoke(Func<int> bundleCall, Func<int> internalCall)
        {
            if (useInternal)
            {
                return internalCall();
            }

            try
            {
                return bundleCall();
            }
            catch (DllNotFoundException)
            {
                useInternal = true;
                return internalCall();
            }
            catch (EntryPointNotFoundException)
            {
                useInternal = true;
                return internalCall();
            }
        }

        private static IntPtr AllocUtf8(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            Marshal.WriteByte(pointer, bytes.Length, 0);
            return pointer;
        }
    }
}
#endif
