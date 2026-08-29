using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scene.DesktopPet
{
    /// <summary>
    /// プロセス内ペットの移動待機睡眠歌唱列簡易バトル遊ぶ
    /// </summary>
    public sealed class DesktopPetInProcessFlock
    {
        private enum FlockMode
        {
            Free = 0,
            Line = 1,
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

        private readonly System.Collections.Generic.List<ActorRef> actors;
        private readonly float viewHalf;
        private CancellationToken loopToken;
        private FlockMode mode = FlockMode.Free;
        private FlockMode lastPickedMode = FlockMode.Free;
        private float modeSeconds = 300f;
        private int battleA;
        private int battleB;
        private float battleRoundCooldown;
        private BattlePhase battlePhase;
        private int playA = -1;
        private int playB = -1;
        private int playTurn = -1;
        private PlayPhase playPhase;
        private float playPhaseCooldown;
        private Vector3 playKickTo;
        private Vector3 playWindupTo;
        private bool playKickChosen;
        private Transform ballTransform;
        private SpriteRenderer ballRenderer;
        private Vector3 ballFrom;
        private Vector3 ballTo;
        private float ballFlyElapsed;
        private float ballFlyDuration;
        private bool ballFlying;
        private bool isBallDragging;
        private Vector3 ballDragOffset;
        private const float BallScale = 0.12f;
        private const float BallInFront = 0.28f;
        private const float ApproachRadius = 0.32f;
        private const float SlamRadius = 0.30f;
        private const string SoccerBallResourcePath = "DesktopPet/SoccerBall";

        /// <summary>
        /// 群れ制御を生成する
        /// </summary>
        public DesktopPetInProcessFlock(
            System.Collections.Generic.IReadOnlyList<Transform> transforms,
            System.Collections.Generic.IReadOnlyList<DesktopPetSpriteAnimator> animators,
            System.Collections.Generic.IReadOnlyList<TextMesh> zzzLabels,
            float viewHalfExtent)
        {
            viewHalf = Mathf.Max(0.4f, viewHalfExtent);
            actors = new System.Collections.Generic.List<ActorRef>(transforms.Count);
            for (int i = 0; i < transforms.Count; i++)
            {
                actors.Add(new ActorRef
                {
                    Transform = transforms[i],
                    Animator = animators[i],
                    Zzz = zzzLabels != null && i < zzzLabels.Count ? zzzLabels[i] : null,
                    IdleRemaining = 0f,
                    SleepRemaining = 0f,
                    SingRemaining = 0f,
                    WalkSessionRemaining = 0f,
                    DragOffset = Vector3.zero
                });
            }

            modeSeconds = 0f;
            CreateBall(transforms.Count > 0 ? transforms[0] : null);
        }

        private void CreateBall(Transform reference)
        {
            GameObject ballObject = new GameObject("DesktopPetBall");
            if (reference != null)
            {
                ballObject.layer = reference.gameObject.layer;
                if (reference.parent != null)
                {
                    ballObject.transform.SetParent(reference.parent, false);
                }
            }

            ballObject.transform.localPosition = Vector3.forward;
            ballObject.transform.localScale = Vector3.one * BallScale;
            ballRenderer = ballObject.AddComponent<SpriteRenderer>();
            ballRenderer.sprite = LoadSoccerBallSprite();
            ballRenderer.sortingOrder = 40;
            ballTransform = ballObject.transform;
            ballObject.SetActive(false);
        }

        private static Sprite LoadSoccerBallSprite()
        {
            Sprite source = Resources.Load<Sprite>(SoccerBallResourcePath);
            if (source == null || source.texture == null)
            {
                Debug.LogError(
                    "[DesktopPetInProcessFlock] Resources/"
                    + SoccerBallResourcePath
                    + "が見つかりません");
                return null;
            }

            const int grid = 24;
            Texture2D src = source.texture;
            FilterMode previousFilter = src.filterMode;
            src.filterMode = FilterMode.Point;
            RenderTexture rt = RenderTexture.GetTemporary(grid, grid, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            Graphics.Blit(src, rt);
            src.filterMode = previousFilter;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D pixelated = new Texture2D(grid, grid, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            pixelated.ReadPixels(new Rect(0f, 0f, grid, grid), 0, 0);
            pixelated.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            float ppu = source.pixelsPerUnit * grid / Mathf.Max(1f, source.rect.width);
            return Sprite.Create(
                pixelated,
                new Rect(0f, 0f, grid, grid),
                new Vector2(0.5f, 0.5f),
                ppu);
        }

        /// <summary>
        /// 群れループを開始する
        /// </summary>
        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                loopToken = cancellationToken;
                System.Collections.Generic.List<int> startOrder = BuildShuffledIndices(actors.Count);
                for (int i = 0; i < startOrder.Count; i++)
                {
                    RollInitialBehavior(startOrder[i], cancellationToken);
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    float dt = Time.unscaledDeltaTime;
                    HandleDragging();
                    UpdateIdleTimers(dt);
                    UpdateSleepVisuals(dt);
                    UpdateSingVisuals(dt);
                    modeSeconds -= dt;
                    if (mode != FlockMode.PlaySolo && mode != FlockMode.PlayDuo)
                    {
                        HideBall();
                    }

                    switch (mode)
                    {
                        case FlockMode.Free:
                            await TickFreeAsync(cancellationToken);
                            break;
                        case FlockMode.Line:
                            await TickLineAsync(cancellationToken);
                            break;
                        case FlockMode.SimpleBattle:
                            await TickSimpleBattleAsync(cancellationToken);
                            TickLeftoverFree(cancellationToken);
                            break;
                        case FlockMode.PlaySolo:
                        case FlockMode.PlayDuo:
                            TickPlay(dt);
                            TickLeftoverFree(cancellationToken);
                            break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        private async UniTask TickFreeAsync(CancellationToken cancellationToken)
        {
            System.Collections.Generic.List<int> ready = new();
            for (int i = 0; i < actors.Count; i++)
            {
                ActorRef actor = actors[i];
                if (actor.IsDragging
                    || actor.SleepRemaining > 0f
                    || actor.SingRemaining > 0f
                    || actor.IsMoving
                    || actor.IdleRemaining > 0f)
                {
                    continue;
                }

                ready.Add(i);
            }

            ShuffleIndices(ready);
            for (int i = 0; i < ready.Count; i++)
            {
                RollInitialBehavior(ready[i], cancellationToken);
            }

            if (modeSeconds > 0f)
            {
                return;
            }

            PickNextFlockMode();
            await UniTask.CompletedTask;
        }

        private void PickNextFlockMode()
        {
            int recruitable = CountRecruitable();
            if (recruitable < 1)
            {
                EnterFreeMode();
                return;
            }

            if (recruitable < 2)
            {
                if (lastPickedMode == FlockMode.PlaySolo)
                {
                    EnterFreeMode();
                }
                else if (lastPickedMode == FlockMode.Free || UnityEngine.Random.value < 0.5f)
                {
                    BeginPlaySolo();
                }
                else
                {
                    EnterFreeMode();
                }

                return;
            }

            float line = lastPickedMode == FlockMode.Line ? 0.05f : 0.14f;
            float battle = lastPickedMode == FlockMode.SimpleBattle ? 0.06f : 0.28f;
            float solo = lastPickedMode == FlockMode.PlaySolo ? 0.04f : 0.10f;
            float duo = lastPickedMode == FlockMode.PlayDuo ? 0.06f : 0.30f;
            float free = lastPickedMode == FlockMode.Free ? 0.05f : 0.12f;
            float roll = UnityEngine.Random.value * (line + battle + solo + duo + free);
            if ((roll -= line) < 0f)
            {
                mode = FlockMode.Line;
                lastPickedMode = FlockMode.Line;
                modeSeconds = NextLongStateSeconds();
                lineOrder.Clear();
                ClearLineFollowFlags();
                WakeRecruitableActors();
            }
            else if ((roll -= battle) < 0f)
            {
                BeginSimpleBattle();
            }
            else if ((roll -= solo) < 0f)
            {
                BeginPlaySolo();
            }
            else if ((roll -= duo) < 0f)
            {
                BeginPlayDuo();
            }
            else
            {
                EnterFreeMode();
            }
        }

        private void EnterFreeMode()
        {
            mode = FlockMode.Free;
            lastPickedMode = FlockMode.Free;
            modeSeconds = NextLongStateSeconds();
        }

        private void RollInitialBehavior(
            int index,
            CancellationToken cancellationToken,
            bool excludeSleep = false,
            bool excludeIdle = false)
        {
            if (index < 0 || index >= actors.Count)
            {
                return;
            }

            float sleep = excludeSleep ? 0f : ScaleSoloWeight(index, 0, 0.22f);
            float sing = ScaleSoloWeight(index, 1, 0.20f);
            float walk = ScaleSoloWeight(index, 2, 0.46f);
            float idle = excludeIdle ? 0f : ScaleSoloWeight(index, 3, 0.12f);
            float sum = sleep + sing + walk + idle;
            if (sum <= 0.0001f)
            {
                BeginWalkSession(index, cancellationToken);
                return;
            }

            float roll = UnityEngine.Random.value * sum;
            if ((roll -= sleep) < 0f)
            {
                BeginSleep(index, NextLongStateSeconds());
                return;
            }

            if ((roll -= sing) < 0f)
            {
                BeginSing(index, NextLongStateSeconds());
                return;
            }

            if ((roll -= walk) < 0f)
            {
                BeginWalkSession(index, cancellationToken);
                return;
            }

            ActorRef actor = actors[index];
            actor.IdleRemaining = NextShortIdleSeconds();
            actors[index] = actor;
            actor.Animator?.SetAction(DesktopPetAction.Idle);
        }

        private float ScaleSoloWeight(int selfIndex, int bucket, float baseWeight)
        {
            if (actors.Count <= 1)
            {
                return baseWeight;
            }

            int sleep = 0;
            int sing = 0;
            int walk = 0;
            int idle = 0;
            for (int i = 0; i < actors.Count; i++)
            {
                if (i == selfIndex || actors[i].IsDragging)
                {
                    continue;
                }

                ActorRef other = actors[i];
                if (other.SleepRemaining > 0f)
                {
                    sleep++;
                }
                else if (other.SingRemaining > 0f)
                {
                    sing++;
                }
                else if (other.IsMoving || other.WalkSessionRemaining > 0f)
                {
                    walk++;
                }
                else
                {
                    idle++;
                }
            }

            int maxRare = Mathf.Max(1, (actors.Count + 1) / 3);
            int maxWalk = Mathf.Max(1, actors.Count - 1);
            float multiplier = bucket switch
            {
                0 => OccupancyMultiplier(sleep, maxRare),
                1 => OccupancyMultiplier(sing, maxRare),
                2 => OccupancyMultiplier(walk, maxWalk),
                _ => OccupancyMultiplier(idle, maxRare)
            };
            return Mathf.Max(0f, baseWeight * multiplier);
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

        private void TickLeftoverFree(CancellationToken cancellationToken)
        {
            System.Collections.Generic.List<int> ready = new();
            for (int i = 0; i < actors.Count; i++)
            {
                if (IsGroupParticipant(i))
                {
                    continue;
                }

                ActorRef actor = actors[i];
                if (actor.IsDragging
                    || actor.SleepRemaining > 0f
                    || actor.SingRemaining > 0f
                    || actor.IsMoving
                    || actor.IdleRemaining > 0f)
                {
                    continue;
                }

                ready.Add(i);
            }

            ShuffleIndices(ready);
            for (int i = 0; i < ready.Count; i++)
            {
                RollInitialBehavior(ready[i], cancellationToken);
            }
        }

        private void DiversifyLeftoverPets()
        {
            System.Collections.Generic.List<int> leftover = new();
            for (int i = 0; i < actors.Count; i++)
            {
                if (IsGroupParticipant(i) || actors[i].IsDragging)
                {
                    continue;
                }

                if (actors[i].SleepRemaining > 0f || actors[i].SingRemaining > 0f)
                {
                    continue;
                }

                ActorRef actor = actors[i];
                actor.IsMoving = false;
                actor.WalkSessionRemaining = 0f;
                actor.IdleRemaining = 0f;
                actor.LineFollowDirect = false;
                actors[i] = actor;
                leftover.Add(i);
            }

            ShuffleIndices(leftover);
            for (int i = 0; i < leftover.Count; i++)
            {
                RollInitialBehavior(leftover[i], loopToken);
            }
        }

        private bool IsGroupParticipant(int index)
        {
            switch (mode)
            {
                case FlockMode.SimpleBattle:
                    return index == battleA || index == battleB;
                case FlockMode.PlaySolo:
                    return index == playA;
                case FlockMode.PlayDuo:
                    return index == playA || index == playB;
                default:
                    return false;
            }
        }

        private static System.Collections.Generic.List<int> BuildShuffledIndices(int count)
        {
            System.Collections.Generic.List<int> list = new(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(i);
            }

            ShuffleIndices(list);
            return list;
        }

        private static void ShuffleIndices(System.Collections.Generic.List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private async UniTask TickLineAsync(CancellationToken cancellationToken)
        {
            if (CountRecruitable() < 2)
            {
                ClearLineFollowFlags();
                PickNextFlockMode();
                return;
            }

            EnsureLineOrder();
            if (lineOrder.Count < 2)
            {
                ClearLineFollowFlags();
                PickNextFlockMode();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            int leaderIndex = lineOrder[0];
            ActorRef leader = actors[leaderIndex];
            // 到着したらすぐ次のポイントへ向かう
            if (!leader.IsMoving)
            {
                IssueLeaderWander(leaderIndex, cancellationToken);
            }

            float spacing = 0.24f;
            float followSpeed = 1.3f;
            for (int i = 1; i < lineOrder.Count; i++)
            {
                int prevIndex = lineOrder[i - 1];
                int followerIndex = lineOrder[i];
                ActorRef prev = actors[prevIndex];
                ActorRef follower = actors[followerIndex];
                if (follower.Transform == null
                    || prev.Transform == null
                    || follower.IsDragging
                    || follower.SleepRemaining > 0f
                    || follower.SingRemaining > 0f)
                {
                    continue;
                }

                TickFollowLeaderLocal(followerIndex, prev.Transform.localPosition, spacing, followSpeed, dt);
            }

            if (modeSeconds <= 0f)
            {
                ClearLineFollowFlags();
                PickNextFlockMode();
            }

            await UniTask.CompletedTask;
        }

        private readonly System.Collections.Generic.List<int> lineOrder =
            new System.Collections.Generic.List<int>(8);

        private void EnsureLineOrder()
        {
            if (lineOrder.Count > 0)
            {
                // 脱落した個体を除き足りない個体を末尾へ足す
                for (int i = lineOrder.Count - 1; i >= 0; i--)
                {
                    int index = lineOrder[i];
                    if (index < 0
                        || index >= actors.Count
                        || actors[index].SleepRemaining > 0f
                        || actors[index].SingRemaining > 0f
                        || actors[index].IsDragging)
                    {
                        lineOrder.RemoveAt(i);
                    }
                }
            }

            if (lineOrder.Count == 0)
            {
                for (int i = 0; i < actors.Count; i++)
                {
                    if (actors[i].SleepRemaining > 0f
                        || actors[i].SingRemaining > 0f
                        || actors[i].IsDragging)
                    {
                        continue;
                    }

                    lineOrder.Add(i);
                }

                // シャッフルして先頭を決める
                for (int i = lineOrder.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    int tmp = lineOrder[i];
                    lineOrder[i] = lineOrder[j];
                    lineOrder[j] = tmp;
                }

                return;
            }

            for (int i = 0; i < actors.Count; i++)
            {
                if (lineOrder.Contains(i))
                {
                    continue;
                }

                if (actors[i].SleepRemaining > 0f
                    || actors[i].SingRemaining > 0f
                    || actors[i].IsDragging)
                {
                    continue;
                }

                lineOrder.Add(i);
            }
        }

        private void IssueLeaderWander(int leaderIndex, CancellationToken cancellationToken)
        {
            ActorRef leader = actors[leaderIndex];
            if (leader.Transform == null || leader.IsDragging)
            {
                return;
            }

            leader.LineFollowDirect = false;
            // MoveActorAsync到着時に次ポイントへ連鎖させる
            leader.WalkSessionRemaining = NextLongStateSeconds();
            leader.IdleRemaining = 0f;
            actors[leaderIndex] = leader;
            Vector3 from = leader.Transform.localPosition;
            Vector3 target = DistantLineMarchPoint(from);
            float travel = Vector2.Distance(new Vector2(from.x, from.y), new Vector2(target.x, target.y));
            float speed = UnityEngine.Random.Range(0.95f, 1.25f);
            float duration = Mathf.Clamp(travel / speed, 3.5f, 14f);
            MoveActorAsync(leaderIndex, target, duration, cancellationToken).Forget();
        }

        private void TickFollowLeaderLocal(
            int index,
            Vector3 leaderLocal,
            float spacing,
            float speed,
            float dt)
        {
            ActorRef actor = actors[index];
            if (actor.Transform == null)
            {
                return;
            }

            Vector3 pos = actor.Transform.localPosition;
            Vector3 delta = leaderLocal - pos;
            delta.z = 0f;
            float dist = delta.magnitude;
            actor.LineFollowDirect = true;
            actor.IdleRemaining = 0f;
            if (dist <= spacing * 1.08f)
            {
                actor.IsMoving = false;
                actor.Animator?.SetAction(DesktopPetAction.Walk);
                actors[index] = actor;
                return;
            }

            float step = speed * Mathf.Max(0.001f, dt);
            float move = Mathf.Min(step, dist - spacing);
            Vector3 next = pos + (delta / dist) * move;
            next = ClampLocal(next);
            actor.Transform.localPosition = next;
            actor.IsMoving = true;
            actor.Animator?.SetFacing(delta.x >= 0f ? DesktopPetFacing.AngleNeg45 : DesktopPetFacing.AnglePos45);
            actor.Animator?.SetAction(DesktopPetAction.Walk);
            actors[index] = actor;
        }

        private void ClearLineFollowFlags()
        {
            lineOrder.Clear();
            for (int i = 0; i < actors.Count; i++)
            {
                ActorRef actor = actors[i];
                actor.LineFollowDirect = false;
                actors[i] = actor;
            }
        }

        private async UniTask TickSimpleBattleAsync(CancellationToken cancellationToken)
        {
            if (!IsPlayableActor(battleA) || !IsPlayableActor(battleB) || modeSeconds <= 0f)
            {
                EndSimpleBattle();
                return;
            }

            FaceBattlePair();
            if (actors[battleA].IsMoving || actors[battleB].IsMoving)
            {
                await UniTask.CompletedTask;
                return;
            }

            battleRoundCooldown -= Time.unscaledDeltaTime;
            if (battleRoundCooldown > 0f)
            {
                await UniTask.CompletedTask;
                return;
            }

            switch (battlePhase)
            {
                case BattlePhase.Idle:
                    battlePhase = BattlePhase.Approach;
                    IssueBattleWindup(cancellationToken);
                    break;
                case BattlePhase.Approach:
                    battlePhase = BattlePhase.Clash;
                    IssueBattleApproach(cancellationToken);
                    break;
                case BattlePhase.Clash:
                    battlePhase = BattlePhase.Separate;
                    Vector3 sparkPos = (actors[battleA].Transform.localPosition
                        + actors[battleB].Transform.localPosition) * 0.5f;
                    SpawnSpark(sparkPos, actors[battleA].Transform);
                    actors[battleA].Animator?.SetAction(DesktopPetAction.Attack);
                    actors[battleB].Animator?.SetAction(DesktopPetAction.Attack);
                    IssueBattleKnockback(cancellationToken);
                    break;
                case BattlePhase.Separate:
                    battlePhase = BattlePhase.Idle;
                    actors[battleA].Animator?.SetAction(DesktopPetAction.Idle);
                    actors[battleB].Animator?.SetAction(DesktopPetAction.Idle);
                    FaceBattlePair();
                    battleRoundCooldown = UnityEngine.Random.Range(1.2f, 2.6f);
                    break;
            }

            await UniTask.CompletedTask;
        }

        private void BeginSimpleBattle()
        {
            System.Collections.Generic.List<int> pool = CollectRecruitableIndices();
            if (!TryPickDistinctPair(pool, out battleA, out battleB))
            {
                EnterFreeMode();
                return;
            }

            mode = FlockMode.SimpleBattle;
            lastPickedMode = FlockMode.SimpleBattle;
            modeSeconds = NextLongStateSeconds();
            WakeActor(battleA);
            WakeActor(battleB);
            CancelActorMove(battleA);
            CancelActorMove(battleB);
            DiversifyLeftoverPets();
            battlePhase = BattlePhase.Idle;
            battleRoundCooldown = 0.2f;
            Vector3 mid = BattleMidpoint();
            IssueBattleMove(battleA, BattleSlot(battleA, mid, 0.55f), 0.55f, loopToken);
            IssueBattleMove(battleB, BattleSlot(battleB, mid, 0.55f), 0.55f, loopToken);
            FaceBattlePair();
        }

        private void EndSimpleBattle()
        {
            if (battleA >= 0 && battleA < actors.Count)
            {
                CancelActorMove(battleA);
            }

            if (battleB >= 0 && battleB < actors.Count)
            {
                CancelActorMove(battleB);
            }

            battleA = -1;
            battleB = -1;
            battlePhase = BattlePhase.Idle;
            PickNextFlockMode();
        }

        private void BeginPlaySolo()
        {
            System.Collections.Generic.List<int> pool = CollectRecruitableIndices();
            if (pool.Count < 1)
            {
                HideBall();
                EnterFreeMode();
                return;
            }

            playA = pool[UnityEngine.Random.Range(0, pool.Count)];
            playB = -1;
            playTurn = playA;
            ShowBallAwayFrom(actors[playA].Transform.localPosition, 0.9f);
            WakeActor(playA);
            mode = FlockMode.PlaySolo;
            lastPickedMode = FlockMode.PlaySolo;
            modeSeconds = NextLongStateSeconds();
            playPhase = PlayPhase.Chase;
            playPhaseCooldown = 0f;
            playKickChosen = false;
            ChooseKickDestination();
            DiversifyLeftoverPets();
        }

        private void BeginPlayDuo()
        {
            System.Collections.Generic.List<int> pool = CollectRecruitableIndices();
            if (!TryPickDistinctPair(pool, out playA, out playB))
            {
                BeginPlaySolo();
                return;
            }

            playTurn = playA;
            ShowBallNear(actors[playA].Transform.localPosition, 0.45f);
            WakeActor(playA);
            WakeActor(playB);
            mode = FlockMode.PlayDuo;
            lastPickedMode = FlockMode.PlayDuo;
            modeSeconds = NextLongStateSeconds();
            playPhase = PlayPhase.Chase;
            playPhaseCooldown = 0f;
            playKickChosen = false;
            ChooseKickDestination();
            DiversifyLeftoverPets();
        }

        private void TickPlay(float dt)
        {
            if (modeSeconds <= 0f)
            {
                EndPlay();
                return;
            }

            if (playTurn < 0 || playTurn >= actors.Count || ballTransform == null)
            {
                EndPlay();
                return;
            }

            if (!TryKeepPlayTurn())
            {
                EndPlay();
                return;
            }

            ActorRef turn = actors[playTurn];
            if (isBallDragging)
            {
                TickPlayBallDragFollow(dt);
                return;
            }

            if (playPhaseCooldown > 0f)
            {
                playPhaseCooldown -= dt;
            }

            if (ballFlying)
            {
                ballFlyElapsed += dt;
                float t = Mathf.Clamp01(ballFlyElapsed / Mathf.Max(0.01f, ballFlyDuration));
                float eased = t * t * (3f - 2f * t);
                ballTransform.localPosition = Vector3.Lerp(ballFrom, ballTo, eased);
                if (t >= 1f)
                {
                    ballFlying = false;
                    ballTransform.localPosition = ballTo;
                    if (mode == FlockMode.PlayDuo)
                    {
                        playTurn = playTurn == playA ? playB : playA;
                    }

                    playPhase = PlayPhase.Chase;
                    playPhaseCooldown = 0.12f;
                    playKickChosen = false;
                    ChooseKickDestination();
                }

                return;
            }

            if (playPhase == PlayPhase.Kick)
            {
                if (playPhaseCooldown <= 0f)
                {
                    KickBallFromCurrentTurn();
                    playPhase = PlayPhase.BallFly;
                }

                FaceKickDirection(ref turn);
                actors[playTurn] = turn;
                return;
            }

            if (turn.Transform == null)
            {
                return;
            }

            ChooseKickDestination();
            Vector3 pos = turn.Transform.localPosition;
            Vector3 ballPos = ballTransform.localPosition;
            switch (playPhase)
            {
                case PlayPhase.Chase:
                    FaceChaseDirection(ref turn, pos, ballPos);
                    TickInProcessChase(ref turn, pos, ballPos, dt);
                    break;
                case PlayPhase.Windup:
                    FaceKickDirection(ref turn);
                    TickInProcessWindup(ref turn, pos, dt);
                    break;
                case PlayPhase.Charge:
                    FaceKickDirection(ref turn);
                    TickInProcessCharge(ref turn, pos, ballPos, dt);
                    break;
                default:
                    FaceChaseDirection(ref turn, pos, ballPos);
                    TickInProcessChase(ref turn, pos, ballPos, dt);
                    break;
            }

            actors[playTurn] = turn;
        }

        private void TickPlayBallDragFollow(float dt)
        {
            if (playTurn < 0 || playTurn >= actors.Count || ballTransform == null)
            {
                return;
            }

            playPhase = PlayPhase.Chase;
            playKickChosen = false;
            playPhaseCooldown = 0f;
            FollowBallWhileDragged(playTurn, dt);
            if (mode == FlockMode.PlayDuo && playA >= 0 && playB >= 0)
            {
                int other = playTurn == playA ? playB : playA;
                if (other >= 0
                    && other < actors.Count
                    && !actors[other].IsDragging
                    && actors[other].SleepRemaining <= 0f
                    && actors[other].SingRemaining <= 0f)
                {
                    FollowBallWhileDragged(other, dt);
                }
            }
        }

        private void FollowBallWhileDragged(int index, float dt)
        {
            ActorRef actor = actors[index];
            if (actor.Transform == null || ballTransform == null)
            {
                return;
            }

            Vector3 pos = actor.Transform.localPosition;
            Vector3 ballPos = ballTransform.localPosition;
            FaceChaseDirection(ref actor, pos, ballPos);
            StepToward(ref actor, pos, StandInFrontOfBall(pos, ballPos, useKickAim: false), 2.2f, dt);
            actors[index] = actor;
        }

        private void RestartPlayAfterBallDrag()
        {
            if (mode != FlockMode.PlaySolo && mode != FlockMode.PlayDuo)
            {
                return;
            }

            if (playTurn < 0 || playTurn >= actors.Count || ballTransform == null)
            {
                EndPlay();
                return;
            }

            ballFlying = false;
            playKickChosen = false;
            playPhase = PlayPhase.Chase;
            playPhaseCooldown = 0f;
            ChooseKickDestination();
        }

        private void TickInProcessChase(
            ref ActorRef turn,
            Vector3 pos,
            Vector3 ballPos,
            float dt)
        {
            Vector3 stand = StandInFrontOfBall(pos, ballPos, useKickAim: true);
            Vector3 delta = ballPos - pos;
            delta.z = 0f;
            float dist = delta.magnitude;
            float distStand = Vector2.Distance(new Vector2(pos.x, pos.y), new Vector2(stand.x, stand.y));
            if (dist <= ApproachRadius || distStand <= 0.06f)
            {
                EnsureKickDestinationHasTravel();
                if (TryGetKickDirection(out float nx, out float ny))
                {
                    playWindupTo = ClampLocal(pos - new Vector3(nx, ny, 0f) * 0.42f);
                    playPhase = PlayPhase.Windup;
                }
                else
                {
                    playPhase = PlayPhase.Charge;
                }

                return;
            }

            StepToward(ref turn, pos, stand, 1.65f, dt);
        }

        private void TickInProcessWindup(ref ActorRef turn, Vector3 pos, float dt)
        {
            if (!TryGetKickDirection(out _, out _))
            {
                playPhase = PlayPhase.Charge;
                return;
            }

            float dist = Vector2.Distance(
                new Vector2(pos.x, pos.y),
                new Vector2(playWindupTo.x, playWindupTo.y));
            if (dist <= 0.04f)
            {
                playPhase = PlayPhase.Charge;
                return;
            }

            StepToward(ref turn, pos, playWindupTo, 1.9f, dt);
        }

        private void TickInProcessCharge(
            ref ActorRef turn,
            Vector3 pos,
            Vector3 ballPos,
            float dt)
        {
            Vector3 stand = StandInFrontOfBall(pos, ballPos, useKickAim: true);
            Vector3 delta = ballPos - pos;
            delta.z = 0f;
            float dist = delta.magnitude;
            float distStand = Vector2.Distance(new Vector2(pos.x, pos.y), new Vector2(stand.x, stand.y));
            if (dist <= SlamRadius || distStand <= 0.06f)
            {
                turn.Animator?.SetAction(DesktopPetAction.Attack);
                turn.IsMoving = false;
                KickBallFromCurrentTurn();
                playPhase = PlayPhase.BallFly;
                playPhaseCooldown = 0f;
                return;
            }

            StepToward(ref turn, pos, stand, 2.6f, dt);
        }

        private void StepToward(
            ref ActorRef turn,
            Vector3 from,
            Vector3 to,
            float speed,
            float dt)
        {
            Vector3 delta = to - from;
            delta.z = 0f;
            float dist = delta.magnitude;
            if (dist <= 0.001f || turn.Transform == null)
            {
                turn.IsMoving = false;
                return;
            }

            float step = speed * Mathf.Max(0.001f, dt);
            Vector3 next = ClampLocal(from + (delta / dist) * Mathf.Min(step, dist));
            turn.Transform.localPosition = next;
            turn.IsMoving = true;
            turn.IdleRemaining = 0f;
            turn.WalkSessionRemaining = 0f;
            turn.Animator?.SetAction(DesktopPetAction.Walk);
        }

        private void ChooseKickDestination()
        {
            if (ballTransform == null || playTurn < 0)
            {
                return;
            }

            if (mode == FlockMode.PlayDuo && playA >= 0 && playB >= 0)
            {
                int receiver = playTurn == playA ? playB : playA;
                if (receiver >= 0 && receiver < actors.Count && actors[receiver].Transform != null)
                {
                    playKickTo = BallInFrontOf(
                        actors[receiver].Transform.localPosition,
                        ballTransform.localPosition);
                    playKickChosen = true;
                    return;
                }
            }

            if (playKickChosen)
            {
                return;
            }

            playKickTo = DistantLocalPoint(ballTransform.localPosition);
            EnsureKickDestinationHasTravel();
            playKickChosen = true;
        }

        private Vector3 BallInFrontOf(Vector3 monsterPos, Vector3 fromPos)
        {
            Vector3 delta = monsterPos - fromPos;
            delta.z = 0f;
            float length = delta.magnitude;
            Vector3 dir = length < 0.01f ? Vector3.right : delta / length;
            return ClampLocal(monsterPos - (dir * BallInFront));
        }

        private Vector3 StandInFrontOfBall(Vector3 petPos, Vector3 ballPos, bool useKickAim)
        {
            if (useKickAim && TryGetKickDirection(out float nx, out float ny))
            {
                return ClampLocal(ballPos - new Vector3(nx, ny, 0f) * BallInFront);
            }

            Vector3 delta = ballPos - petPos;
            delta.z = 0f;
            float length = delta.magnitude;
            Vector3 dir = length < 0.01f ? Vector3.right : delta / length;
            return ClampLocal(ballPos - (dir * BallInFront));
        }

        private bool TryGetKickDirection(out float nx, out float ny)
        {
            nx = 0f;
            ny = 0f;
            if (ballTransform == null)
            {
                return false;
            }

            Vector3 delta = playKickTo - ballTransform.localPosition;
            delta.z = 0f;
            float length = delta.magnitude;
            if (length < 0.05f)
            {
                return false;
            }

            nx = delta.x / length;
            ny = delta.y / length;
            return true;
        }

        private void FaceKickDirection(ref ActorRef turn)
        {
            float faceDx = playKickTo.x - (ballTransform != null ? ballTransform.localPosition.x : turn.Transform.localPosition.x);
            turn.Animator?.SetFacing(faceDx > 0.02f
                ? DesktopPetFacing.AngleNeg45
                : DesktopPetFacing.AnglePos45);
        }

        private static void FaceChaseDirection(ref ActorRef turn, Vector3 petPos, Vector3 ballPos)
        {
            float faceDx = ballPos.x - petPos.x;
            turn.Animator?.SetFacing(faceDx > 0.02f
                ? DesktopPetFacing.AngleNeg45
                : DesktopPetFacing.AnglePos45);
        }

        private void KickBallFromCurrentTurn()
        {
            if (ballTransform == null || playTurn < 0)
            {
                return;
            }

            EnsureKickDestinationHasTravel();
            ballFrom = ballTransform.localPosition;
            ballTo = ClampLocal(playKickTo);
            float travel = Vector2.Distance(
                new Vector2(ballFrom.x, ballFrom.y),
                new Vector2(ballTo.x, ballTo.y));
            ballFlyDuration = Mathf.Clamp(travel / 2.4f, 0.4f, 1.5f);
            ballFlyElapsed = 0f;
            ballFlying = true;
        }

        private bool TryKeepPlayTurn()
        {
            if (IsPlayableActor(playTurn))
            {
                return true;
            }

            if (mode == FlockMode.PlayDuo)
            {
                int other = playTurn == playA ? playB : playA;
                if (IsPlayableActor(other))
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

        private bool IsPlayableActor(int index)
        {
            if (index < 0 || index >= actors.Count)
            {
                return false;
            }

            ActorRef actor = actors[index];
            return actor.Transform != null
                && !actor.IsDragging
                && actor.SleepRemaining <= 0f
                && actor.SingRemaining <= 0f;
        }

        private void EnsureKickDestinationHasTravel()
        {
            if (ballTransform == null)
            {
                return;
            }

            Vector3 clamped = ClampLocal(playKickTo);
            float travel = Vector2.Distance(
                new Vector2(ballTransform.localPosition.x, ballTransform.localPosition.y),
                new Vector2(clamped.x, clamped.y));
            if (travel >= 0.35f)
            {
                playKickTo = clamped;
                return;
            }

            playKickTo = DistantLocalPoint(ballTransform.localPosition);
            playKickChosen = true;
        }

        private void EndPlay()
        {
            playA = -1;
            playB = -1;
            playTurn = -1;
            ballFlying = false;
            HideBall();
            PickNextFlockMode();
        }

        private void ShowBallAwayFrom(Vector3 from, float minDistance)
        {
            if (ballTransform == null)
            {
                return;
            }

            Vector3 best = DistantLocalPoint(from);
            for (int i = 0; i < 10; i++)
            {
                Vector3 candidate = RandomLocalPoint();
                float dist = Vector2.Distance(
                    new Vector2(from.x, from.y),
                    new Vector2(candidate.x, candidate.y));
                if (dist >= minDistance)
                {
                    best = candidate;
                    break;
                }
            }

            ballTransform.localPosition = best;
            ballTransform.gameObject.SetActive(true);
            ballFlying = false;
        }

        private void ShowBallNear(Vector3 near, float distance)
        {
            if (ballTransform == null)
            {
                return;
            }

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 pos = near + new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f);
            ballTransform.localPosition = ClampLocal(pos);
            ballTransform.gameObject.SetActive(true);
            ballFlying = false;
        }

        private void HideBall()
        {
            ballFlying = false;
            isBallDragging = false;
            if (ballTransform != null)
            {
                ballTransform.gameObject.SetActive(false);
            }
        }

        private System.Collections.Generic.List<int> CollectRecruitableIndices()
        {
            System.Collections.Generic.List<int> list = new();
            for (int i = 0; i < actors.Count; i++)
            {
                if (!actors[i].IsDragging && actors[i].Transform != null)
                {
                    list.Add(i);
                }
            }

            return list;
        }

        private void WakeRecruitableActors()
        {
            System.Collections.Generic.List<int> pool = CollectRecruitableIndices();
            for (int i = 0; i < pool.Count; i++)
            {
                WakeActor(pool[i]);
            }
        }

        private bool TryPickDistinctPair(
            System.Collections.Generic.List<int> pool,
            out int first,
            out int second)
        {
            first = -1;
            second = -1;
            if (pool == null || pool.Count < 2)
            {
                return false;
            }

            System.Collections.Generic.List<int> shuffled = new(pool);
            ShuffleIndices(shuffled);
            first = shuffled[0];
            second = shuffled[1];
            return first != second;
        }

        private void WakeActor(int index)
        {
            ActorRef actor = actors[index];
            actor.SleepRemaining = 0f;
            actor.SingRemaining = 0f;
            actor.IdleRemaining = 0f;
            actor.WalkSessionRemaining = 0f;
            if (actor.Zzz != null)
            {
                actor.Zzz.gameObject.SetActive(false);
            }

            actors[index] = actor;
        }

        private void IssueBattleWindup(CancellationToken cancellationToken)
        {
            Vector3 mid = BattleMidpoint();
            IssueBattleMove(battleA, BattleSlot(battleA, mid, 0.48f), 0.28f, cancellationToken);
            IssueBattleMove(battleB, BattleSlot(battleB, mid, 0.48f), 0.28f, cancellationToken);
        }

        private void IssueBattleApproach(CancellationToken cancellationToken)
        {
            Vector3 mid = BattleMidpoint();
            IssueBattleMove(battleA, BattleSlot(battleA, mid, 0.08f), 0.22f, cancellationToken);
            IssueBattleMove(battleB, BattleSlot(battleB, mid, 0.08f), 0.22f, cancellationToken);
        }

        private void IssueBattleKnockback(CancellationToken cancellationToken)
        {
            Vector3 mid = BattleMidpoint();
            IssueBattleMove(battleA, BattleKnockback(battleA, mid), 0.32f, cancellationToken);
            IssueBattleMove(battleB, BattleKnockback(battleB, mid), 0.32f, cancellationToken);
        }

        private void IssueBattleMove(
            int index,
            Vector3 localTarget,
            float duration,
            CancellationToken cancellationToken)
        {
            int other = index == battleA ? battleB : battleA;
            float faceDx = 0f;
            if (other >= 0 && other < actors.Count && actors[other].Transform != null)
            {
                faceDx = actors[other].Transform.localPosition.x - actors[index].Transform.localPosition.x;
            }

            MoveActorAsync(index, ClampLocal(localTarget), duration, cancellationToken, faceDx).Forget();
        }

        private Vector3 BattleMidpoint()
        {
            if (battleA < 0 || battleB < 0
                || actors[battleA].Transform == null
                || actors[battleB].Transform == null)
            {
                return Vector3.forward;
            }

            return (actors[battleA].Transform.localPosition
                + actors[battleB].Transform.localPosition) * 0.5f;
        }

        private Vector3 BattleSlot(int index, Vector3 mid, float fromMid)
        {
            ResolveBattleSide(index, mid, out float nx, out float ny);
            return ClampLocal(mid + new Vector3(nx * fromMid, ny * fromMid, 0f));
        }

        private Vector3 BattleKnockback(int index, Vector3 mid)
        {
            ResolveBattleSide(index, mid, out float nx, out float ny);
            float angle = Mathf.Atan2(ny, nx) + UnityEngine.Random.Range(-0.9f, 0.9f);
            float dist = UnityEngine.Random.Range(0.7f, 1.15f);
            return ClampLocal(mid + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f));
        }

        private void ResolveBattleSide(int index, Vector3 mid, out float nx, out float ny)
        {
            Vector3 pos = actors[index].Transform != null
                ? actors[index].Transform.localPosition
                : mid;
            Vector3 delta = pos - mid;
            delta.z = 0f;
            float length = delta.magnitude;
            if (length >= 0.04f)
            {
                nx = delta.x / length;
                ny = delta.y / length;
                return;
            }

            nx = index == battleA ? -1f : 1f;
            ny = 0f;
        }

        private void CancelActorMove(int index)
        {
            if (index < 0 || index >= actors.Count)
            {
                return;
            }

            ActorRef actor = actors[index];
            actor.MoveEpoch++;
            actor.IsMoving = false;
            actor.WalkSessionRemaining = 0f;
            actor.LineFollowDirect = false;
            actors[index] = actor;
        }

        private void FaceBattlePair()
        {
            if (battleA < 0 || battleB < 0 || battleA >= actors.Count || battleB >= actors.Count)
            {
                return;
            }

            if (actors[battleA].Transform == null || actors[battleB].Transform == null)
            {
                return;
            }

            float dxA = actors[battleB].Transform.localPosition.x - actors[battleA].Transform.localPosition.x;
            float dxB = -dxA;
            actors[battleA].Animator?.SetFacing(dxA > 0.02f
                ? DesktopPetFacing.AngleNeg45
                : DesktopPetFacing.AnglePos45);
            actors[battleB].Animator?.SetFacing(dxB > 0.02f
                ? DesktopPetFacing.AngleNeg45
                : DesktopPetFacing.AnglePos45);
        }

        private void HandleDragging()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            UnityEngine.Camera camera = null;
            for (int c = 0; c < UnityEngine.Camera.allCamerasCount; c++)
            {
                UnityEngine.Camera candidate = UnityEngine.Camera.allCameras[c];
                if (candidate != null && candidate.name.Contains("DesktopPet"))
                {
                    camera = candidate;
                    break;
                }
            }

            if (camera == null)
            {
                return;
            }

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(
                mouse.position.ReadValue().x,
                mouse.position.ReadValue().y,
                Mathf.Abs(camera.transform.position.z - 1f)));

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!TryBeginBallDrag(world))
                {
                    int nearest = FindNearestActor(world, 0.55f);
                    if (nearest >= 0)
                    {
                        ActorRef actor = actors[nearest];
                        actor.IsDragging = true;
                        actor.DragInterruptedSleep = actor.SleepRemaining > 0f;
                        if (actor.DragInterruptedSleep)
                        {
                            actor.SleepRemaining = 0f;
                            SetZzz(nearest, false);
                        }

                        if (actor.SingRemaining <= 0f)
                        {
                            actor.WalkSessionRemaining = 0f;
                            actor.IdleRemaining = 0f;
                        }

                        actor.DragOffset = actor.Transform.position - world;
                        actors[nearest] = actor;
                    }
                }
            }

            if (mouse.leftButton.isPressed)
            {
                if (isBallDragging && ballTransform != null)
                {
                    Vector3 next = world + ballDragOffset;
                    next.z = ballTransform.position.z;
                    ballTransform.position = next;
                    ballTransform.localPosition = ClampLocal(ballTransform.localPosition);
                }

                for (int i = 0; i < actors.Count; i++)
                {
                    ActorRef actor = actors[i];
                    if (!actor.IsDragging)
                    {
                        continue;
                    }

                    Vector3 next = world + actor.DragOffset;
                    next.z = actor.Transform.position.z;
                    actor.Transform.position = next;
                    actor.Transform.localPosition = ClampLocal(actor.Transform.localPosition);
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (isBallDragging)
                {
                    isBallDragging = false;
                    RestartPlayAfterBallDrag();
                }

                for (int i = 0; i < actors.Count; i++)
                {
                    ActorRef actor = actors[i];
                    if (!actor.IsDragging)
                    {
                        continue;
                    }

                    actor.IsDragging = false;
                    bool keepSinging = actor.SingRemaining > 0f;
                    bool interruptedSleep = actor.DragInterruptedSleep;
                    actor.DragInterruptedSleep = false;
                    actors[i] = actor;
                    if (interruptedSleep)
                    {
                        RollInitialBehavior(i, loopToken, excludeSleep: true, excludeIdle: true);
                    }
                    else if (!keepSinging)
                    {
                        actor.IdleRemaining = 0f;
                        actors[i] = actor;
                        RollInitialBehavior(i, loopToken, excludeIdle: true);
                    }
                }
            }
        }

        private bool TryBeginBallDrag(Vector3 world)
        {
            if (isBallDragging
                || ballTransform == null
                || !ballTransform.gameObject.activeSelf
                || (mode != FlockMode.PlaySolo && mode != FlockMode.PlayDuo))
            {
                return false;
            }

            if (Vector3.Distance(ballTransform.position, world) > 0.42f)
            {
                return false;
            }

            isBallDragging = true;
            ballFlying = false;
            ballDragOffset = ballTransform.position - world;
            playPhase = PlayPhase.Chase;
            playKickChosen = false;
            playPhaseCooldown = 0f;
            return true;
        }

        private async UniTaskVoid MoveActorAsync(
            int index,
            Vector3 localTarget,
            float duration,
            CancellationToken cancellationToken,
            float? faceDeltaX = null)
        {
            ActorRef actor = actors[index];
            if (actor.IsDragging || actor.SleepRemaining > 0f || actor.SingRemaining > 0f || actor.Transform == null)
            {
                return;
            }

            actor.MoveEpoch++;
            int epoch = actor.MoveEpoch;
            actor.IsMoving = true;
            actors[index] = actor;
            Vector3 from = actor.Transform.localPosition;
            float dx = faceDeltaX ?? (localTarget.x - from.x);
            actor.Animator?.SetFacing(dx > 0.02f
                ? DesktopPetFacing.AngleNeg45
                : dx < -0.02f
                    ? DesktopPetFacing.AnglePos45
                    : DesktopPetFacing.AnglePos45);
            actor.Animator?.SetAction(DesktopPetAction.Walk);

            float elapsed = 0f;
            duration = Mathf.Max(0.18f, duration);
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                actor = actors[index];
                if (actor.MoveEpoch != epoch
                    || actor.IsDragging
                    || actor.SleepRemaining > 0f
                    || actor.SingRemaining > 0f
                    || actor.LineFollowDirect)
                {
                    if (actor.MoveEpoch == epoch)
                    {
                        actor.IsMoving = false;
                        actors[index] = actor;
                    }

                    return;
                }

                elapsed += Time.unscaledDeltaTime;
                if (actor.WalkSessionRemaining > 0f)
                {
                    actor.WalkSessionRemaining -= Time.unscaledDeltaTime;
                    actors[index] = actor;
                }

                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                actor.Transform.localPosition = Vector3.Lerp(from, localTarget, t);
                if (mode == FlockMode.SimpleBattle && IsGroupParticipant(index))
                {
                    FaceBattlePair();
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            actor = actors[index];
            actor.IsMoving = false;
            if (mode == FlockMode.SimpleBattle && IsGroupParticipant(index))
            {
                actors[index] = actor;
                FaceBattlePair();
                return;
            }

            if (actor.WalkSessionRemaining > 1f
                && actor.SleepRemaining <= 0f
                && actor.SingRemaining <= 0f
                && !actor.IsDragging)
            {
                actors[index] = actor;
                Vector3 origin = actor.Transform != null ? actor.Transform.localPosition : Vector3.zero;
                Vector3 next = DistantLocalPoint(origin);
                float travel = Vector2.Distance(new Vector2(origin.x, origin.y), new Vector2(next.x, next.y));
                float speed = UnityEngine.Random.Range(0.75f, 1.0f);
                float nextDuration = Mathf.Clamp(travel / speed, 2.2f, 6f);
                MoveActorAsync(index, next, nextDuration, cancellationToken).Forget();
                return;
            }

            actor.WalkSessionRemaining = 0f;
            actors[index] = actor;
            if (actor.SleepRemaining <= 0f && actor.SingRemaining <= 0f && !actor.IsDragging)
            {
                RollInitialBehavior(index, cancellationToken, excludeIdle: true);
            }
        }

        private void UpdateIdleTimers(float dt)
        {
            for (int i = 0; i < actors.Count; i++)
            {
                ActorRef actor = actors[i];
                if (actor.IsDragging
                    || actor.IsMoving
                    || actor.SleepRemaining > 0f
                    || actor.SingRemaining > 0f)
                {
                    continue;
                }

                if (actor.IdleRemaining > 0f)
                {
                    actor.IdleRemaining -= dt;
                    actors[i] = actor;
                }
            }
        }

        private void BeginWalkSession(int index, CancellationToken cancellationToken)
        {
            ActorRef actor = actors[index];
            actor.WalkSessionRemaining = NextLongStateSeconds();
            actor.IdleRemaining = 0f;
            actors[index] = actor;
            MoveActorAsync(index, RandomLocalPoint(), 3.2f, cancellationToken).Forget();
        }

        private void BeginSleep(int index, float seconds)
        {
            ActorRef actor = actors[index];
            actor.SleepRemaining = Mathf.Max(300f, seconds);
            actor.SingRemaining = 0f;
            actor.WalkSessionRemaining = 0f;
            actor.IdleRemaining = 0f;
            actor.IsMoving = false;
            actor.Animator?.SetFacing(DesktopPetFacing.Front);
            actor.Animator?.SetAction(DesktopPetAction.Idle);
            actors[index] = actor;
            SetZzz(index, true);
            SetNote(index, false);
        }

        private void BeginSing(int index, float seconds)
        {
            ActorRef actor = actors[index];
            actor.SingRemaining = Mathf.Max(300f, seconds);
            actor.SleepRemaining = 0f;
            actor.WalkSessionRemaining = 0f;
            actor.IdleRemaining = 0f;
            actor.IsMoving = false;
            actor.Animator?.SetFacing(DesktopPetFacing.Front);
            actor.Animator?.SetAction(DesktopPetAction.Walk);
            actors[index] = actor;
            SetZzz(index, false);
            SetNote(index, true);
        }

        private void UpdateSleepVisuals(float dt)
        {
            for (int i = 0; i < actors.Count; i++)
            {
                ActorRef actor = actors[i];
                if (actor.SleepRemaining <= 0f)
                {
                    continue;
                }

                actor.SleepRemaining -= dt;
                actor.Animator?.SetFacing(DesktopPetFacing.Front);
                actor.Animator?.SetAction(DesktopPetAction.Idle);
                if (actor.Zzz != null)
                {
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f + i);
                    actor.Zzz.transform.localPosition = new Vector3(0.25f, 0.55f + pulse * 0.08f, -0.1f);
                    int phase = Mathf.FloorToInt((Time.unscaledTime * 2.5f) + i) % 3;
                    actor.Zzz.text = phase == 0 ? "z" : phase == 1 ? "zz" : "zzz";
                    actor.Zzz.gameObject.SetActive(true);
                }

                if (actor.SleepRemaining <= 0f)
                {
                    actor.SleepRemaining = 0f;
                    actor.IdleRemaining = 0f;
                    SetZzz(i, false);
                    actors[i] = actor;
                    RollInitialBehavior(i, loopToken);
                    continue;
                }

                actors[i] = actor;
            }
        }

        private void UpdateSingVisuals(float dt)
        {
            for (int i = 0; i < actors.Count; i++)
            {
                ActorRef actor = actors[i];
                if (actor.SingRemaining <= 0f)
                {
                    continue;
                }

                actor.SingRemaining -= dt;
                actor.Animator?.SetFacing(DesktopPetFacing.Front);
                actor.Animator?.SetAction(DesktopPetAction.Walk);
                if (actor.Zzz != null)
                {
                    float pulse = 0.55f + 0.2f * Mathf.Sin(Time.unscaledTime * 4f + i);
                    actor.Zzz.transform.localPosition = new Vector3(0.28f, 0.58f + pulse * 0.12f, -0.1f);
                    int phase = Mathf.FloorToInt((Time.unscaledTime * 3f) + i) % 4;
                    actor.Zzz.text = phase switch
                    {
                        0 => "♪",
                        1 => "♫",
                        2 => "♩",
                        _ => "♬"
                    };
                    actor.Zzz.color = Color.HSVToRGB(
                        Mathf.Repeat((Time.unscaledTime * 0.45f) + (i * 0.19f) + (phase * 0.13f), 1f),
                        0.85f,
                        1f);
                    actor.Zzz.gameObject.SetActive(true);
                }

                if (actor.SingRemaining <= 0f)
                {
                    actor.SingRemaining = 0f;
                    actor.IdleRemaining = 0f;
                    SetNote(i, false);
                    actors[i] = actor;
                    RollInitialBehavior(i, loopToken);
                    continue;
                }

                actors[i] = actor;
            }
        }

        private void SetZzz(int index, bool enabled)
        {
            ActorRef actor = actors[index];
            if (actor.Zzz != null)
            {
                actor.Zzz.gameObject.SetActive(enabled);
                if (enabled)
                {
                    actor.Zzz.text = "zzz";
                    actor.Zzz.color = new Color(0.25f, 0.45f, 0.85f, 1f);
                }
            }
        }

        private void SetNote(int index, bool enabled)
        {
            ActorRef actor = actors[index];
            if (actor.Zzz != null)
            {
                actor.Zzz.gameObject.SetActive(enabled);
                if (enabled)
                {
                    actor.Zzz.text = "♪";
                    actor.Zzz.color = Color.HSVToRGB(UnityEngine.Random.value, 0.85f, 1f);
                }
            }
        }

        private int CountRecruitable()
        {
            int count = 0;
            for (int i = 0; i < actors.Count; i++)
            {
                if (!actors[i].IsDragging && actors[i].Transform != null)
                {
                    count++;
                }
            }

            return count;
        }

        private int FindNearestActor(Vector3 world, float maxDistance)
        {
            int best = -1;
            float bestDist = maxDistance;
            for (int i = 0; i < actors.Count; i++)
            {
                float d = Vector3.Distance(actors[i].Transform.position, world);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }

        private Vector3 RandomLocalPoint()
        {
            return ClampLocal(new Vector3(
                UnityEngine.Random.Range(-viewHalf, viewHalf),
                UnityEngine.Random.Range(-viewHalf, viewHalf),
                1f));
        }

        /// <summary>
        /// 列移動用に画面を横断する遠い移動先を返す
        /// </summary>
        private Vector3 DistantLineMarchPoint(Vector3 from)
        {
            float minDistance = viewHalf * 1.55f;
            Vector3 best = RandomLocalPoint();
            float bestDist = 0f;
            for (int attempt = 0; attempt < 18; attempt++)
            {
                Vector3 candidate;
                if (attempt < 12)
                {
                    float x = from.x < 0f
                        ? UnityEngine.Random.Range(viewHalf * 0.35f, viewHalf)
                        : UnityEngine.Random.Range(-viewHalf, -viewHalf * 0.35f);
                    float y = from.y < 0f
                        ? UnityEngine.Random.Range(viewHalf * 0.35f, viewHalf)
                        : UnityEngine.Random.Range(-viewHalf, -viewHalf * 0.35f);
                    candidate = ClampLocal(new Vector3(x, y, 1f));
                }
                else
                {
                    candidate = RandomLocalPoint();
                }

                float dist = Vector2.Distance(
                    new Vector2(from.x, from.y),
                    new Vector2(candidate.x, candidate.y));
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
        /// 現在位置から十分離れた移動先を返す
        /// </summary>
        private Vector3 DistantLocalPoint(Vector3 from)
        {
            float minDistance = viewHalf * 0.85f;
            Vector3 best = RandomLocalPoint();
            float bestDist = 0f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 candidate = RandomLocalPoint();
                float dist = Vector2.Distance(
                    new Vector2(from.x, from.y),
                    new Vector2(candidate.x, candidate.y));
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

        private Vector3 ClampLocal(Vector3 local)
        {
            local.x = Mathf.Clamp(local.x, -viewHalf, viewHalf);
            local.y = Mathf.Clamp(local.y, -viewHalf, viewHalf);
            local.z = 1f;
            return local;
        }

        private static float NextShortIdleSeconds() =>
            UnityEngine.Random.Range(3f, 10f);

        private static float NextLongStateSeconds() =>
            UnityEngine.Random.Range(300f, 600f);

        private static DesktopPetFacing RollFacing() =>
            UnityEngine.Random.value < 0.5f
                ? DesktopPetFacing.AnglePos45
                : DesktopPetFacing.AngleNeg45;

        private static void SpawnSpark(Vector3 localMidpoint, Transform reference)
        {
            GameObject root = new GameObject("DesktopPetSpark");
            int layer = 0;
            if (reference != null)
            {
                if (reference.parent != null)
                {
                    root.transform.SetParent(reference.parent, false);
                }

                layer = reference.gameObject.layer;
            }

            localMidpoint.z = 0.85f;
            root.transform.localPosition = localMidpoint;
            root.layer = layer;
            for (int i = 0; i < 16; i++)
            {
                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(dot.GetComponent<Collider>());
                dot.transform.SetParent(root.transform, false);
                dot.layer = layer;
                dot.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.04f, 0.09f);
                Vector3 dir = UnityEngine.Random.insideUnitSphere;
                dir.z = 0f;
                if (dir.sqrMagnitude < 0.001f)
                {
                    dir = Vector3.right;
                }

                dot.transform.localPosition = dir.normalized * UnityEngine.Random.Range(0.02f, 0.08f);
                MeshRenderer meshRenderer = dot.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.material.color = UnityEngine.Random.Range(0, 3) switch
                    {
                        0 => Color.white,
                        1 => new Color(1f, 0.96f, 0.35f),
                        _ => new Color(1f, 0.78f, 0.2f)
                    };
                }
            }

            Object.Destroy(root, 0.5f);
        }

        private struct ActorRef
        {
            public Transform Transform;
            public DesktopPetSpriteAnimator Animator;
            public TextMesh Zzz;
            public float IdleRemaining;
            public float SleepRemaining;
            public float SingRemaining;
            public float WalkSessionRemaining;
            public bool IsMoving;
            public bool DragInterruptedSleep;
            public bool IsDragging;
            public bool LineFollowDirect;
            public int MoveEpoch;
            public Vector3 DragOffset;
        }
    }
}
