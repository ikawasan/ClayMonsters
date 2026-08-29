using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace ClayMonstersPet;

internal sealed class PetForm : Form
{
    public const int WindowSize = 330;
    private const int Fps = 8;

    private readonly PetSheet sheet;
    private readonly string? gameExe;
    private readonly System.Windows.Forms.Timer animTimer;
    private readonly System.Windows.Forms.Timer logicTimer;
    private readonly Random random = new();
    private readonly List<NoteParticle> notes = new(24);
    private readonly Font zzzFont = new("Segoe UI", 18f, FontStyle.Bold);
    private readonly Font noteFont = new("Segoe UI", 16f, FontStyle.Bold);

    private PetFacing facing = PetFacing.AnglePos45;
    private PetAction action = PetAction.Idle;
    private PetAiState aiState = PetAiState.Idle;
    private int frameIndex;
    private bool clickThrough;
    private bool isDragging;
    private Point dragGrabOffset;
    private PointF currentPos;
    private PointF moveFrom;
    private PointF moveTo;
    private float moveElapsed;
    private float moveDuration = 2.4f;
    private float idleRemaining;
    private float sleepRemaining;
    private float singRemaining;
    private float walkSessionRemaining;
    private float zzzTime;
    private float singTime;
    private float noteSpawnCooldown;
    private float attackCooldown;
    private bool brainControlled;
    private bool lineFollowChasing;
    private Action? onUserDragEnded;
    private IPetBehaviorAdvisor? behaviorAdvisor;
    private bool dragInterruptedSleep;
    private bool dragKeepSinging;
    private float brainStuckSeconds;

    public PetForm(string cacheDirectory, string? gameExe)
    {
        this.gameExe = gameExe;
        sheet = PetSheet.Load(cacheDirectory);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        Text = "ClayMonstersPet";
        StartPosition = FormStartPosition.Manual;
        Size = new Size(WindowSize, WindowSize);
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        DoubleBuffered = true;
        PetWindowOrder.Apply(this);

        currentPos = PetDesktopBounds.RandomTopLeft(random, WindowSize, WindowSize);
        Location = Point.Round(currentPos);

        animTimer = new System.Windows.Forms.Timer { Interval = 1000 / Fps };
        animTimer.Tick += (_, _) => TickAnimation();
        animTimer.Start();

        logicTimer = new System.Windows.Forms.Timer { Interval = 33 };
        logicTimer.Tick += (_, _) => TickLogic();
        logicTimer.Start();

        idleRemaining = NextLongStateSeconds();
        attackCooldown = NextAttackDelay();
        ContextMenuStrip = BuildMenu();
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        RollInitialBehavior();
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyLayeredStyle();
    }

    public PointF Position => currentPos;

    public PointF Center => new(
        currentPos.X + WindowSize * 0.5f,
        currentPos.Y + WindowSize * 0.5f);

    public bool IsDragging => isDragging;

    public bool IsSleeping => aiState == PetAiState.Sleep;

    public bool IsSinging => aiState == PetAiState.Sing;

    public bool IsIdle => aiState == PetAiState.Idle;

    public bool IsMovingNow =>
        (aiState == PetAiState.Walk
            || aiState == PetAiState.LineFollow
            || aiState == PetAiState.Battle
            || aiState == PetAiState.Play)
        && !lineFollowChasing
        && moveElapsed < moveDuration;

    public bool IsBusyForBrain =>
        isDragging
        || aiState == PetAiState.Sleep
        || aiState == PetAiState.Sing
        || aiState == PetAiState.Battle;

    public PetAiState AiState => aiState;

    public void SetBrainControlled(bool enabled) => brainControlled = enabled;

    public void SetBehaviorAdvisor(IPetBehaviorAdvisor? advisor) => behaviorAdvisor = advisor;

    public void SetDragEndedCallback(Action? callback) => onUserDragEnded = callback;

    public void PlaceAt(PointF position)
    {
        currentPos = ClampToWorkArea(position);
        Location = Point.Round(currentPos);
    }

    /// <summary>
    /// 起動直後の行動を待機以外も含めて抽選する
    /// </summary>
    public void RollInitialBehavior()
    {
        RollInitialBehavior(excludeSleep: false);
    }

    /// <summary>
    /// ソロ行動を抽選する
    /// </summary>
    public void RollInitialBehavior(bool excludeSleep)
    {
        if (isDragging)
        {
            return;
        }

        ChooseNextSoloBehavior(excludeSleep, excludeIdle: false);
    }

    /// <summary>
    /// 睡眠ドラッグ後に別行動へ移す
    /// </summary>
    public void WakeFromDragAndChangeAction()
    {
        brainControlled = false;
        lineFollowChasing = false;
        sleepRemaining = 0f;
        ChooseNextSoloBehavior(excludeSleep: true, excludeIdle: true);
    }

    public void BeginExternalMove(PointF target, float durationSeconds, PetAiState state)
    {
        BeginExternalMove(target, durationSeconds, state, null);
    }

    public void BeginExternalMove(
        PointF target,
        float durationSeconds,
        PetAiState state,
        float? faceDeltaX)
    {
        if (isDragging || aiState == PetAiState.Sleep || aiState == PetAiState.Sing)
        {
            return;
        }

        lineFollowChasing = false;
        bool continueWalkCycle =
            action == PetAction.Walk
            && (aiState == PetAiState.Walk
                || aiState == PetAiState.LineFollow
                || aiState == PetAiState.Play
                || aiState == PetAiState.Battle)
            && (state == PetAiState.Walk
                || state == PetAiState.Play
                || state == PetAiState.Battle);
        moveFrom = currentPos;
        moveTo = ClampToWorkArea(target);
        moveElapsed = 0f;
        moveDuration = Math.Max(0.18f, durationSeconds);
        float dx = faceDeltaX ?? (moveTo.X - moveFrom.X);
        facing = ResolveFacing(dx);
        action = PetAction.Walk;
        // 連続ウェイポイント移動中は歩行フレームを引き継いでループを途切れさせない
        if (!continueWalkCycle)
        {
            frameIndex = 0;
        }

        aiState = state;
    }

    /// <summary>
    /// 指定方向を向く
    /// </summary>
    public void FaceToward(float deltaX)
    {
        if (aiState == PetAiState.Sleep || aiState == PetAiState.Sing)
        {
            return;
        }

        facing = ResolveFacing(deltaX);
    }

    /// <summary>
    /// 列移動で先頭(または直前)の位置へ追従する
    /// </summary>
    public void TickFollowLeader(PointF leaderCenter, float spacingPixels, float speedPixelsPerSec, float dt)
    {
        if (isDragging || aiState == PetAiState.Sleep || aiState == PetAiState.Sing)
        {
            return;
        }

        lineFollowChasing = true;
        aiState = PetAiState.LineFollow;
        PointF center = Center;
        float dx = leaderCenter.X - center.X;
        float dy = leaderCenter.Y - center.Y;
        float dist = MathF.Sqrt((dx * dx) + (dy * dy));
        float spacing = Math.Max(28f, spacingPixels);
        if (dist <= spacing * 1.08f)
        {
            action = PetAction.Walk;
            return;
        }

        float step = Math.Max(10f, speedPixelsPerSec) * Math.Max(0.001f, dt);
        float move = Math.Min(step, dist - spacing);
        float inv = 1f / dist;
        float nx = dx * inv;
        float ny = dy * inv;
        PlaceAt(new PointF(currentPos.X + (nx * move), currentPos.Y + (ny * move)));
        facing = ResolveFacing(nx * WindowSize);
        action = PetAction.Walk;
    }

    /// <summary>
    /// 列追従を解除する
    /// </summary>
    public void ClearLineFollowChase()
    {
        lineFollowChasing = false;
        if (aiState == PetAiState.LineFollow)
        {
            aiState = PetAiState.Idle;
            action = PetAction.Idle;
        }
    }

    public void SnapExternal(PointF position, PetFacing petFacing, PetAction petAction)
    {
        if (isDragging)
        {
            return;
        }

        currentPos = ClampToWorkArea(position);
        Location = Point.Round(currentPos);
        if (aiState == PetAiState.Sleep || aiState == PetAiState.Sing)
        {
            return;
        }

        facing = petFacing;
        action = petAction;
    }

    public void EnterIdle(float seconds, bool allowShort = false)
    {
        if (isDragging)
        {
            return;
        }

        lineFollowChasing = false;
        aiState = PetAiState.Idle;
        walkSessionRemaining = 0f;
        facing = PetFacing.AnglePos45;
        if (action != PetAction.Attack)
        {
            action = PetAction.Idle;
            frameIndex = 0;
        }

        if (allowShort)
        {
            idleRemaining = Math.Max(0.2f, seconds);
        }
        else if (seconds >= 300f)
        {
            idleRemaining = seconds;
        }
        else
        {
            idleRemaining = NextShortIdleSeconds();
        }
    }

    public void BeginSleep(float seconds)
    {
        if (isDragging)
        {
            return;
        }

        aiState = PetAiState.Sleep;
        walkSessionRemaining = 0f;
        sleepRemaining = seconds > 0f ? seconds : NextLongStateSeconds();
        sleepRemaining = Math.Max(300f, sleepRemaining);
        ApplySleepVisual();
        frameIndex = 0;
        zzzTime = 0f;
        notes.Clear();
    }

    public void BeginSing(float seconds)
    {
        if (isDragging)
        {
            return;
        }

        aiState = PetAiState.Sing;
        walkSessionRemaining = 0f;
        singRemaining = seconds > 0f ? seconds : NextLongStateSeconds();
        singRemaining = Math.Max(300f, singRemaining);
        singTime = 0f;
        noteSpawnCooldown = 0f;
        ApplySingVisual();
        frameIndex = 0;
        notes.Clear();
    }

    public void WakeFromSleep()
    {
        if (aiState != PetAiState.Sleep)
        {
            return;
        }

        EnterIdle(NextShortIdleSeconds(), allowShort: true);
    }

    public void StopSinging()
    {
        if (aiState != PetAiState.Sing)
        {
            return;
        }

        notes.Clear();
        EnterIdle(NextShortIdleSeconds(), allowShort: true);
    }

    public void TriggerBattleClash()
    {
        aiState = PetAiState.Battle;
        if (sheet.GetCount(facing, PetAction.Attack) > 0)
        {
            action = PetAction.Attack;
            frameIndex = 0;
        }
    }

    /// <summary>
    /// 戦闘姿勢を維持する
    /// </summary>
    public void HoldBattleStance()
    {
        if (isDragging)
        {
            return;
        }

        lineFollowChasing = false;
        aiState = PetAiState.Battle;
        if (action != PetAction.Attack)
        {
            action = PetAction.Idle;
        }
    }

    public float DistanceTo(PetForm other)
    {
        PointF a = Center;
        PointF b = other.Center;
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// 指定座標までの距離を返す
    /// </summary>
    public float DistanceToPoint(PointF point)
    {
        PointF a = Center;
        float dx = a.X - point.X;
        float dy = a.Y - point.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// ボールを弾く攻撃モーションを再生する
    /// </summary>
    public void PlayKickAttack()
    {
        if (isDragging || aiState == PetAiState.Sleep || aiState == PetAiState.Sing)
        {
            return;
        }

        lineFollowChasing = false;
        aiState = PetAiState.Play;
        if (sheet.GetCount(facing, PetAction.Attack) > 0)
        {
            action = PetAction.Attack;
            frameIndex = 0;
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
        e.Graphics.CompositingMode = CompositingMode.SourceCopy;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.SmoothingMode = SmoothingMode.None;
        Bitmap? frame = sheet.GetFrame(facing, action, frameIndex);
        if (frame != null)
        {
            float scale = Math.Min(
                (WindowSize - 16f) / frame.Width,
                (WindowSize - 16f) / frame.Height);
            int drawW = Math.Max(1, (int)(frame.Width * scale));
            int drawH = Math.Max(1, (int)(frame.Height * scale));
            int x = (WindowSize - drawW) / 2;
            int y = (WindowSize - drawH) / 2;
            e.Graphics.DrawImage(frame, x, y, drawW, drawH);
        }

        e.Graphics.CompositingMode = CompositingMode.SourceOver;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        DrawZzz(e.Graphics);
        DrawNotes(e.Graphics);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animTimer.Dispose();
            logicTimer.Dispose();
            sheet.Dispose();
            zzzFont.Dispose();
            noteFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        ContextMenuStrip menu = new();
        ToolStripMenuItem quitPet = new(PetLocalizedText.QuitMenu);
        quitPet.Click += (_, _) => QuitDesktopPet();
        ToolStripMenuItem launchGame = new(PetLocalizedText.LaunchGameMenu);
        launchGame.Click += (_, _) => OpenGame();
        launchGame.Enabled = !string.IsNullOrEmpty(gameExe) && File.Exists(gameExe);
        menu.Items.Add(quitPet);
        menu.Items.Add(launchGame);
        return menu;
    }

    private Point mouseDownClient;
    private bool leftClickCandidate;

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && ContextMenuStrip != null)
        {
            ContextMenuStrip.Show(Cursor.Position);
            return;
        }

        if (e.Button != MouseButtons.Left || clickThrough)
        {
            return;
        }

        mouseDownClient = e.Location;
        leftClickCandidate = true;
        isDragging = false;
        dragGrabOffset = e.Location;
        Capture = true;
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!Capture || (Control.MouseButtons & MouseButtons.Left) == 0)
        {
            return;
        }

        int dx = e.X - mouseDownClient.X;
        int dy = e.Y - mouseDownClient.Y;
        if (!isDragging && (dx * dx + dy * dy) >= 16)
        {
            isDragging = true;
            leftClickCandidate = false;
            dragInterruptedSleep = aiState == PetAiState.Sleep;
            dragKeepSinging = aiState == PetAiState.Sing;
            if (dragInterruptedSleep)
            {
                sleepRemaining = 0f;
            }

            if (!dragKeepSinging)
            {
                aiState = PetAiState.Dragged;
                action = PetAction.Idle;
                facing = PetFacing.AnglePos45;
                frameIndex = 0;
            }
        }

        if (!isDragging)
        {
            return;
        }

        Point screen = PointToScreen(e.Location);
        PointF next = new(
            screen.X - dragGrabOffset.X,
            screen.Y - dragGrabOffset.Y);
        PlaceAt(next);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        bool wasDragging = isDragging;
        bool showMenu = leftClickCandidate && !wasDragging;
        isDragging = false;
        leftClickCandidate = false;
        Capture = false;

        if (wasDragging)
        {
            if (dragInterruptedSleep)
            {
                WakeFromDragAndChangeAction();
            }
            else if (dragKeepSinging && aiState == PetAiState.Sing)
            {
                // 歌唱はドラッグ後も継続し音符を残す
            }
            else
            {
                ChooseNextSoloBehavior(excludeSleep: false, excludeIdle: true);
            }

            dragInterruptedSleep = false;
            dragKeepSinging = false;
            onUserDragEnded?.Invoke();
            return;
        }

        if (showMenu && !clickThrough && ContextMenuStrip != null)
        {
            ContextMenuStrip.Show(Cursor.Position);
        }
    }

    private void QuitDesktopPet()
    {
        Application.Exit();
    }

    private void TickAnimation()
    {
        EnsureWalkVisualWhileMoving();
        if (aiState == PetAiState.Sleep)
        {
            ApplySleepVisual();
            int sleepCount = sheet.GetCount(PetFacing.Front, PetAction.Idle);
            if (sleepCount > 1 && random.NextDouble() < 0.35)
            {
                frameIndex = (frameIndex + 1) % sleepCount;
            }

            Invalidate();
            return;
        }

        if (aiState == PetAiState.Sing)
        {
            ApplySingVisual();
            int singCount = sheet.GetCount(PetFacing.Front, PetAction.Walk);
            if (singCount > 1)
            {
                frameIndex = (frameIndex + 1) % singCount;
            }

            Invalidate();
            return;
        }

        int count = sheet.GetCount(facing, action);
        if (count <= 1)
        {
            Invalidate();
            return;
        }

        frameIndex++;
        if (action == PetAction.Attack && frameIndex >= count)
        {
            action = PetAction.Idle;
            frameIndex = 0;
        }
        else
        {
            frameIndex %= count;
        }

        Invalidate();
    }

    private void TickLogic()
    {
        float dt = logicTimer.Interval / 1000f;
        UpdateNotes(dt);
        EnsureWalkVisualWhileMoving();

        if (isDragging)
        {
            if (dragKeepSinging && aiState == PetAiState.Sing)
            {
                TickSing(dt);
            }

            Invalidate();
            return;
        }

        if (aiState == PetAiState.Sleep)
        {
            ApplySleepVisual();
            zzzTime += dt;
            sleepRemaining -= dt;
            if (sleepRemaining <= 0f)
            {
                WakeFromSleep();
            }

            Invalidate();
            return;
        }

        if (aiState == PetAiState.Sing)
        {
            TickSing(dt);
            Invalidate();
            return;
        }

        if (brainControlled && lineFollowChasing && aiState == PetAiState.LineFollow)
        {
            // 位置は群れ脳のTickFollowLeaderで更新済み
            Invalidate();
            return;
        }

        if (brainControlled
            && (aiState == PetAiState.LineFollow
                || aiState == PetAiState.Walk
                || aiState == PetAiState.Battle
                || aiState == PetAiState.Play))
        {
            TickMoveInterpolation(dt);
            TickBrainStuckWatchdog(dt);
            Invalidate();
            return;
        }

        if (!brainControlled)
        {
            brainStuckSeconds = 0f;
            TickSoloAi(dt);
        }
        else if (aiState == PetAiState.Idle)
        {
            idleRemaining -= dt;
            if (idleRemaining <= 0f)
            {
                brainControlled = false;
                brainStuckSeconds = 0f;
                TickSoloAi(dt);
            }
        }

        Invalidate();
    }

    private void TickBrainStuckWatchdog(float dt)
    {
        if (aiState == PetAiState.Battle || aiState == PetAiState.Play || lineFollowChasing)
        {
            brainStuckSeconds = 0f;
            return;
        }

        if (IsMovingNow)
        {
            brainStuckSeconds = 0f;
            return;
        }

        brainStuckSeconds += dt;
        if (brainStuckSeconds < 2.5f)
        {
            return;
        }

        brainStuckSeconds = 0f;
        brainControlled = false;
        ChooseNextSoloBehavior(excludeSleep: false, excludeIdle: true);
    }

    private void TickSing(float dt)
    {
        singTime += dt;
        singRemaining -= dt;
        ApplySingVisual();
        noteSpawnCooldown -= dt;
        if (noteSpawnCooldown <= 0f)
        {
            SpawnNoteParticle();
            noteSpawnCooldown = 0.35f + (float)random.NextDouble() * 0.35f;
        }

        if (singRemaining <= 0f && !isDragging)
        {
            StopSinging();
        }
    }

    private void TickSoloAi(float dt)
    {
        attackCooldown -= dt;
        if (aiState == PetAiState.Idle
            && attackCooldown <= 0f
            && action != PetAction.Attack
            && sheet.GetCount(facing, PetAction.Attack) > 0)
        {
            action = PetAction.Attack;
            frameIndex = 0;
            attackCooldown = NextAttackDelay();
        }

        if (aiState == PetAiState.Walk
            || aiState == PetAiState.LineFollow
            || aiState == PetAiState.Battle
            || aiState == PetAiState.Play)
        {
            TickMoveInterpolation(dt);
            if (!IsMovingNow
                && (aiState == PetAiState.Play || aiState == PetAiState.LineFollow))
            {
                ChooseNextSoloBehavior(excludeSleep: false, excludeIdle: true);
            }

            return;
        }

        if (aiState != PetAiState.Idle)
        {
            return;
        }

        idleRemaining -= dt;
        if (idleRemaining > 0f)
        {
            return;
        }

        ChooseNextSoloBehavior(excludeSleep: false, excludeIdle: false);
    }

    private void ChooseNextSoloBehavior(bool excludeSleep, bool excludeIdle)
    {
        float sleep = excludeSleep ? 0f : ScaleSoloWeight(PetAiState.Sleep, 0.22f);
        float sing = ScaleSoloWeight(PetAiState.Sing, 0.20f);
        float walk = ScaleSoloWeight(PetAiState.Walk, 0.46f);
        float idle = excludeIdle ? 0f : ScaleSoloWeight(PetAiState.Idle, 0.12f);
        float sum = sleep + sing + walk + idle;
        if (sum <= 0.0001f)
        {
            BeginWalkSession();
            return;
        }

        double roll = random.NextDouble() * sum;
        if ((roll -= sleep) < 0.0)
        {
            BeginSleep(NextLongStateSeconds());
            return;
        }

        if ((roll -= sing) < 0.0)
        {
            BeginSing(NextLongStateSeconds());
            return;
        }

        if ((roll -= walk) < 0.0)
        {
            BeginWalkSession();
            return;
        }

        EnterIdle(NextShortIdleSeconds(), allowShort: true);
    }

    private float ScaleSoloWeight(PetAiState state, float baseWeight)
    {
        float multiplier = behaviorAdvisor != null
            ? behaviorAdvisor.GetSoloWeight(this, state)
            : 1f;
        return Math.Max(0f, baseWeight * multiplier);
    }

    private void TickMoveInterpolation(float dt)
    {
        if (aiState != PetAiState.Walk
            && aiState != PetAiState.LineFollow
            && aiState != PetAiState.Battle
            && aiState != PetAiState.Play)
        {
            return;
        }

        if (aiState == PetAiState.Walk && walkSessionRemaining > 0f)
        {
            walkSessionRemaining -= dt;
        }

        moveElapsed += dt;
        float t = Math.Clamp(moveElapsed / moveDuration, 0f, 1f);
        float eased = t * t * (3f - 2f * t);
        currentPos = new PointF(
            Lerp(moveFrom.X, moveTo.X, eased),
            Lerp(moveFrom.Y, moveTo.Y, eased));
        Location = Point.Round(currentPos);
        if (t < 1f)
        {
            return;
        }

        if (aiState == PetAiState.Battle)
        {
            if (action != PetAction.Attack)
            {
                action = PetAction.Idle;
            }

            return;
        }

        // 群れ制御中の先頭移動は到着後に長待機へ落とさず次ポイント指示を待つ
        if (brainControlled && (aiState == PetAiState.Walk || aiState == PetAiState.Play))
        {
            return;
        }

        if (aiState == PetAiState.Walk && walkSessionRemaining > 1f)
        {
            BeginLocalWander();
            return;
        }

        walkSessionRemaining = 0f;
        ChooseNextSoloBehavior(excludeSleep: false, excludeIdle: true);
    }

    private void BeginWalkSession()
    {
        walkSessionRemaining = NextLongStateSeconds();
        BeginLocalWander();
    }

    private void BeginLocalWander()
    {
        BeginExternalMove(
            PetDesktopBounds.RandomTopLeft(random, WindowSize, WindowSize),
            3.2f + (float)random.NextDouble() * 2.0f,
            PetAiState.Walk);
    }

    private void DrawZzz(Graphics graphics)
    {
        if (aiState != PetAiState.Sleep)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            float phase = (zzzTime * 0.7f + i * 0.33f) % 1f;
            float x = WindowSize * 0.62f + i * 10f + phase * 8f;
            float y = WindowSize * 0.22f - phase * 42f;
            int alpha = (int)(255 * (1f - phase));
            using SolidBrush brush = new(Color.FromArgb(alpha, 40, 80, 160));
            string glyph = i == 2 ? "Z" : "z";
            float size = 14f + i * 4f + phase * 6f;
            using Font font = new(zzzFont.FontFamily, size, FontStyle.Bold);
            graphics.DrawString(glyph, font, brush, x, y);
        }
    }

    private void DrawNotes(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        graphics.CompositingMode = CompositingMode.SourceOver;
        for (int i = 0; i < notes.Count; i++)
        {
            NoteParticle note = notes[i];
            float t = Math.Clamp(note.Life / note.MaxLife, 0f, 1f);
            if (t <= 0.08f)
            {
                continue;
            }

            float size = note.Size * (0.85f + (1f - t) * 0.4f);
            using Font font = new(noteFont.FontFamily, size, FontStyle.Bold);
            Color fill = Color.FromArgb(255, note.Color);
            Color outline = DarkenColor(fill, 0.72f);
            using SolidBrush outlineBrush = new(outline);
            using SolidBrush fillBrush = new(fill);
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    graphics.DrawString(
                        note.Glyph,
                        font,
                        outlineBrush,
                        note.X + offsetX,
                        note.Y + offsetY);
                }
            }

            graphics.DrawString(note.Glyph, font, fillBrush, note.X, note.Y);
        }
    }

    private void SpawnNoteParticle()
    {
        string[] glyphs = { "♪", "♫", "♩", "♬" };
        notes.Add(new NoteParticle
        {
            X = WindowSize * 0.55f + (float)(random.NextDouble() * 36f - 8f),
            Y = WindowSize * 0.28f,
            Vx = -12f + (float)random.NextDouble() * 36f,
            Vy = -28f - (float)random.NextDouble() * 36f,
            Life = 1.1f + (float)random.NextDouble() * 0.5f,
            MaxLife = 1.6f,
            Size = 14f + (float)random.NextDouble() * 10f,
            Glyph = glyphs[random.Next(glyphs.Length)],
            Color = ColorFromHsv(
                random.NextDouble() * 360.0,
                0.78 + (random.NextDouble() * 0.22),
                0.95 + (random.NextDouble() * 0.05))
        });
    }

    private void UpdateNotes(float dt)
    {
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            NoteParticle note = notes[i];
            note.Life -= dt;
            note.X += note.Vx * dt;
            note.Y += note.Vy * dt;
            note.Vx += (float)(Math.Sin(singTime * 4f + i) * 18f * dt);
            if (note.Life <= 0f)
            {
                notes.RemoveAt(i);
            }
            else
            {
                notes[i] = note;
            }
        }
    }

    private void EnsureWalkVisualWhileMoving()
    {
        if (isDragging || aiState == PetAiState.Sleep || aiState == PetAiState.Sing)
        {
            return;
        }

        if (action == PetAction.Attack)
        {
            return;
        }

        if (IsMovingNow || lineFollowChasing || aiState == PetAiState.LineFollow)
        {
            action = PetAction.Walk;
        }
    }

    private float NextAttackDelay() => 6f + (float)(random.NextDouble() * 8f);

    /// <summary>
    /// 短い休憩時間を3〜10秒で返す
    /// </summary>
    private float NextShortIdleSeconds() => 3f + (float)(random.NextDouble() * 7f);

    /// <summary>
    /// 状態継続時間を5〜10分で返す
    /// </summary>
    private float NextLongStateSeconds() => 300f + (float)(random.NextDouble() * 300f);

    private void OpenGame()
    {
        if (string.IsNullOrEmpty(gameExe) || !File.Exists(gameExe))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = gameExe,
                UseShellExecute = true
            });
            Application.Exit();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "ClayMonstersPet", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyLayeredStyle()
    {
        IntPtr hwnd = Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int exStyle = GetWindowLong(hwnd, -20);
        exStyle |= 0x00080000;
        if (clickThrough)
        {
            exStyle |= 0x00000020;
        }
        else
        {
            exStyle &= ~0x00000020;
        }

        SetWindowLong(hwnd, -20, exStyle);
        // TransparencyKeyに加え色キーを明示しピンク背景を残さない
        SetLayeredWindowAttributes(hwnd, 0x00FF00FF, 0, 0x00000001);
    }

    private void ApplySleepVisual()
    {
        facing = PetFacing.Front;
        action = PetAction.Idle;
    }

    private void ApplySingVisual()
    {
        facing = PetFacing.Front;
        action = PetAction.Walk;
    }

    private PetFacing RollFacing() =>
        random.Next(0, 2) == 0 ? PetFacing.AnglePos45 : PetFacing.AngleNeg45;

    private static PetFacing ResolveFacing(float deltaX)
    {
        if (deltaX > 14f)
        {
            return PetFacing.AngleNeg45;
        }

        if (deltaX < -14f)
        {
            return PetFacing.AnglePos45;
        }

        return PetFacing.AnglePos45;
    }

    private static PointF ClampToWorkArea(PointF position)
    {
        return PetDesktopBounds.ClampTopLeft(position, WindowSize, WindowSize);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static Color DarkenColor(Color color, float scale)
    {
        float safe = Math.Clamp(scale, 0f, 1f);
        return Color.FromArgb(
            255,
            (int)Math.Clamp(color.R * safe, 0f, 255f),
            (int)Math.Clamp(color.G * safe, 0f, 255f),
            (int)Math.Clamp(color.B * safe, 0f, 255f));
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        double wrapped = hue % 360.0;
        if (wrapped < 0.0)
        {
            wrapped += 360.0;
        }

        int hi = (int)Math.Floor(wrapped / 60.0) % 6;
        double f = (wrapped / 60.0) - Math.Floor(wrapped / 60.0);
        int v = (int)Math.Clamp(value * 255.0, 0.0, 255.0);
        int p = (int)Math.Clamp(value * (1.0 - saturation) * 255.0, 0.0, 255.0);
        int q = (int)Math.Clamp(value * (1.0 - (f * saturation)) * 255.0, 0.0, 255.0);
        int t = (int)Math.Clamp(value * (1.0 - ((1.0 - f) * saturation)) * 255.0, 0.0, 255.0);
        return hi switch
        {
            0 => Color.FromArgb(255, v, t, p),
            1 => Color.FromArgb(255, q, v, p),
            2 => Color.FromArgb(255, p, v, t),
            3 => Color.FromArgb(255, p, q, v),
            4 => Color.FromArgb(255, t, p, v),
            _ => Color.FromArgb(255, v, p, q)
        };
    }

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

    private struct NoteParticle
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float Life;
        public float MaxLife;
        public float Size;
        public string Glyph;
        public Color Color;
    }
}
