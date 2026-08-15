using System.Drawing.Drawing2D;
using System.Reflection;

namespace ClayMonstersPet;

/// <summary>
/// デスクトップ上のサッカーボール表示
/// </summary>
internal sealed class PetBallForm : Form
{
    public const int BallSize = 56;
    private const int PixelGrid = 24;

    private readonly Bitmap ballImage;
    private PointF currentPos;
    private PointF moveFrom;
    private PointF moveTo;
    private float moveElapsed;
    private float moveDuration;
    private bool flying;
    private float spin;
    private bool isDragging;
    private Point dragGrabOffset;
    private Point mouseDownClient;
    private Action? dragStarted;
    private Action? dragEnded;

    public PetBallForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(BallSize, BallSize);
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
        Text = "ClayMonstersPetBall";
        Cursor = Cursors.Hand;
        PetWindowOrder.Apply(this);
        ballImage = LoadBallImage();

        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        currentPos = new PointF(
            work.Left + (work.Width - BallSize) * 0.5f,
            work.Top + (work.Height - BallSize) * 0.5f);
        Location = Point.Round(currentPos);
    }

    public PointF Position => currentPos;

    public PointF Center => new(
        currentPos.X + BallSize * 0.5f,
        currentPos.Y + BallSize * 0.5f);

    public bool IsFlying => flying;

    public bool IsDragging => isDragging;

    /// <summary>
    /// ボールのドラッグ開始と終了を通知する
    /// </summary>
    public void SetDragCallbacks(Action? started, Action? ended)
    {
        dragStarted = started;
        dragEnded = ended;
    }

    /// <summary>
    /// ボールを指定位置へ置く
    /// </summary>
    public void PlaceAt(PointF topLeft)
    {
        flying = false;
        currentPos = ClampToWorkArea(topLeft);
        Location = Point.Round(currentPos);
        Invalidate();
    }

    /// <summary>
    /// ボールを指定位置へ飛ばす
    /// </summary>
    public void KickTo(PointF topLeft, float durationSeconds)
    {
        if (isDragging)
        {
            return;
        }

        moveFrom = currentPos;
        moveTo = ClampToWorkArea(topLeft);
        moveElapsed = 0f;
        moveDuration = Math.Max(0.25f, durationSeconds);
        flying = true;
    }

    /// <summary>
    /// 飛行中の補間を進める
    /// </summary>
    public void Tick(float dt)
    {
        if (!flying || isDragging)
        {
            return;
        }

        moveElapsed += dt;
        float t = Math.Clamp(moveElapsed / moveDuration, 0f, 1f);
        float eased = t * t * (3f - 2f * t);
        currentPos = new PointF(
            Lerp(moveFrom.X, moveTo.X, eased),
            Lerp(moveFrom.Y, moveTo.Y, eased));
        spin += dt * 180f;
        Location = Point.Round(currentPos);
        Invalidate();
        if (t >= 1f)
        {
            flying = false;
            currentPos = moveTo;
            Location = Point.Round(currentPos);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        mouseDownClient = e.Location;
        dragGrabOffset = e.Location;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!Capture || (MouseButtons & MouseButtons.Left) == 0)
        {
            return;
        }

        int dx = e.X - mouseDownClient.X;
        int dy = e.Y - mouseDownClient.Y;
        if (!isDragging && ((dx * dx) + (dy * dy)) >= 16)
        {
            isDragging = true;
            flying = false;
            dragStarted?.Invoke();
        }

        if (!isDragging)
        {
            return;
        }

        Point screen = PointToScreen(e.Location);
        PlaceAt(new PointF(screen.X - dragGrabOffset.X, screen.Y - dragGrabOffset.Y));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        bool wasDragging = isDragging;
        isDragging = false;
        Capture = false;
        if (wasDragging)
        {
            dragEnded?.Invoke();
        }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080;
            cp.ExStyle |= 0x00000008;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Magenta);
        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
        float cx = BallSize * 0.5f;
        float cy = BallSize * 0.5f;
        e.Graphics.TranslateTransform(cx, cy);
        e.Graphics.RotateTransform(MathF.Floor(spin / 45f) * 45f);
        e.Graphics.DrawImage(ballImage, -cx, -cy, BallSize, BallSize);
        e.Graphics.ResetTransform();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ballImage.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Bitmap LoadBallImage()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream("ClayMonstersPet.soccer_ball.png");
        if (stream == null)
        {
            Bitmap fallback = new(BallSize, BallSize);
            using Graphics g = Graphics.FromImage(fallback);
            g.Clear(Color.Magenta);
            return fallback;
        }

        using Bitmap loaded = new(stream);
        return Pixelate(loaded);
    }

    private static Bitmap Pixelate(Bitmap source)
    {
        Bitmap pixelated = new(PixelGrid, PixelGrid, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(pixelated);
        graphics.Clear(Color.Magenta);
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(source, 0, 0, PixelGrid, PixelGrid);
        for (int y = 0; y < PixelGrid; y++)
        {
            for (int x = 0; x < PixelGrid; x++)
            {
                Color pixel = pixelated.GetPixel(x, y);
                if (pixel.A < 128)
                {
                    pixelated.SetPixel(x, y, Color.Magenta);
                }
                else
                {
                    pixelated.SetPixel(x, y, Color.FromArgb(255, pixel.R, pixel.G, pixel.B));
                }
            }
        }

        return pixelated;
    }

    private static PointF ClampToWorkArea(PointF point)
    {
        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        float x = Math.Clamp(point.X, work.Left, work.Right - BallSize);
        float y = Math.Clamp(point.Y, work.Top, work.Bottom - BallSize);
        return new PointF(x, y);
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
