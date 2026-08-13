namespace ClayMonstersPet;

/// <summary>
/// 複数ペットの列移動と簡易バトルと遊ぶを制御する
/// </summary>
internal sealed class PetGroupBrain : IDisposable, IPetBehaviorAdvisor
{
    private enum GroupMode
    {
        Free = 0,
        LineMarch = 1,
        SimpleBattle = 2,
        PlaySolo = 3,
        PlayDuo = 4
    }

    private enum BattlePhase
    {
        Idle = 0,
        Approach = 1,
        Clash = 2,
        Separate = 3
    }

    private enum PlayPhase
    {
        Chase = 0,
        Windup = 1,
        Charge = 2,
        Kick = 3,
        BallFly = 4
    }

    private readonly List<PetForm> pets;
    private readonly System.Windows.Forms.Timer timer;
    private readonly Random random = new();
    private GroupMode mode = GroupMode.Free;
    private float modeRemaining;
    private List<PetForm>? lineOrder;
    private float lineReissueCooldown;
    private float battleRoundCooldown;
    private BattlePhase battlePhase;
    private PetForm? battleA;
    private PetForm? battleB;
    private PetForm? playA;
    private PetForm? playB;
    private PetForm? playTurn;
    private PetBallForm? ball;
    private PetSparkForm? sparkOverlay;
    private PlayPhase playPhase;
    private float playPhaseCooldown;
    private PointF playKickDestination;
    private bool playKickChosen;
    private GroupMode lastPickedMode = GroupMode.Free;
    private const float BallInFrontPixels = 112f;
    private bool disposed;

    public PetGroupBrain(List<PetForm> pets)
    {
        this.pets = pets;
        for (int i = 0; i < pets.Count; i++)
        {
            pets[i].SetDragEndedCallback(OnPetDragEnded);
            pets[i].SetBehaviorAdvisor(this);
        }

        SetAllBrainControlled(false);
        timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (_, _) => Tick();
        timer.Start();

        ScatterInitialPositions();
        // 開始直後から群れ行動も抽選する(全員Free待機で始めない)
        PickNextMode();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        timer.Stop();
        timer.Dispose();
        for (int i = 0; i < pets.Count; i++)
        {
            pets[i].SetBehaviorAdvisor(null);
        }

        DestroyBall();
        DestroySparkOverlay();
    }

    /// <inheritdoc/>
    public float GetSoloWeight(PetForm self, PetAiState state)
    {
        if (pets.Count <= 1)
        {
            return 1f;
        }

        int sleep = 0;
        int sing = 0;
        int walk = 0;
        int idle = 0;
        for (int i = 0; i < pets.Count; i++)
        {
            PetForm pet = pets[i];
            if (ReferenceEquals(pet, self) || pet.IsDragging)
            {
                continue;
            }

            switch (pet.AiState)
            {
                case PetAiState.Sleep:
                    sleep++;
                    break;
                case PetAiState.Sing:
                    sing++;
                    break;
                case PetAiState.Walk:
                case PetAiState.LineFollow:
                case PetAiState.Battle:
                case PetAiState.Play:
                    walk++;
                    break;
                default:
                    idle++;
                    break;
            }
        }

        int maxRare = Math.Max(1, (pets.Count + 1) / 3);
        int maxWalk = Math.Max(1, pets.Count - 1);
        return state switch
        {
            PetAiState.Sleep => OccupancyMultiplier(sleep, maxRare),
            PetAiState.Sing => OccupancyMultiplier(sing, maxRare),
            PetAiState.Walk => OccupancyMultiplier(walk, maxWalk),
            PetAiState.Idle => OccupancyMultiplier(idle, maxRare),
            _ => 1f
        };
    }

    private static float OccupancyMultiplier(int count, int maxAllowed)
    {
        if (count >= maxAllowed)
        {
            return 0.08f;
        }

        if (count <= 0)
        {
            return 1.4f;
        }

        return 1f / (1f + count);
    }

    /// <summary>
    /// 群れモード継続時間を5〜10分で返す
    /// </summary>
    private float NextLongModeSeconds() => 300f + (float)(random.NextDouble() * 300f);

    private void OnPetDragEnded()
    {
        // ドラッグ後も現在モードの残り時間は維持し強制切替しない
    }

    private void Tick()
    {
        if (pets.Count <= 0)
        {
            return;
        }

        float dt = timer.Interval / 1000f;
        sparkOverlay?.Tick(dt);
        modeRemaining -= dt;
        if (lineReissueCooldown > 0f)
        {
            lineReissueCooldown -= dt;
        }

        if (mode != GroupMode.PlaySolo && mode != GroupMode.PlayDuo)
        {
            DestroyBall();
        }

        if (mode == GroupMode.Free)
        {
            if (modeRemaining <= 0f)
            {
                PickNextMode();
            }

            return;
        }

        if (mode == GroupMode.LineMarch)
        {
            TickLineMarch(dt);
            if (modeRemaining <= 0f)
            {
                lineOrder = null;
                SetAllBrainControlled(false);
                PickNextMode();
            }

            return;
        }

        if (mode == GroupMode.SimpleBattle)
        {
            TickSimpleBattle(dt);
            if (modeRemaining <= 0f)
            {
                EndSimpleBattle();
            }

            return;
        }

        if (mode == GroupMode.PlaySolo || mode == GroupMode.PlayDuo)
        {
            TickPlay(dt);
            if (modeRemaining <= 0f)
            {
                EndPlay();
            }
        }
    }

    private void PickNextMode()
    {
        int recruitable = CountRecruitablePets();
        if (recruitable < 1)
        {
            EnterFree(NextLongModeSeconds());
            return;
        }

        if (recruitable < 2)
        {
            if (lastPickedMode == GroupMode.PlaySolo)
            {
                EnterFree(NextLongModeSeconds());
            }
            else if (lastPickedMode == GroupMode.Free || random.NextDouble() < 0.5)
            {
                BeginPlaySolo();
            }
            else
            {
                EnterFree(NextLongModeSeconds());
            }

            return;
        }

        float line = lastPickedMode == GroupMode.LineMarch ? 0.05f : 0.14f;
        float battle = lastPickedMode == GroupMode.SimpleBattle ? 0.06f : 0.28f;
        float solo = lastPickedMode == GroupMode.PlaySolo ? 0.04f : 0.10f;
        float duo = lastPickedMode == GroupMode.PlayDuo ? 0.06f : 0.30f;
        float free = lastPickedMode == GroupMode.Free ? 0.05f : 0.12f;
        double roll = random.NextDouble() * (line + battle + solo + duo + free);
        if ((roll -= line) < 0.0)
        {
            BeginLineMarch();
        }
        else if ((roll -= battle) < 0.0)
        {
            BeginSimpleBattle();
        }
        else if ((roll -= solo) < 0.0)
        {
            BeginPlaySolo();
        }
        else if ((roll -= duo) < 0.0)
        {
            BeginPlayDuo();
        }
        else
        {
            EnterFree(NextLongModeSeconds());
        }
    }

    private void EnterFree(float seconds)
    {
        mode = GroupMode.Free;
        lastPickedMode = GroupMode.Free;
        modeRemaining = seconds >= 300f ? seconds : NextLongModeSeconds();
        battleA = null;
        battleB = null;
        battlePhase = BattlePhase.Idle;
        playA = null;
        playB = null;
        playTurn = null;
        lineOrder = null;
        DestroyBall();
        SetAllBrainControlled(false);
        List<PetForm> toAssign = new(pets.Count);
        for (int i = 0; i < pets.Count; i++)
        {
            pets[i].ClearLineFollowChase();
            if (pets[i].IsDragging || pets[i].IsSleeping || pets[i].IsSinging)
            {
                continue;
            }

            toAssign.Add(pets[i]);
        }

        // 先に決めた個体を見て偏りを避ける
        toAssign = BuildShuffledOrder(toAssign);
        for (int i = 0; i < toAssign.Count; i++)
        {
            toAssign[i].RollInitialBehavior();
        }
    }

    private void BeginLineMarch()
    {
        List<PetForm> active = GetRecruitablePets();
        if (active.Count < 2)
        {
            EnterFree(NextLongModeSeconds());
            return;
        }

        mode = GroupMode.LineMarch;
        lastPickedMode = GroupMode.LineMarch;
        modeRemaining = NextLongModeSeconds();
        lineOrder = BuildShuffledOrder(active);
        SetAllBrainControlled(false);
        for (int i = 0; i < lineOrder.Count; i++)
        {
            lineOrder[i].SetBrainControlled(true);
        }

        WakeAllForGroup(lineOrder);
        IssueLeaderWander();
    }

    private void TickLineMarch(float dt)
    {
        List<PetForm> active = GetAvailablePets();
        if (active.Count < 2)
        {
            EnterFree(NextLongModeSeconds());
            return;
        }

        lineOrder = RebuildLineOrder(active);
        if (lineOrder.Count < 2)
        {
            EnterFree(NextLongModeSeconds());
            return;
        }

        PetForm leader = lineOrder[0];
        if (!leader.IsMovingNow)
        {
            IssueLeaderWander();
        }

        float spacing = PetForm.WindowSize * 0.32f;
        float followSpeed = 280f;
        for (int i = 1; i < lineOrder.Count; i++)
        {
            PetForm prev = lineOrder[i - 1];
            PetForm follower = lineOrder[i];
            if (follower.IsDragging || follower.IsSleeping || follower.IsSinging)
            {
                continue;
            }

            follower.TickFollowLeader(prev.Center, spacing, followSpeed, dt);
        }
    }

    private void IssueLeaderWander()
    {
        if (lineOrder == null || lineOrder.Count == 0)
        {
            return;
        }

        PetForm leader = lineOrder[0];
        if (leader.IsDragging || leader.IsSleeping || leader.IsSinging)
        {
            return;
        }

        leader.ClearLineFollowChase();
        PointF current = leader.Position;
        PointF target = DistantLineMarchPoint(current);
        float dxFinal = target.X - current.X;
        float dyFinal = target.Y - current.Y;
        float travel = MathF.Sqrt((dxFinal * dxFinal) + (dyFinal * dyFinal));
        float speed = 210f + (float)(random.NextDouble() * 70f);
        float duration = Math.Clamp(travel / speed, 2.4f, 10f);
        leader.BeginExternalMove(target, duration, PetAiState.Walk);
    }

    private PointF DistantLineMarchPoint(PointF from)
    {
        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        int margin = 8;
        int minX = work.Left + margin;
        int minY = work.Top + margin;
        int maxX = Math.Max(minX, work.Right - PetForm.WindowSize - margin);
        int maxY = Math.Max(minY, work.Bottom - PetForm.WindowSize - margin);
        float minDistance = Math.Max(work.Width, work.Height) * 0.58f;
        float midX = (minX + maxX) * 0.5f;
        float midY = (minY + maxY) * 0.5f;
        PointF best = from;
        float bestDist = 0f;
        for (int attempt = 0; attempt < 18; attempt++)
        {
            int x0 = minX;
            int x1 = maxX;
            int y0 = minY;
            int y1 = maxY;
            if (attempt < 12)
            {
                if (from.X < midX)
                {
                    x0 = (int)MathF.Ceiling(minX + ((maxX - minX) * 0.62f));
                }
                else
                {
                    x1 = (int)MathF.Floor(minX + ((maxX - minX) * 0.38f));
                }

                if (from.Y < midY)
                {
                    y0 = (int)MathF.Ceiling(minY + ((maxY - minY) * 0.62f));
                }
                else
                {
                    y1 = (int)MathF.Floor(minY + ((maxY - minY) * 0.38f));
                }

                x0 = Math.Clamp(x0, minX, maxX);
                x1 = Math.Clamp(Math.Max(x0, x1), minX, maxX);
                y0 = Math.Clamp(y0, minY, maxY);
                y1 = Math.Clamp(Math.Max(y0, y1), minY, maxY);
            }

            PointF candidate = new(
                random.Next(x0, x1 + 1),
                random.Next(y0, y1 + 1));
            float dx = candidate.X - from.X;
            float dy = candidate.Y - from.Y;
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

    private List<PetForm> BuildShuffledOrder(List<PetForm> source)
    {
        List<PetForm> order = new(source);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            PetForm tmp = order[i];
            order[i] = order[j];
            order[j] = tmp;
        }

        return order;
    }

    private List<PetForm> RebuildLineOrder(List<PetForm> active)
    {
        if (lineOrder == null || lineOrder.Count == 0)
        {
            return BuildShuffledOrder(active);
        }

        List<PetForm> kept = new(active.Count);
        for (int i = 0; i < lineOrder.Count; i++)
        {
            PetForm pet = lineOrder[i];
            if (active.Contains(pet))
            {
                kept.Add(pet);
            }
        }

        for (int i = 0; i < active.Count; i++)
        {
            if (!kept.Contains(active[i]))
            {
                kept.Add(active[i]);
            }
        }

        return kept;
    }

    private void BeginSimpleBattle()
    {
        List<PetForm> active = GetRecruitablePets();
        if (!TryPickDistinctPair(active, out battleA, out battleB))
        {
            EnterFree(NextLongModeSeconds());
            return;
        }

        SetAllBrainControlled(false);
        battleA!.SetBrainControlled(true);
        battleB!.SetBrainControlled(true);
        WakeAllForGroup(new List<PetForm> { battleA, battleB });
        DiversifyIdlePets(battleA, battleB);
        mode = GroupMode.SimpleBattle;
        lastPickedMode = GroupMode.SimpleBattle;
        modeRemaining = NextLongModeSeconds();
        battlePhase = BattlePhase.Idle;
        battleRoundCooldown = 0.2f;
        PointF mid = BattleMidpoint();
        IssueBattleMove(battleA, BattleSlotTopLeft(battleA, mid, PetForm.WindowSize * 0.85f), 700f);
        IssueBattleMove(battleB, BattleSlotTopLeft(battleB, mid, PetForm.WindowSize * 0.85f), 700f);
        FaceBattlePair();
    }

    private void TickSimpleBattle(float dt)
    {
        PetForm? fighterA = battleA;
        PetForm? fighterB = battleB;
        if (!IsPlayablePet(fighterA) || !IsPlayablePet(fighterB))
        {
            EndSimpleBattle();
            return;
        }

        FaceBattlePair();
        if (fighterA!.IsMovingNow || fighterB!.IsMovingNow)
        {
            return;
        }

        battleRoundCooldown -= dt;
        if (battleRoundCooldown > 0f)
        {
            return;
        }

        switch (battlePhase)
        {
            case BattlePhase.Idle:
                battlePhase = BattlePhase.Approach;
                IssueBattleWindup();
                break;
            case BattlePhase.Approach:
                battlePhase = BattlePhase.Clash;
                IssueBattleApproach();
                break;
            case BattlePhase.Clash:
                battlePhase = BattlePhase.Separate;
                PointF spark = new(
                    (fighterA.Center.X + fighterB.Center.X) * 0.5f,
                    (fighterA.Center.Y + fighterB.Center.Y) * 0.5f);
                BurstBattleSparks(spark);
                fighterA.TriggerBattleClash();
                fighterB.TriggerBattleClash();
                IssueBattleKnockback();
                break;
            case BattlePhase.Separate:
                battlePhase = BattlePhase.Idle;
                fighterA.HoldBattleStance();
                fighterB.HoldBattleStance();
                FaceBattlePair();
                battleRoundCooldown = 1.2f + (float)(random.NextDouble() * 1.4f);
                break;
        }
    }

    private void IssueBattleWindup()
    {
        if (battleA == null || battleB == null)
        {
            return;
        }

        PointF mid = BattleMidpoint();
        IssueBattleMove(battleA, BattleSlotTopLeft(battleA, mid, PetForm.WindowSize * 0.62f), 900f);
        IssueBattleMove(battleB, BattleSlotTopLeft(battleB, mid, PetForm.WindowSize * 0.62f), 900f);
    }

    private void IssueBattleApproach()
    {
        if (battleA == null || battleB == null)
        {
            return;
        }

        PointF mid = BattleMidpoint();
        IssueBattleMove(battleA, BattleSlotTopLeft(battleA, mid, PetForm.WindowSize * 0.14f), 1400f);
        IssueBattleMove(battleB, BattleSlotTopLeft(battleB, mid, PetForm.WindowSize * 0.14f), 1400f);
    }

    private void IssueBattleKnockback()
    {
        if (battleA == null || battleB == null)
        {
            return;
        }

        PointF mid = BattleMidpoint();
        IssueBattleMove(battleA, BattleKnockbackTopLeft(battleA, mid), 1200f);
        IssueBattleMove(battleB, BattleKnockbackTopLeft(battleB, mid), 1200f);
    }

    private void IssueBattleMove(PetForm pet, PointF target, float speedPixelsPerSec)
    {
        PetForm? other = ReferenceEquals(pet, battleA) ? battleB : battleA;
        float faceDx = other != null ? other.Center.X - pet.Center.X : 0f;
        PointF clamped = ClampPetTopLeft(target);
        float travel = Distance(pet.Position, clamped);
        float duration = Math.Clamp(travel / Math.Max(80f, speedPixelsPerSec), 0.18f, 0.9f);
        pet.BeginExternalMove(clamped, duration, PetAiState.Battle, faceDx);
    }

    private PointF BattleMidpoint()
    {
        if (battleA == null || battleB == null)
        {
            Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
            return new PointF(
                work.Left + (work.Width * 0.5f),
                work.Top + (work.Height * 0.5f));
        }

        return new PointF(
            (battleA.Center.X + battleB.Center.X) * 0.5f,
            (battleA.Center.Y + battleB.Center.Y) * 0.5f);
    }

    private PointF BattleSlotTopLeft(PetForm pet, PointF mid, float fromMid)
    {
        ResolveBattleSide(pet, mid, out float nx, out float ny);
        return new PointF(
            mid.X + (nx * fromMid) - (PetForm.WindowSize * 0.5f),
            mid.Y + (ny * fromMid) - (PetForm.WindowSize * 0.5f));
    }

    private PointF BattleKnockbackTopLeft(PetForm pet, PointF mid)
    {
        ResolveBattleSide(pet, mid, out float nx, out float ny);
        float angle = MathF.Atan2(ny, nx) + (((float)random.NextDouble() - 0.5f) * 1.2f);
        float dist = 220f + (float)(random.NextDouble() * 140f);
        return new PointF(
            mid.X + (MathF.Cos(angle) * dist) - (PetForm.WindowSize * 0.5f),
            mid.Y + (MathF.Sin(angle) * dist) - (PetForm.WindowSize * 0.5f));
    }

    private void ResolveBattleSide(PetForm pet, PointF mid, out float nx, out float ny)
    {
        float dx = pet.Center.X - mid.X;
        float dy = pet.Center.Y - mid.Y;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length >= 4f)
        {
            nx = dx / length;
            ny = dy / length;
            return;
        }

        bool isA = ReferenceEquals(pet, battleA);
        nx = isA ? -1f : 1f;
        ny = 0f;
    }

    private void FaceBattlePair()
    {
        if (battleA == null || battleB == null)
        {
            return;
        }

        battleA.FaceToward(battleB.Center.X - battleA.Center.X);
        battleB.FaceToward(battleA.Center.X - battleB.Center.X);
    }

    private void EndSimpleBattle()
    {
        battleA = null;
        battleB = null;
        battlePhase = BattlePhase.Idle;
        HideSparkOverlay();
        PickNextMode();
    }

    private void BeginPlaySolo()
    {
        List<PetForm> active = GetRecruitablePets();
        if (active.Count < 1)
        {
            EnterFree(NextLongModeSeconds());
            return;
        }

        DestroyBall();
        playA = active[random.Next(active.Count)];
        playB = null;
        playTurn = playA;
        SetAllBrainControlled(false);
        playA.SetBrainControlled(true);
        WakeAllForGroup(new List<PetForm> { playA });
        DiversifyIdlePets(playA, null);
        EnsureBall();
        PlaceBallAwayFrom(playA.Center, PetForm.WindowSize * 1.8f);
        mode = GroupMode.PlaySolo;
        lastPickedMode = GroupMode.PlaySolo;
        modeRemaining = NextLongModeSeconds();
        playPhase = PlayPhase.Chase;
        playPhaseCooldown = 0f;
        playKickChosen = false;
        IssueChaseBall(playA);
    }

    private void BeginPlayDuo()
    {
        List<PetForm> active = GetRecruitablePets();
        if (!TryPickDistinctPair(active, out playA, out playB))
        {
            BeginPlaySolo();
            return;
        }

        DestroyBall();

        playTurn = playA;
        SetAllBrainControlled(false);
        playA!.SetBrainControlled(true);
        playB!.SetBrainControlled(true);
        WakeAllForGroup(new List<PetForm> { playA, playB });
        DiversifyIdlePets(playA, playB);
        EnsureBall();
        // ボールを先頭担当の近くに置く
        PlaceBallNear(playA.Center, PetForm.WindowSize * 0.9f);
        mode = GroupMode.PlayDuo;
        lastPickedMode = GroupMode.PlayDuo;
        modeRemaining = NextLongModeSeconds();
        playPhase = PlayPhase.Chase;
        playPhaseCooldown = 0f;
        playKickChosen = false;
        IssueChaseBall(playA);
    }

    private void TickPlay(float dt)
    {
        if (ball == null || !TryKeepPlayTurn() || playTurn == null)
        {
            EndPlay();
            return;
        }

        if (ball.IsDragging)
        {
            TickPlayBallDragFollow();
            return;
        }

        ball.Tick(dt);
        if (playPhaseCooldown > 0f)
        {
            playPhaseCooldown -= dt;
        }

        switch (playPhase)
        {
            case PlayPhase.Chase:
                TickPlayChase();
                break;
            case PlayPhase.Windup:
                FaceKickDirection(playTurn);
                if (!playTurn.IsMovingNow)
                {
                    IssueChargeBall(playTurn);
                    playPhase = PlayPhase.Charge;
                }

                break;
            case PlayPhase.Charge:
                TickPlayCharge();
                break;
            case PlayPhase.Kick:
                FaceKickDirection(playTurn);
                if (playPhaseCooldown <= 0f)
                {
                    KickBallFromCurrentTurn();
                    playPhase = PlayPhase.BallFly;
                    playPhaseCooldown = 0f;
                }

                break;
            case PlayPhase.BallFly:
                if (!ball.IsFlying)
                {
                    if (mode == GroupMode.PlayDuo)
                    {
                        playTurn = ReferenceEquals(playTurn, playA) ? playB : playA;
                    }

                    playPhase = PlayPhase.Chase;
                    playPhaseCooldown = 0.15f;
                    playKickChosen = false;
                    if (playTurn != null)
                    {
                        IssueChaseBall(playTurn);
                    }
                }

                break;
        }
    }

    private void TickPlayChase()
    {
        if (playTurn == null || ball == null)
        {
            return;
        }

        FaceChaseDirection(playTurn);
        float approachRadius = BallInFrontPixels + 12f;
        if (playTurn.DistanceToPoint(ball.Center) <= approachRadius
            || HasReachedPlayStand(playTurn))
        {
            ChooseKickDestination();
            EnsureKickDestinationHasTravel();
            if (!TryGetKickDirection(out _, out _))
            {
                IssueChargeBall(playTurn);
                playPhase = PlayPhase.Charge;
                return;
            }

            IssueWindup(playTurn);
            playPhase = PlayPhase.Windup;
            return;
        }

        if (!playTurn.IsMovingNow && playPhaseCooldown <= 0f)
        {
            IssueChaseBall(playTurn);
        }
    }

    private void TickPlayCharge()
    {
        if (playTurn == null || ball == null)
        {
            return;
        }

        FaceKickDirection(playTurn);
        float slamRadius = BallInFrontPixels + 16f;
        if (playTurn.DistanceToPoint(ball.Center) <= slamRadius || !playTurn.IsMovingNow)
        {
            playTurn.PlayKickAttack();
            FaceKickDirection(playTurn);
            KickBallFromCurrentTurn();
            playPhase = PlayPhase.BallFly;
            playPhaseCooldown = 0f;
        }
    }

    private void IssueChaseBall(PetForm pet)
    {
        if (ball == null || pet.IsDragging)
        {
            return;
        }

        bool liveFollow = ball.IsDragging;
        if (!liveFollow)
        {
            ChooseKickDestination();
        }

        PointF target = PetStandInFrontOfBall(pet, useKickAim: !liveFollow);
        float dx = target.X - pet.Position.X;
        float dy = target.Y - pet.Position.Y;
        float travel = MathF.Sqrt((dx * dx) + (dy * dy));
        float speed = liveFollow
            ? 280f + (float)(random.NextDouble() * 40f)
            : 175f + (float)(random.NextDouble() * 40f);
        float duration = liveFollow
            ? Math.Clamp(travel / speed, 0.12f, 0.9f)
            : Math.Clamp(travel / speed, 0.5f, 3.5f);
        pet.BeginExternalMove(target, duration, PetAiState.Play, dx);
    }

    private void TickPlayBallDragFollow()
    {
        if (playTurn == null || ball == null)
        {
            return;
        }

        playPhase = PlayPhase.Chase;
        playKickChosen = false;
        playPhaseCooldown = 0f;
        FaceChaseDirection(playTurn);
        IssueChaseBall(playTurn);
        if (mode == GroupMode.PlayDuo && playA != null && playB != null)
        {
            PetForm other = ReferenceEquals(playTurn, playA) ? playB : playA;
            if (!other.IsDragging && !other.IsSleeping && !other.IsSinging)
            {
                FaceChaseDirection(other);
                IssueChaseBall(other);
            }
        }
    }

    private void OnBallDragStarted()
    {
        if (mode != GroupMode.PlaySolo && mode != GroupMode.PlayDuo)
        {
            return;
        }

        playPhase = PlayPhase.Chase;
        playKickChosen = false;
        playPhaseCooldown = 0f;
    }

    private void OnBallDragEnded()
    {
        if (mode != GroupMode.PlaySolo && mode != GroupMode.PlayDuo)
        {
            return;
        }

        if (playTurn == null || ball == null)
        {
            EndPlay();
            return;
        }

        playKickChosen = false;
        playPhase = PlayPhase.Chase;
        playPhaseCooldown = 0f;
        ChooseKickDestination();
        IssueChaseBall(playTurn);
    }

    private void IssueWindup(PetForm pet)
    {
        if (ball == null || pet.IsDragging)
        {
            return;
        }

        if (!TryGetKickDirection(out float nx, out float ny))
        {
            return;
        }

        float runup = PetForm.WindowSize * 0.48f;
        PointF windup = new(
            pet.Position.X - (nx * runup),
            pet.Position.Y - (ny * runup));
        float duration = 0.42f;
        pet.BeginExternalMove(windup, duration, PetAiState.Play, KickFaceDeltaX());
    }

    private void IssueChargeBall(PetForm pet)
    {
        if (ball == null || pet.IsDragging)
        {
            return;
        }

        PointF target = PetStandInFrontOfBall(pet);
        float dx = target.X - pet.Position.X;
        float dy = target.Y - pet.Position.Y;
        float travel = MathF.Sqrt((dx * dx) + (dy * dy));
        float duration = Math.Clamp(travel / 320f, 0.28f, 0.7f);
        pet.BeginExternalMove(target, duration, PetAiState.Play, KickFaceDeltaX());
    }

    private void ChooseKickDestination()
    {
        if (ball == null)
        {
            return;
        }

        if (mode == GroupMode.PlayDuo && playA != null && playB != null && playTurn != null)
        {
            PetForm receiver = ReferenceEquals(playTurn, playA) ? playB : playA;
            playKickDestination = BallTopLeftInFrontOf(receiver.Center, ball.Center);
            playKickChosen = true;
            return;
        }

        if (playKickChosen)
        {
            return;
        }

        playKickDestination = RandomBallPointAwayFrom(ball.Center, PetForm.WindowSize * 2.4f);
        EnsureKickDestinationHasTravel();
        playKickChosen = true;
    }

    private PointF BallTopLeftInFrontOf(PointF monsterCenter, PointF fromCenter)
    {
        float dx = monsterCenter.X - fromCenter.X;
        float dy = monsterCenter.Y - fromCenter.Y;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        float nx;
        float ny;
        if (length < 1f)
        {
            nx = 1f;
            ny = 0f;
        }
        else
        {
            nx = dx / length;
            ny = dy / length;
        }

        PointF stopCenter = new(
            monsterCenter.X - (nx * BallInFrontPixels),
            monsterCenter.Y - (ny * BallInFrontPixels));
        return new PointF(
            stopCenter.X - PetBallForm.BallSize * 0.5f,
            stopCenter.Y - PetBallForm.BallSize * 0.5f);
    }

    private PointF PetStandInFrontOfBall(PetForm pet, bool useKickAim = true)
    {
        PointF ballCenter = ball != null ? ball.Center : pet.Center;
        float nx;
        float ny;
        if (!useKickAim || !TryGetKickDirection(out nx, out ny))
        {
            float dx = ballCenter.X - pet.Center.X;
            float dy = ballCenter.Y - pet.Center.Y;
            float length = MathF.Sqrt((dx * dx) + (dy * dy));
            if (length < 1f)
            {
                nx = 1f;
                ny = 0f;
            }
            else
            {
                nx = dx / length;
                ny = dy / length;
            }
        }

        PointF stand = new(
            ballCenter.X - (nx * BallInFrontPixels),
            ballCenter.Y - (ny * BallInFrontPixels));
        return new PointF(
            stand.X - PetForm.WindowSize * 0.5f,
            stand.Y - PetForm.WindowSize * 0.5f);
    }

    private float KickFaceDeltaX()
    {
        if (ball == null)
        {
            return 0f;
        }

        return (playKickDestination.X + PetBallForm.BallSize * 0.5f) - ball.Center.X;
    }

    private bool TryGetKickDirection(out float nx, out float ny)
    {
        nx = 0f;
        ny = 0f;
        if (ball == null)
        {
            return false;
        }

        float dx = (playKickDestination.X + PetBallForm.BallSize * 0.5f) - ball.Center.X;
        float dy = (playKickDestination.Y + PetBallForm.BallSize * 0.5f) - ball.Center.Y;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length < 8f)
        {
            return false;
        }

        nx = dx / length;
        ny = dy / length;
        return true;
    }

    private void FaceKickDirection(PetForm pet)
    {
        pet.FaceToward(KickFaceDeltaX());
    }

    private void FaceChaseDirection(PetForm pet)
    {
        if (ball == null)
        {
            return;
        }

        pet.FaceToward(ball.Center.X - pet.Center.X);
    }

    private void KickBallFromCurrentTurn()
    {
        if (ball == null || playTurn == null)
        {
            return;
        }

        EnsureKickDestinationHasTravel();
        float dx = playKickDestination.X - ball.Position.X;
        float dy = playKickDestination.Y - ball.Position.Y;
        float travel = MathF.Sqrt((dx * dx) + (dy * dy));
        float duration = Math.Clamp(travel / 420f, 0.45f, 1.6f);
        ball.KickTo(playKickDestination, duration);
    }

    private bool TryKeepPlayTurn()
    {
        if (IsPlayablePet(playTurn))
        {
            return true;
        }

        if (mode == GroupMode.PlayDuo)
        {
            PetForm? other = ReferenceEquals(playTurn, playA) ? playB : playA;
            if (IsPlayablePet(other))
            {
                playTurn = other;
                playPhase = PlayPhase.Chase;
                playKickChosen = false;
                playPhaseCooldown = 0f;
                return true;
            }
        }

        return false;
    }

    private static bool IsPlayablePet(PetForm? pet)
    {
        return pet != null
            && !pet.IsDisposed
            && !pet.IsDragging
            && !pet.IsSleeping
            && !pet.IsSinging;
    }

    private bool HasReachedPlayStand(PetForm pet)
    {
        if (ball == null)
        {
            return false;
        }

        PointF stand = PetStandInFrontOfBall(pet, useKickAim: !ball.IsDragging);
        PointF clamped = ClampPetTopLeft(stand);
        return Distance(pet.Position, clamped) <= 16f;
    }

    private void EnsureKickDestinationHasTravel()
    {
        if (ball == null)
        {
            return;
        }

        PointF clamped = ClampBallTopLeft(playKickDestination);
        float dx = clamped.X - ball.Position.X;
        float dy = clamped.Y - ball.Position.Y;
        if (MathF.Sqrt((dx * dx) + (dy * dy)) >= 96f)
        {
            playKickDestination = clamped;
            return;
        }

        playKickDestination = BallTopLeftTowardInterior(ball.Center);
        playKickChosen = true;
    }

    private PointF BallTopLeftTowardInterior(PointF fromCenter)
    {
        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        PointF destCenter = new(
            work.Left + (work.Width * 0.5f),
            work.Top + (work.Height * 0.5f));
        float dx = destCenter.X - fromCenter.X;
        float dy = destCenter.Y - fromCenter.Y;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length < 48f)
        {
            return RandomBallPointAwayFrom(fromCenter, PetForm.WindowSize * 1.6f);
        }

        float travel = Math.Max(PetForm.WindowSize * 2.2f, length * 0.65f);
        float inv = 1f / length;
        return ClampBallTopLeft(new PointF(
            fromCenter.X + (dx * inv * travel) - (PetBallForm.BallSize * 0.5f),
            fromCenter.Y + (dy * inv * travel) - (PetBallForm.BallSize * 0.5f)));
    }

    private static PointF ClampPetTopLeft(PointF topLeft)
    {
        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        return new PointF(
            Math.Clamp(topLeft.X, work.Left, work.Right - PetForm.WindowSize),
            Math.Clamp(topLeft.Y, work.Top, work.Bottom - PetForm.WindowSize));
    }

    private static PointF ClampBallTopLeft(PointF topLeft)
    {
        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        return new PointF(
            Math.Clamp(topLeft.X, work.Left, work.Right - PetBallForm.BallSize),
            Math.Clamp(topLeft.Y, work.Top, work.Bottom - PetBallForm.BallSize));
    }

    private static float Distance(PointF a, PointF b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private void EndPlay()
    {
        playA = null;
        playB = null;
        playTurn = null;
        DestroyBall();
        PickNextMode();
    }

    private void EnsureBall()
    {
        if (ball != null && !ball.IsDisposed)
        {
            ball.Show();
            return;
        }

        ball = new PetBallForm();
        ball.SetDragCallbacks(OnBallDragStarted, OnBallDragEnded);
        ball.Show();
    }

    private void BurstBattleSparks(PointF worldCenter)
    {
        if (sparkOverlay == null || sparkOverlay.IsDisposed)
        {
            sparkOverlay = new PetSparkForm();
        }

        sparkOverlay.BurstAt(worldCenter);
    }

    private void HideSparkOverlay()
    {
        if (sparkOverlay == null || sparkOverlay.IsDisposed)
        {
            sparkOverlay = null;
            return;
        }

        sparkOverlay.Hide();
    }

    private void DestroySparkOverlay()
    {
        if (sparkOverlay == null)
        {
            return;
        }

        try
        {
            if (!sparkOverlay.IsDisposed)
            {
                sparkOverlay.Close();
                sparkOverlay.Dispose();
            }
        }
        catch
        {
            // 破棄失敗時は参照だけ切る
        }

        sparkOverlay = null;
    }

    private void DestroyBall()
    {
        if (ball == null)
        {
            return;
        }

        try
        {
            if (!ball.IsDisposed)
            {
                ball.SetDragCallbacks(null, null);
                ball.Close();
                ball.Dispose();
            }
        }
        catch
        {
            // 破棄失敗時は参照だけ切る
        }

        ball = null;
    }

    private void PlaceBallAwayFrom(PointF from, float minDistance)
    {
        if (ball == null)
        {
            return;
        }

        ball.PlaceAt(RandomBallPointAwayFrom(from, minDistance));
    }

    private void PlaceBallNear(PointF near, float distance)
    {
        if (ball == null)
        {
            return;
        }

        float angle = (float)(random.NextDouble() * Math.PI * 2.0);
        PointF topLeft = new(
            near.X + MathF.Cos(angle) * distance - PetBallForm.BallSize * 0.5f,
            near.Y + MathF.Sin(angle) * distance - PetBallForm.BallSize * 0.5f);
        ball.PlaceAt(topLeft);
    }

    private PointF RandomBallPointAwayFrom(PointF from, float minDistance)
    {
        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        int margin = 24;
        int maxX = Math.Max(work.Left + margin, work.Right - PetBallForm.BallSize - margin);
        int maxY = Math.Max(work.Top + margin, work.Bottom - PetBallForm.BallSize - margin);
        PointF best = new(work.Left + margin, work.Top + margin);
        float bestDist = 0f;
        for (int attempt = 0; attempt < 14; attempt++)
        {
            PointF candidate = new(
                random.Next(work.Left + margin, maxX + 1),
                random.Next(work.Top + margin, maxY + 1));
            float dx = candidate.X + PetBallForm.BallSize * 0.5f - from.X;
            float dy = candidate.Y + PetBallForm.BallSize * 0.5f - from.Y;
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

    private void ScatterInitialPositions()
    {
        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        for (int i = 0; i < pets.Count; i++)
        {
            pets[i].PlaceAt(RandomPoint(work));
        }
    }

    private void DiversifyIdlePets(PetForm? skipA, PetForm? skipB)
    {
        List<PetForm> toAssign = new(pets.Count);
        for (int i = 0; i < pets.Count; i++)
        {
            PetForm pet = pets[i];
            if (pet.IsDragging || pet.IsSleeping || pet.IsSinging)
            {
                continue;
            }

            if (ReferenceEquals(pet, skipA) || ReferenceEquals(pet, skipB))
            {
                continue;
            }

            pet.SetBrainControlled(false);
            pet.ClearLineFollowChase();
            toAssign.Add(pet);
        }

        toAssign = BuildShuffledOrder(toAssign);
        for (int i = 0; i < toAssign.Count; i++)
        {
            toAssign[i].RollInitialBehavior();
        }
    }

    private void WakeAllForGroup(List<PetForm> active)
    {
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].IsSleeping)
            {
                active[i].WakeFromSleep();
            }

            if (active[i].IsSinging)
            {
                active[i].StopSinging();
            }
        }
    }

    private void SetAllBrainControlled(bool enabled)
    {
        for (int i = 0; i < pets.Count; i++)
        {
            pets[i].SetBrainControlled(enabled);
        }
    }

    private List<PetForm> GetAvailablePets()
    {
        List<PetForm> list = new(pets.Count);
        for (int i = 0; i < pets.Count; i++)
        {
            if (!pets[i].IsDragging && !pets[i].IsSleeping && !pets[i].IsSinging)
            {
                list.Add(pets[i]);
            }
        }

        return list;
    }

    private List<PetForm> GetRecruitablePets()
    {
        List<PetForm> list = new(pets.Count);
        for (int i = 0; i < pets.Count; i++)
        {
            if (!pets[i].IsDisposed && !pets[i].IsDragging)
            {
                list.Add(pets[i]);
            }
        }

        return list;
    }

    private int CountRecruitablePets() => GetRecruitablePets().Count;

    private bool TryPickDistinctPair(List<PetForm> active, out PetForm? first, out PetForm? second)
    {
        first = null;
        second = null;
        if (active == null || active.Count < 2)
        {
            return false;
        }

        List<PetForm> shuffled = BuildShuffledOrder(active);
        first = shuffled[0];
        second = shuffled[1];
        return first != null && second != null && !ReferenceEquals(first, second);
    }

    private PointF RandomPoint(Rectangle work)
    {
        int margin = 32;
        int maxX = Math.Max(work.Left + margin, work.Right - PetForm.WindowSize - margin);
        int maxY = Math.Max(work.Top + margin, work.Bottom - PetForm.WindowSize - margin);
        return new PointF(
            random.Next(work.Left + margin, maxX + 1),
            random.Next(work.Top + margin, maxY + 1));
    }
}
