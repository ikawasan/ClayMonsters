using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Scene.DesktopPet
{
    /// <summary>
    /// 複数モニターの作業領域を列挙する
    /// </summary>
    internal static class DesktopPetScreenBounds
    {
        /// <summary>
        /// 利用可能な作業領域一覧を返す
        /// </summary>
        public static RectInt[] GetWorkingAreas()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            List<RectInt> areas = new List<RectInt>(4);
            EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdc, ref NativeRect _, IntPtr __) =>
                {
                    MonitorInfo info = new MonitorInfo();
                    info.cbSize = Marshal.SizeOf<MonitorInfo>();
                    if (!GetMonitorInfo(hMonitor, ref info))
                    {
                        return true;
                    }

                    NativeRect work = info.rcWork;
                    int width = work.Right - work.Left;
                    int height = work.Bottom - work.Top;
                    if (width > 0 && height > 0)
                    {
                        areas.Add(new RectInt(work.Left, work.Top, width, height));
                    }

                    return true;
                },
                IntPtr.Zero);

            if (areas.Count > 0)
            {
                return areas.ToArray();
            }
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            int count = DesktopPetMacNative.CMPet_GetWorkingAreaCount();
            if (count > 0)
            {
                List<RectInt> areas = new List<RectInt>(count);
                for (int i = 0; i < count; i++)
                {
                    if (!DesktopPetMacNative.CMPet_TryGetWorkingArea(
                            i,
                            out int x,
                            out int y,
                            out int w,
                            out int h))
                    {
                        continue;
                    }

                    if (w > 0 && h > 0)
                    {
                        areas.Add(new RectInt(x, y, w, h));
                    }
                }

                if (areas.Count > 0)
                {
                    return areas.ToArray();
                }
            }
#endif
            int fallbackW = Mathf.Max(1, Display.main.systemWidth);
            int fallbackH = Mathf.Max(1, Display.main.systemHeight);
            return new[] { new RectInt(0, 0, fallbackW, fallbackH) };
        }

        /// <summary>
        /// ランダムな画面上の窓左上を返す
        /// </summary>
        public static Vector2Int RandomTopLeft(int windowWidth, int windowHeight, int margin = 24)
        {
            RectInt[] areas = GetWorkingAreas();
            RectInt work = areas[UnityEngine.Random.Range(0, areas.Length)];
            int minX = work.x + margin;
            int minY = work.y + margin;
            int maxX = Mathf.Max(minX, work.xMax - windowWidth - margin);
            int maxY = Mathf.Max(minY, work.yMax - windowHeight - margin);
            return new Vector2Int(
                UnityEngine.Random.Range(minX, maxX + 1),
                UnityEngine.Random.Range(minY, maxY + 1));
        }

        /// <summary>
        /// 主作業領域の中央付近の窓左上を返す
        /// </summary>
        public static Vector2Int CenterTopLeft(int windowWidth, int windowHeight)
        {
            RectInt[] areas = GetWorkingAreas();
            RectInt work = areas[0];
            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i].x <= 0 && areas[i].y <= 0)
                {
                    work = areas[i];
                    break;
                }
            }

            int x = Mathf.Clamp(
                work.x + ((work.width - windowWidth) / 2),
                work.x,
                Mathf.Max(work.x, work.xMax - windowWidth));
            int y = Mathf.Clamp(
                work.y + ((work.height - windowHeight) / 2),
                work.y,
                Mathf.Max(work.y, work.yMax - windowHeight));
            return new Vector2Int(x, y);
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private delegate bool MonitorEnumProc(
            IntPtr hMonitor,
            IntPtr hdcMonitor,
            ref NativeRect lprcMonitor,
            IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr lprcClip,
            MonitorEnumProc lpfnEnum,
            IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int cbSize;
            public NativeRect rcMonitor;
            public NativeRect rcWork;
            public uint dwFlags;
        }
#endif
    }
}
