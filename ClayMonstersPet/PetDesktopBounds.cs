using System.Drawing;

namespace ClayMonstersPet;

/// <summary>
/// 複数モニターの作業領域をまとめて扱う
/// </summary>
internal static class PetDesktopBounds
{
    /// <summary>
    /// 全モニターの作業領域を返す
    /// </summary>
    public static Rectangle[] GetWorkingAreas()
    {
        Screen[] screens = Screen.AllScreens;
        if (screens == null || screens.Length == 0)
        {
            return new[] { new Rectangle(0, 0, 1280, 720) };
        }

        List<Rectangle> areas = new(screens.Length);
        for (int i = 0; i < screens.Length; i++)
        {
            Screen? screen = screens[i];
            if (screen == null)
            {
                continue;
            }

            Rectangle work = screen.WorkingArea;
            if (work.Width > 0 && work.Height > 0)
            {
                areas.Add(work);
            }
        }

        if (areas.Count == 0)
        {
            return new[] { new Rectangle(0, 0, 1280, 720) };
        }

        return areas.ToArray();
    }

    /// <summary>
    /// ランダムな画面上へ窓左上座標を返す
    /// </summary>
    public static PointF RandomTopLeft(Random random, int windowWidth, int windowHeight, int margin = 24)
    {
        Rectangle[] areas = GetWorkingAreas();
        Rectangle work = areas[random.Next(areas.Length)];
        return RandomTopLeftIn(work, random, windowWidth, windowHeight, margin);
    }

    /// <summary>
    /// 指定点から十分遠いランダムな窓左上座標を返す
    /// </summary>
    public static PointF DistantTopLeft(
        Random random,
        PointF fromTopLeft,
        int windowWidth,
        int windowHeight,
        float minDistance,
        int margin = 8)
    {
        PointF best = fromTopLeft;
        float bestDist = 0f;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            PointF candidate = RandomTopLeft(random, windowWidth, windowHeight, margin);
            float dx = candidate.X - fromTopLeft.X;
            float dy = candidate.Y - fromTopLeft.Y;
            float dist = MathF.Sqrt((dx * dx) + (dy * dy));
            if (dist >= minDistance)
            {
                return candidate;
            }

            if (dist > bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// 窓全体がいずれかの画面内へ収まるよう左上を補正する
    /// </summary>
    public static PointF ClampTopLeft(PointF topLeft, int windowWidth, int windowHeight)
    {
        float centerX = topLeft.X + (windowWidth * 0.5f);
        float centerY = topLeft.Y + (windowHeight * 0.5f);
        Rectangle work = FindBestWorkingArea(centerX, centerY, windowWidth, windowHeight);
        float maxX = Math.Max(work.Left, work.Right - windowWidth);
        float maxY = Math.Max(work.Top, work.Bottom - windowHeight);
        float x = Math.Clamp(topLeft.X, work.Left, maxX);
        float y = Math.Clamp(topLeft.Y, work.Top, maxY);
        return new PointF(x, y);
    }

    /// <summary>
    /// 画面間移動中に窓が一時的にまたがっても通過できるよう左上を補正する
    /// </summary>
    public static PointF ClampTopLeftForTransit(PointF topLeft, int windowWidth, int windowHeight)
    {
        if (WindowFitsAnyWorkingArea(topLeft, windowWidth, windowHeight))
        {
            return topLeft;
        }

        Rectangle virtualBounds = GetVirtualDesktopBounds();
        float maxX = Math.Max(virtualBounds.Left, virtualBounds.Right - windowWidth);
        float maxY = Math.Max(virtualBounds.Top, virtualBounds.Bottom - windowHeight);
        return new PointF(
            Math.Clamp(topLeft.X, virtualBounds.Left, maxX),
            Math.Clamp(topLeft.Y, virtualBounds.Top, maxY));
    }

    /// <summary>
    /// 窓全体がいずれかの作業領域へ収まるか返す
    /// </summary>
    public static bool WindowFitsAnyWorkingArea(PointF topLeft, int windowWidth, int windowHeight)
    {
        Rectangle[] areas = GetWorkingAreas();
        for (int i = 0; i < areas.Length; i++)
        {
            Rectangle work = areas[i];
            if (topLeft.X >= work.Left
                && topLeft.Y >= work.Top
                && topLeft.X + windowWidth <= work.Right
                && topLeft.Y + windowHeight <= work.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 全モニターを包む仮想デスクトップ矩形を返す
    /// </summary>
    public static Rectangle GetVirtualDesktopBounds()
    {
        Rectangle[] areas = GetWorkingAreas();
        int left = areas[0].Left;
        int top = areas[0].Top;
        int right = areas[0].Right;
        int bottom = areas[0].Bottom;
        for (int i = 1; i < areas.Length; i++)
        {
            left = Math.Min(left, areas[i].Left);
            top = Math.Min(top, areas[i].Top);
            right = Math.Max(right, areas[i].Right);
            bottom = Math.Max(bottom, areas[i].Bottom);
        }

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    /// <summary>
    /// 点が属する作業領域の中心を返す
    /// </summary>
    public static PointF WorkingAreaCenterNear(PointF point, int windowWidth, int windowHeight)
    {
        Rectangle work = FindBestWorkingArea(point.X, point.Y, windowWidth, windowHeight);
        return new PointF(
            work.Left + (work.Width * 0.5f),
            work.Top + (work.Height * 0.5f));
    }

    /// <summary>
    /// 点が属する作業領域を返す
    /// </summary>
    public static Rectangle FindBestWorkingArea(
        float centerX,
        float centerY,
        int windowWidth,
        int windowHeight)
    {
        Rectangle[] areas = GetWorkingAreas();
        for (int i = 0; i < areas.Length; i++)
        {
            Rectangle work = areas[i];
            if (work.Width < windowWidth || work.Height < windowHeight)
            {
                continue;
            }

            if (centerX >= work.Left
                && centerX < work.Right
                && centerY >= work.Top
                && centerY < work.Bottom)
            {
                return work;
            }
        }

        Rectangle best = areas[0];
        float bestDist = float.MaxValue;
        bool foundFit = false;
        for (int i = 0; i < areas.Length; i++)
        {
            Rectangle work = areas[i];
            bool fits = work.Width >= windowWidth && work.Height >= windowHeight;
            if (!fits && foundFit)
            {
                continue;
            }

            float cx = work.Left + (work.Width * 0.5f);
            float cy = work.Top + (work.Height * 0.5f);
            float dx = cx - centerX;
            float dy = cy - centerY;
            float dist = (dx * dx) + (dy * dy);
            if (fits && !foundFit)
            {
                foundFit = true;
                bestDist = dist;
                best = work;
                continue;
            }

            if (dist < bestDist)
            {
                bestDist = dist;
                best = work;
            }
        }

        return best;
    }

    /// <summary>
    /// 全画面を包む矩形の大きい辺を返す
    /// </summary>
    public static float MaxVirtualSpan()
    {
        Rectangle[] areas = GetWorkingAreas();
        int left = areas[0].Left;
        int top = areas[0].Top;
        int right = areas[0].Right;
        int bottom = areas[0].Bottom;
        for (int i = 1; i < areas.Length; i++)
        {
            left = Math.Min(left, areas[i].Left);
            top = Math.Min(top, areas[i].Top);
            right = Math.Max(right, areas[i].Right);
            bottom = Math.Max(bottom, areas[i].Bottom);
        }

        return Math.Max(right - left, bottom - top);
    }

    private static PointF RandomTopLeftIn(
        Rectangle work,
        Random random,
        int windowWidth,
        int windowHeight,
        int margin)
    {
        int minX = work.Left + margin;
        int minY = work.Top + margin;
        int maxX = Math.Max(minX, work.Right - windowWidth - margin);
        int maxY = Math.Max(minY, work.Bottom - windowHeight - margin);
        return new PointF(
            random.Next(minX, maxX + 1),
            random.Next(minY, maxY + 1));
    }
}
