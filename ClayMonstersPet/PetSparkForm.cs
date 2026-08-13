using System.Drawing.Drawing2D;

namespace ClayMonstersPet;

/// <summary>
/// 簡易戦闘の火花をキャラ同士の中間へ出す透明窓
/// </summary>
internal sealed class PetSparkForm : Form
{
    public const int OverlaySize = 160;

    private readonly List<SparkParticle> sparks = new(48);
    private readonly Random random = new();

    public PetSparkForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(OverlaySize, OverlaySize);
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
        Text = "ClayMonstersPetSpark";
        Enabled = false;
        Location = new Point(-OverlaySize, -OverlaySize);
    }

    /// <summary>
    /// 指定ワールド座標を中心に火花を出す
    /// </summary>
    public void BurstAt(PointF worldCenter)
    {
        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        float left = Math.Clamp(
            worldCenter.X - (OverlaySize * 0.5f),
            work.Left,
            work.Right - OverlaySize);
        float top = Math.Clamp(
            worldCenter.Y - (OverlaySize * 0.5f),
            work.Top,
            work.Bottom - OverlaySize);
        Location = Point.Round(new PointF(left, top));

        float originX = OverlaySize * 0.5f;
        float originY = OverlaySize * 0.5f;
        for (int i = 0; i < 28; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2.0);
            float speed = 80f + (float)random.NextDouble() * 200f;
            sparks.Add(new SparkParticle
            {
                X = originX,
                Y = originY,
                Vx = MathF.Cos(angle) * speed,
                Vy = MathF.Sin(angle) * speed - 50f,
                Life = 0.28f + (float)random.NextDouble() * 0.35f,
                MaxLife = 0.65f,
                Size = 3.5f + (float)random.NextDouble() * 5f,
                Color = random.Next(0, 4) switch
                {
                    0 => Color.FromArgb(255, 255, 255, 255),
                    1 => Color.FromArgb(255, 255, 245, 90),
                    2 => Color.FromArgb(255, 255, 200, 70),
                    _ => Color.FromArgb(255, 255, 160, 40)
                }
            });
        }

        if (!Visible)
        {
            Show();
        }

        BringToFront();
        Invalidate();
    }

    /// <summary>
    /// 火花の寿命と移動を進める
    /// </summary>
    public void Tick(float dt)
    {
        if (sparks.Count == 0)
        {
            if (Visible)
            {
                Hide();
            }

            return;
        }

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            SparkParticle spark = sparks[i];
            spark.Life -= dt;
            spark.X += spark.Vx * dt;
            spark.Y += spark.Vy * dt;
            spark.Vy += 180f * dt;
            if (spark.Life <= 0f)
            {
                sparks.RemoveAt(i);
            }
            else
            {
                sparks[i] = spark;
            }
        }

        if (sparks.Count == 0)
        {
            Hide();
            return;
        }

        Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080;
            cp.ExStyle |= 0x00000008;
            cp.ExStyle |= 0x00080000;
            cp.ExStyle |= 0x00000020;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Magenta);
        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.CompositingMode = CompositingMode.SourceCopy;
        for (int i = 0; i < sparks.Count; i++)
        {
            SparkParticle spark = sparks[i];
            float t = Math.Clamp(spark.Life / spark.MaxLife, 0f, 1f);
            if (t <= 0.1f)
            {
                continue;
            }

            using SolidBrush brush = new(Color.FromArgb(255, spark.Color));
            float size = spark.Size * (0.75f + t);
            e.Graphics.FillRectangle(
                brush,
                spark.X - (size * 0.5f),
                spark.Y - (size * 0.5f),
                size,
                size);
        }
    }

    private struct SparkParticle
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float Life;
        public float MaxLife;
        public float Size;
        public Color Color;
    }
}
