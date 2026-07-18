using System.Collections.Generic;
using ClayEditor.Rigging;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// リアルタイム戦闘の1体HP・攻撃力・ガッツ・機動力・技・部位残数・硬直を持つ
    /// </summary>
    public sealed class BattleUnit
    {
        private readonly ProceduralMotionCharacter motion;
        private readonly ModelPartLossController partLoss;

        private readonly Dictionary<BonePart, int> remainingPartCount = new Dictionary<BonePart, int>();
        private readonly List<int> limbIndices = new List<int>();
        private readonly List<int> removedLimbOrder = new List<int>();
        private readonly HashSet<int> removedLimbs = new HashSet<int>();

        private readonly int speed;
        private readonly float baseGutsGain;
        private readonly float lossSpeedBonusPerPart;

        public BattleUnit(
            string name,
            int maxHp, int attack, int defense, int speed,
            IReadOnlyList<MotionType> attackMotions,
            ProceduralMotionCharacter motion,
            ModelPartLossController partLoss,
            float maxGuts = 100f,
            float baseGutsGainPerSecond = 16f,
            float lossSpeedBonusPerPart = 0.15f)
        {
            Name = name;
            MaxHp = Mathf.Max(1, maxHp);
            CurrentHp = MaxHp;
            Attack = attack;
            Defense = defense;
            Speed = speed;

            MaxGuts = Mathf.Max(1f, maxGuts);
            Guts = 0f;
            InitialGuts = 50f;

            baseGutsGain = baseGutsGainPerSecond;
            this.lossSpeedBonusPerPart = lossSpeedBonusPerPart;
            this.speed = speed;

            this.motion = motion;
            this.partLoss = partLoss;

            if (partLoss != null)
            {
                foreach (var limb in partLoss.Limbs)
                {
                    limbIndices.Add(limb.Index);
                    remainingPartCount.TryGetValue(limb.Part, out int c);
                    remainingPartCount[limb.Part] = c + 1;
                }
            }

            Moves = new List<AttackMove>();
            if (attackMotions != null)
            {
                foreach (MotionType m in attackMotions)
                {
                    if (!MotionPartRequirement.IsAttack(m))
                    {
                        continue;
                    }

                    if (!HasRequiredPartAvailable(MotionPartRequirement.GetRequiredPart(m)))
                    {
                        continue;
                    }

                    Moves.Add(new AttackMove(m));
                }
            }
        }

        /// <summary>
        /// 表示名
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 最大HP
        /// </summary>
        public int MaxHp { get; }

        /// <summary>
        /// 現在HP
        /// </summary>
        public int CurrentHp { get; private set; }

        /// <summary>
        /// 攻撃力
        /// </summary>
        public int Attack { get; }

        /// <summary>
        /// 防御力
        /// </summary>
        public int Defense { get; }

        /// <summary>
        /// 速度ステータス
        /// </summary>
        public int Speed { get; }

        /// <summary>
        /// 最大ガッツ
        /// </summary>
        public float MaxGuts { get; }

        /// <summary>
        /// 現在ガッツ
        /// </summary>
        public float Guts { get; private set; }

        /// <summary>
        /// 戦闘開始時ガッツ
        /// </summary>
        public float InitialGuts { get; private set; }

        /// <summary>
        /// 攻撃技の一覧
        /// </summary>
        public List<AttackMove> Moves { get; }

        /// <summary>
        /// 現在の移動意図(-1=接近,0=停止,+1=後退)
        /// </summary>
        public int MovementIntent { get; set; }

        /// <summary>
        /// 残りの硬直時間(秒)0より大きい間は行動できない
        /// </summary>
        public float RecoveryTimer { get; private set; }

        /// <summary>
        /// 攻撃技の硬直残り時間(秒)
        /// </summary>
        public float AttackActionRemaining { get; private set; }

        /// <summary>
        /// 攻撃技を実行中か
        /// </summary>
        public bool IsPerformingAttack => AttackActionRemaining > 0f;

        /// <summary>
        /// 欠損した部位の数
        /// </summary>
        public int LostPartCount => removedLimbs.Count;

        /// <summary>
        /// 欠損数に応じた機動・ガッツ回復の倍率(欠損が増えるほど手数が増える)
        /// </summary>
        public float SpeedFactor => 1f + lossSpeedBonusPerPart * LostPartCount;

        /// <summary>
        /// 現在の移動速度(単位/秒)
        /// </summary>
        public float MoveSpeed => BattleStatusBalance.ComputeMoveSpeed(speed, SpeedFactor);

        /// <summary>
        /// 参照距離を基準にした現在のステップ移動距離を返す
        /// </summary>
        /// <param name="referenceStepDistance">既定速度時のステップ距離</param>
        public float ResolveStepDistance(float referenceStepDistance)
        {
            return BattleStatusBalance.ComputeStepDistance(speed, SpeedFactor, referenceStepDistance);
        }

        /// <summary>
        /// 現在のガッツ回復速度(/秒)
        /// </summary>
        public float GutsGainPerSecond => baseGutsGain * SpeedFactor;

        /// <summary>
        /// 撃破されたか
        /// </summary>
        public bool IsDefeated => CurrentHp <= 0;

        /// <summary>
        /// 行動可能か(硬直していないか)
        /// </summary>
        public bool CanAct => RecoveryTimer <= 0f;

        /// <summary>
        /// 戦闘開始時ガッツを設定する
        /// </summary>
        public void InitializeBattleGuts(float initialGuts)
        {
            InitialGuts = Mathf.Clamp(initialGuts, 0f, MaxGuts);
            Guts = InitialGuts;
        }

        /// <summary>
        /// 技の現在命中率を返す
        /// </summary>
        public float GetHitRate(int moveIndex)
        {
            if (moveIndex < 0 || moveIndex >= Moves.Count)
            {
                return 0f;
            }

            AttackMove move = Moves[moveIndex];
            return BattleCombatRules.ComputeHitRate(move.Accuracy, Guts, MaxGuts);
        }

        /// <summary>
        /// ガッツのみ消費する
        /// </summary>
        public void ConsumeGuts(float amount)
        {
            Guts = Mathf.Max(0f, Guts - amount);
        }

        /// <summary>
        /// 硬直を開始する
        /// </summary>
        public void BeginRecovery(float duration)
        {
            RecoveryTimer = Mathf.Max(RecoveryTimer, duration);
        }

        /// <summary>
        /// 時間経過でガッツ回復と硬直消化を進める
        /// </summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <param name="gainGuts">ガッツを回復するか</param>
        public void Tick(float deltaTime, bool gainGuts = true)
        {
            if (gainGuts)
            {
                Guts = Mathf.Min(MaxGuts, Guts + GutsGainPerSecond * deltaTime);
            }
            if (RecoveryTimer > 0f)
            {
                RecoveryTimer = Mathf.Max(0f, RecoveryTimer - deltaTime);
            }

            if (AttackActionRemaining > 0f)
            {
                AttackActionRemaining = Mathf.Max(0f, AttackActionRemaining - deltaTime);
            }
        }

        /// <summary>
        /// 部位欠損により・その技が使えるか(部位が残っているか)を返す
        /// </summary>
        public bool IsMoveUsableByPart(int moveIndex)
        {
            if (moveIndex < 0 || moveIndex >= Moves.Count)
            {
                return false;
            }

            BonePart required = Moves[moveIndex].RequiredPart;
            return HasRequiredPartAvailable(required);
        }

        /// <summary>
        /// 指定部位のリムが1つ以上残っているかを返す
        /// </summary>
        public bool HasRequiredPartAvailable(BonePart required)
        {
            if (required == BonePart.Body)
            {
                return true;
            }

            return CountRemainingLimbsOfPart(required) > 0;
        }

        /// <summary>
        /// 次に復旧するリム番号を返す
        /// </summary>
        public int? PeekNextRestoreLimbIndex()
        {
            if (removedLimbOrder.Count == 0)
            {
                return null;
            }

            int limbIndex = removedLimbOrder[0];
            if (!removedLimbs.Contains(limbIndex))
            {
                removedLimbOrder.RemoveAt(0);
                return PeekNextRestoreLimbIndex();
            }

            return limbIndex;
        }

        /// <summary>
        /// 部位修復の見た目を開始する
        /// </summary>
        public void BeginPartRepairVisual(int limbIndex)
        {
            partLoss?.BeginGradualRestoreLimb(limbIndex);
        }

        /// <summary>
        /// 部位修復の見た目進捗を更新する
        /// </summary>
        public void SetPartRepairVisualProgress(int limbIndex, float progress)
        {
            partLoss?.SetGradualRestoreProgress(limbIndex, progress);
        }

        /// <summary>
        /// 部位修復の見た目をキャンセルする
        /// </summary>
        public void CancelPartRepairVisual()
        {
            partLoss?.CancelGradualRestoreLimb();
        }

        /// <summary>
        /// 欠損した部位を1つ復旧する
        /// </summary>
        public bool TryRestoreOnePart()
        {
            if (removedLimbOrder.Count == 0)
            {
                return false;
            }

            int limbIndex = removedLimbOrder[0];
            if (!removedLimbs.Contains(limbIndex))
            {
                removedLimbOrder.RemoveAt(0);
                return TryRestoreOnePart();
            }

            if (partLoss != null && !partLoss.RestoreLimb(limbIndex))
            {
                return false;
            }

            removedLimbOrder.RemoveAt(0);
            removedLimbs.Remove(limbIndex);
            BonePart part = FindLimbPart(limbIndex);
            remainingPartCount.TryGetValue(part, out int count);
            remainingPartCount[part] = count + 1;
            return true;
        }

        /// <summary>
        /// 欠損した部位をすべて元に戻す勝敗演出などで見た目を完全復元する
        /// </summary>
        public void RestoreAllParts()
        {
            partLoss?.RestoreAll();

            removedLimbs.Clear();
            removedLimbOrder.Clear();
            remainingPartCount.Clear();

            if (partLoss != null)
            {
                foreach (var limb in partLoss.Limbs)
                {
                    remainingPartCount.TryGetValue(limb.Part, out int c);
                    remainingPartCount[limb.Part] = c + 1;
                }
            }
        }

        /// <summary>
        /// 現在の間合いで・その技を今すぐ使えるか(部位・間合い・ガッツ・硬直)を返す
        /// </summary>
        public bool CanUseMove(int moveIndex, float distance)
        {
            if (!CanAct || !IsMoveUsableByPart(moveIndex))
            {
                return false;
            }

            AttackMove move = Moves[moveIndex];
            return move.IsInRange(distance) && Guts >= move.GutsCost;
        }

        /// <summary>
        /// 技の使用を確定し・ガッツ消費と硬直開始を行う
        /// </summary>
        public void ConsumeForMove(AttackMove move)
        {
            Guts = Mathf.Max(0f, Guts - move.GutsCost);
            RecoveryTimer = move.Recovery;
            AttackActionRemaining = move.Recovery;
        }

        /// <summary>
        /// ダメージを受ける
        /// </summary>
        public void TakeDamage(int damage)
        {
            CurrentHp = Mathf.Clamp(CurrentHp - Mathf.Max(0, damage), 0, MaxHp);
        }

        /// <summary>
        /// 攻撃命中時に相手の破壊対象部位から1つ欠損させ・欠損種類をlostPartで返す
        /// </summary>
        public bool TryLosePart(AttackMove move, out BonePart lostPart, out int lostLimbIndex)
        {
            lostLimbIndex = -1;
            if (move == null)
            {
                lostPart = BonePart.Body;
                return false;
            }

            return TryLosePart(move.TargetDestroyPart, out lostPart, out lostLimbIndex);
        }

        /// <summary>
        /// 指定部位(または全身攻撃なら残存部位)から1つ欠損させ・欠損種類をlostPartで返す
        /// </summary>
        public bool TryLosePart(BonePart targetPart, out BonePart lostPart, out int lostLimbIndex)
        {
            lostPart = BonePart.Body;
            lostLimbIndex = -1;

            var remaining = new List<int>();
            for (int i = 0; i < limbIndices.Count; i++)
            {
                int limbIndex = limbIndices[i];
                if (removedLimbs.Contains(limbIndex))
                {
                    continue;
                }

                BonePart limbPart = FindLimbPart(limbIndex);
                if (targetPart == BonePart.Body || limbPart == targetPart)
                {
                    remaining.Add(limbIndex);
                }
            }

            if (remaining.Count == 0)
            {
                return false;
            }

            int pick = remaining[Random.Range(0, remaining.Count)];
            BonePart pickedPart = FindLimbPart(pick);

            partLoss?.RemoveLimb(pick);

            removedLimbs.Add(pick);
            removedLimbOrder.Add(pick);
            if (remainingPartCount.TryGetValue(pickedPart, out int c))
            {
                remainingPartCount[pickedPart] = Mathf.Max(0, c - 1);
            }

            lostPart = pickedPart;
            lostLimbIndex = pick;
            return true;
        }

        /// <summary>
        /// 同期された部位欠損を適用する
        /// </summary>
        public bool ApplySyncedPartLoss(BonePart targetPart, out BonePart lostPart)
        {
            return ApplySyncedPartLoss(-1, targetPart, out lostPart);
        }

        /// <summary>
        /// 同期された部位欠損を適用する
        /// </summary>
        public bool ApplySyncedPartLoss(int limbIndex, BonePart targetPart, out BonePart lostPart)
        {
            lostPart = BonePart.Body;

            if (limbIndex >= 0)
            {
                if (removedLimbs.Contains(limbIndex))
                {
                    return false;
                }

                partLoss?.RemoveLimb(limbIndex);
                removedLimbs.Add(limbIndex);
                removedLimbOrder.Add(limbIndex);
                lostPart = FindLimbPart(limbIndex);
                if (remainingPartCount.TryGetValue(lostPart, out int existingCount))
                {
                    remainingPartCount[lostPart] = Mathf.Max(0, existingCount - 1);
                }

                return true;
            }

            var remaining = new List<int>();
            for (int i = 0; i < limbIndices.Count; i++)
            {
                int currentLimbIndex = limbIndices[i];
                if (removedLimbs.Contains(currentLimbIndex))
                {
                    continue;
                }

                BonePart limbPart = FindLimbPart(currentLimbIndex);
                if (targetPart == BonePart.Body || limbPart == targetPart)
                {
                    remaining.Add(currentLimbIndex);
                }
            }

            if (remaining.Count == 0)
            {
                return false;
            }

            int pick = remaining[0];
            BonePart pickedPart = FindLimbPart(pick);

            partLoss?.RemoveLimb(pick);

            removedLimbs.Add(pick);
            removedLimbOrder.Add(pick);
            if (remainingPartCount.TryGetValue(pickedPart, out int c))
            {
                remainingPartCount[pickedPart] = Mathf.Max(0, c - 1);
            }

            lostPart = pickedPart;
            return true;
        }

        /// <summary>
        /// 同期された部位修復を適用する
        /// </summary>
        public bool ApplySyncedRestoreLimb(int limbIndex)
        {
            if (limbIndex < 0 || !removedLimbs.Contains(limbIndex))
            {
                return false;
            }

            partLoss?.CancelGradualRestoreLimb();
            if (partLoss != null && !partLoss.RestoreLimb(limbIndex))
            {
                return false;
            }

            removedLimbs.Remove(limbIndex);
            removedLimbOrder.Remove(limbIndex);
            BonePart part = FindLimbPart(limbIndex);
            remainingPartCount.TryGetValue(part, out int count);
            remainingPartCount[part] = count + 1;
            return true;
        }

        private int CountRemainingLimbsOfPart(BonePart part)
        {
            int count = 0;
            for (int i = 0; i < limbIndices.Count; i++)
            {
                int limbIndex = limbIndices[i];
                if (removedLimbs.Contains(limbIndex))
                {
                    continue;
                }

                if (FindLimbPart(limbIndex) == part)
                {
                    count++;
                }
            }

            return count;
        }

        private BonePart FindLimbPart(int limbIndex)
        {
            if (partLoss != null)
            {
                foreach (var limb in partLoss.Limbs)
                {
                    if (limb.Index == limbIndex)
                    {
                        return limb.Part;
                    }
                }
            }

            return BonePart.Body;
        }

        /// <summary>
        /// 被弾モーションを再生する
        /// </summary>
        /// <param name="heavy">部位破壊を伴う強い被弾か</param>
        public void PlayHitMotion(bool heavy = false)
        {
            if (!HasMotion)
            {
                return;
            }

            float duration = heavy ? 0.45f : 0.3f;
            motion.Play(MotionType.Hit, duration);
        }

        /// <summary>
        /// モーションを再生する
        /// </summary>
        /// <param name="type">再生するモーション</param>
        /// <param name="duration">限定モーションの長さ(秒)0以下なら既定値</param>
        public void PlayMotion(MotionType type, float duration = -1f)
        {
            motion?.Play(type, duration);
        }

        /// <summary>
        /// 攻撃前の溜めモーションを再生する
        /// </summary>
        /// <param name="upcomingAttack">この後に出す攻撃</param>
        /// <param name="duration">溜め時間(秒)</param>
        public void PlayAttackCharge(MotionType upcomingAttack, float duration)
        {
            motion?.PlayCharge(upcomingAttack, duration);
        }

        /// <summary>
        /// ステップ移動のモーションを再生する
        /// </summary>
        /// <param name="stepIntent">-1=前進・+1=後退</param>
        /// <param name="duration">モーション長(秒)</param>
        public void PlayStepMotion(int stepIntent, float duration)
        {
            if (!HasMotion)
            {
                return;
            }

            MotionType type = stepIntent < 0 ? MotionType.StepForward : MotionType.StepBackward;
            motion.Play(type, duration);
        }

        /// <summary>
        /// 手続きモーションが利用可能か
        /// </summary>
        public bool HasMotion => motion != null && motion.IsReady;

        /// <summary>
        /// 移動中・待機中の見えるモーションを更新する
        /// </summary>
        public void UpdateLocomotionMotion()
        {
            if (!HasMotion)
            {
                return;
            }

            if (ProceduralMotionCharacter.IsAttackMotion(motion.CurrentMotion)
                || ProceduralMotionCharacter.IsChargeMotion(motion.CurrentMotion)
                || ProceduralMotionCharacter.IsStepMotion(motion.CurrentMotion)
                || ProceduralMotionCharacter.IsHitMotion(motion.CurrentMotion))
            {
                return;
            }

            if (!CanAct)
            {
                return;
            }

            MotionType target = ResolveLocomotionMotion();
            if (motion.CurrentMotion == target)
            {
                return;
            }

            motion.Play(target);
        }

        private MotionType ResolveLocomotionMotion()
        {
            if (MovementIntent == 0)
            {
                return MotionType.Idle;
            }

            return motion != null && motion.HasLegs ? MotionType.LegRun : MotionType.Run;
        }

        /// <summary>
        /// フィールド配置変更後に攻撃モーションの基準位置を同期する
        /// </summary>
        public void SyncMotionLayoutPosition()
        {
            motion?.OnLayoutPositionChanged();
        }

        /// <summary>
        /// 勝利演出配置前に攻撃状態を捨てIdle基準ポーズへ戻す
        /// </summary>
        public void PreparePresentationIdle()
        {
            motion?.PreparePresentationIdle();
        }

        /// <summary>
        /// 部位欠損メッシュの再構築を止める
        /// </summary>
        public void SuspendPartLossRebuild()
        {
            partLoss?.SuspendMeshRebuild();
        }
    }
}