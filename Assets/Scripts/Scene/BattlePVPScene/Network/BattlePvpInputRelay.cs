using Battle;
using Battle.Input;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

namespace Scene.BattlePVPScene.Network
{
    /// <summary>
    /// 相手プレイヤーの入力とスロット選択をネットワーク同期する
    /// 各プレイヤーは自分のリレーへオーナー書き込みし相手データは相手のリレーから読む
    /// </summary>
    public sealed class BattlePvpInputRelay : NetworkBehaviour
    {
        private readonly NetworkVariable<int> ownerSlotIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<bool> ownerMatchupReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerStepSequence = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerStepDirection = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> ownerStepStartDistance = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> ownerStepTargetDistance = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerAttackSequence = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerAttackMoveIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerStrikeSequence = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<BattlePvpStrikeResult> ownerStrikeResult = new NetworkVariable<BattlePvpStrikeResult>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<BattlePvpInputSnapshot> ownerInput = new NetworkVariable<BattlePvpInputSnapshot>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerPartRestoreSequence = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerPartRestoreLimbIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerCounterSequence = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerCounteredAttackSequence = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerMatchGeneration = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerKnockbackSequence = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> ownerKnockbackDistance = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> ownerDistanceSequence = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> ownerAuthoritativeDistance = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private BattleKeyboardMovementInput localInput;
        private BattlePvpInputRelay opponentRelay;
        private int pendingRemoteAttackMoveIndex = -1;
        private int pendingRemoteAttackSequence;
        private int consumedRemoteAttackStartSequence;
        private int pendingRemoteKnockbackSequence;
        private float pendingRemoteKnockbackDistance;
        private int consumedRemoteKnockbackSequence;

        /// <summary>
        /// 相手の攻撃開始通知を消費する
        /// </summary>
        /// <param name="moveIndex">攻撃技番号</param>
        /// <param name="sequence">同期番号</param>
        /// <returns>通知があればtrue</returns>
        public bool TryConsumeRemoteAttackStart(out int moveIndex, out int sequence)
        {
            moveIndex = -1;
            sequence = 0;
            if (pendingRemoteAttackSequence <= 0
                || pendingRemoteAttackSequence <= consumedRemoteAttackStartSequence)
            {
                return false;
            }

            moveIndex = pendingRemoteAttackMoveIndex;
            sequence = pendingRemoteAttackSequence;
            consumedRemoteAttackStartSequence = sequence;
            pendingRemoteAttackSequence = 0;
            pendingRemoteAttackMoveIndex = -1;
            return moveIndex >= 0;
        }

        /// <summary>
        /// 相手のふっとばし通知を消費する
        /// </summary>
        /// <param name="resultingDistance">同期後の間合い</param>
        /// <returns>通知があればtrue</returns>
        public bool TryConsumeRemoteKnockback(out float resultingDistance)
        {
            resultingDistance = 0f;
            if (pendingRemoteKnockbackSequence <= 0
                || pendingRemoteKnockbackSequence <= consumedRemoteKnockbackSequence)
            {
                return false;
            }

            resultingDistance = pendingRemoteKnockbackDistance;
            consumedRemoteKnockbackSequence = pendingRemoteKnockbackSequence;
            pendingRemoteKnockbackSequence = 0;
            return true;
        }

        /// <summary>
        /// 相手側の最新入力
        /// </summary>
        public BattlePvpInputSnapshot RemoteInput
        {
            get
            {
                BattlePvpInputRelay opponent = ResolveOpponentRelay();
                return opponent != null ? opponent.ownerInput.Value : default;
            }
        }

        /// <summary>
        /// ローカルが選択したスロット
        /// </summary>
        public int LocalSlotIndex => ownerSlotIndex.Value;

        /// <summary>
        /// 相手が選択したスロット
        /// </summary>
        public int OpponentSlotIndex
        {
            get
            {
                BattlePvpInputRelay opponent = ResolveOpponentRelay();
                return opponent != null ? opponent.ownerSlotIndex.Value : -1;
            }
        }

        /// <summary>
        /// 相手の攻撃入力同期番号
        /// </summary>
        public int OpponentAttackSequence
        {
            get
            {
                BattlePvpInputRelay opponent = ResolveOpponentRelay();
                return opponent != null ? opponent.ownerAttackSequence.Value : 0;
            }
        }

        /// <summary>
        /// 相手の最新攻撃技番号
        /// </summary>
        public int OpponentAttackMoveIndex
        {
            get
            {
                BattlePvpInputRelay opponent = ResolveOpponentRelay();
                return opponent != null ? opponent.ownerAttackMoveIndex.Value : -1;
            }
        }

        /// <summary>
        /// 公開中のステップ同期番号
        /// </summary>
        public int StepSequence => ownerStepSequence.Value;

        /// <summary>
        /// 公開中の攻撃結果同期番号
        /// </summary>
        public int StrikeSequence => ownerStrikeSequence.Value;

        /// <summary>
        /// ローカルの攻撃開始同期番号
        /// </summary>
        public int LocalAttackSequence => ownerAttackSequence.Value;

        /// <summary>
        /// ローカル側が間合い同期の権威を持つか
        /// </summary>
        public bool IsDistanceAuthority => IsOwner && IsHost;

        /// <summary>
        /// 現在の対戦世代番号
        /// </summary>
        public int MatchGeneration => ownerMatchGeneration.Value;

        /// <summary>
        /// 公開中のカウンター同期番号
        /// </summary>
        public int CounterSequence => ownerCounterSequence.Value;

        /// <summary>
        /// 最新のカウンターで無効化した攻撃開始同期番号
        /// </summary>
        public int CurrentCounteredAttackSequence => ownerCounteredAttackSequence.Value;

        /// <summary>
        /// 公開中の部位修復同期番号
        /// </summary>
        public int PartRestoreSequence => ownerPartRestoreSequence.Value;

        /// <summary>
        /// 公開中のふっとばし同期番号
        /// </summary>
        public int KnockbackSequence => ownerKnockbackSequence.Value;

        /// <summary>
        /// 現在公開中のふっとばし後間合い
        /// </summary>
        public float CurrentKnockbackDistance => ownerKnockbackDistance.Value;

        /// <summary>
        /// 公開中の権威間合同期番号
        /// </summary>
        public int DistanceSequence => ownerDistanceSequence.Value;

        /// <summary>
        /// 現在公開中の権威間合い
        /// </summary>
        public float CurrentAuthoritativeDistance => ownerAuthoritativeDistance.Value;

        /// <summary>
        /// 現在公開中の部位修復リム番号
        /// </summary>
        public int CurrentPartRestoreLimbIndex => ownerPartRestoreLimbIndex.Value;

        /// <summary>
        /// 現在公開中のステップ同期内容を返す
        /// </summary>
        public BattleRemoteStepPayload CreateStepPayload()
        {
            return new BattleRemoteStepPayload(
                ownerStepDirection.Value,
                ownerStepTargetDistance.Value,
                ownerStepSequence.Value,
                ownerMatchGeneration.Value);
        }

        /// <summary>
        /// 現在公開中の攻撃結果を返す
        /// </summary>
        public BattlePvpStrikeResult CurrentStrikeResult => ownerStrikeResult.Value;

        /// <summary>
        /// ステップ同期イベントが公開された
        /// </summary>
        public event Action<BattleRemoteStepPayload> RemoteStepPublished;

        /// <summary>
        /// 攻撃結果同期イベントが公開された
        /// </summary>
        public event Action<BattlePvpStrikeResult> RemoteStrikePublished;

        /// <summary>
        /// 部位修復完了同期イベントが公開された
        /// </summary>
        public event Action<BattleRemotePartRestorePayload> RemotePartRestorePublished;

        /// <summary>
        /// カウンター同期イベントが公開された
        /// </summary>
        public event Action<int, int> RemoteCounterPublished;

        /// <summary>
        /// 両者のスロット選択が揃ったか
        /// </summary>
        public bool AreBothSlotsReady =>
            ownerSlotIndex.Value >= 0
            && OpponentSlotIndex >= 0;

        /// <summary>
        /// 両者が対戦開始を押したか
        /// </summary>
        public bool AreBothMatchupReady =>
            ownerMatchupReady.Value
            && OpponentMatchupReady;

        private bool OpponentMatchupReady
        {
            get
            {
                BattlePvpInputRelay opponent = ResolveOpponentRelay();
                return opponent != null && opponent.ownerMatchupReady.Value;
            }
        }

        /// <summary>
        /// 対戦前状態をリセットする
        /// </summary>
        public void ResetSessionState()
        {
            if (!IsOwner)
            {
                return;
            }

            ownerMatchupReady.Value = false;
        }

        /// <summary>
        /// 再戦用にスロット選択と同期状態を初期化する
        /// </summary>
        public void ResetForRematch()
        {
            if (!IsOwner)
            {
                return;
            }

            ownerSlotIndex.Value = -1;
            ownerMatchupReady.Value = false;
            ownerStepSequence.Value = 0;
            ownerStepDirection.Value = 0;
            ownerStepStartDistance.Value = 0f;
            ownerStepTargetDistance.Value = 0f;
            ownerAttackSequence.Value = 0;
            ownerAttackMoveIndex.Value = -1;
            ownerStrikeSequence.Value = 0;
            ownerStrikeResult.Value = default;
            ownerInput.Value = default;
            ownerPartRestoreSequence.Value = 0;
            ownerPartRestoreLimbIndex.Value = -1;
            ownerCounterSequence.Value = 0;
            ownerCounteredAttackSequence.Value = 0;
            ownerKnockbackSequence.Value = 0;
            ownerKnockbackDistance.Value = 0f;
            ownerDistanceSequence.Value = 0;
            ownerAuthoritativeDistance.Value = 0f;
            ownerMatchGeneration.Value++;
        }

        /// <summary>
        /// 相手プレイヤーのリレーを返す
        /// </summary>
        public BattlePvpInputRelay GetOpponentRelay()
        {
            return ResolveOpponentRelay();
        }

        /// <summary>
        /// 戦闘入力の送信を開始する
        /// </summary>
        public void BeginBattleInput(BattleKeyboardMovementInput input)
        {
            EndBattleInput();
            localInput = input;
        }

        /// <summary>
        /// 戦闘入力の送信を停止する
        /// </summary>
        public void EndBattleInput()
        {
            localInput = null;
        }

        /// <summary>
        /// ローカル攻撃開始を通知し同期番号を返す
        /// </summary>
        /// <param name="moveIndex">攻撃技番号</param>
        /// <returns>攻撃開始同期番号・未送信時0</returns>
        public int SubmitAttackStart(int moveIndex)
        {
            if (!IsOwner || moveIndex < 0)
            {
                return 0;
            }

            ownerAttackMoveIndex.Value = moveIndex;
            ownerAttackSequence.Value++;
            int sequence = ownerAttackSequence.Value;
            PublishAttackStartRpc(moveIndex, sequence, ownerMatchGeneration.Value);
            return sequence;
        }

        /// <summary>
        /// 選択したスロットを通知する
        /// </summary>
        public void SubmitSlotSelection(int slotIndex)
        {
            if (!IsOwner)
            {
                return;
            }

            ownerSlotIndex.Value = slotIndex;
        }

        /// <summary>
        /// 対戦開始ボタン押下を通知する
        /// </summary>
        public void SubmitMatchupReady()
        {
            if (!IsOwner)
            {
                return;
            }

            ownerMatchupReady.Value = true;
            Debug.Log("[BattlePvpRelay] 対戦開始準備完了");
        }

        /// <summary>
        /// ローカルプレイヤーのステップ移動を通知する
        /// </summary>
        public void SubmitStepMove(int stepIntent, float targetDistance)
        {
            if (!IsOwner || stepIntent == 0)
            {
                return;
            }

            ownerStepDirection.Value = stepIntent;
            ownerStepStartDistance.Value = 0f;
            ownerStepTargetDistance.Value = targetDistance;
            ownerStepSequence.Value++;
            PublishStepRpc(stepIntent, targetDistance, ownerStepSequence.Value, ownerMatchGeneration.Value);
        }

        /// <summary>
        /// ホスト権威の現在間合いを通知する
        /// </summary>
        /// <param name="distance">現在間合い</param>
        public void SubmitAuthoritativeDistance(float distance)
        {
            if (!IsDistanceAuthority)
            {
                return;
            }

            ownerAuthoritativeDistance.Value = distance;
            ownerDistanceSequence.Value++;
        }

        /// <summary>
        /// ローカルプレイヤーの攻撃結果を通知する
        /// </summary>
        public void SubmitStrikeResult(MoveUsedResult result, int moveIndex, int attackSequence)
        {
            if (!IsOwner || result.Attacker == null)
            {
                return;
            }

            if (moveIndex < 0)
            {
                for (int i = 0; i < result.Attacker.Moves.Count; i++)
                {
                    AttackMove candidate = result.Attacker.Moves[i];
                    if (candidate != null && candidate.Motion == result.Move.Motion)
                    {
                        moveIndex = i;
                        break;
                    }
                }
            }

            int sequence = ownerStrikeSequence.Value + 1;
            ownerStrikeResult.Value = new BattlePvpStrikeResult
            {
                Sequence = sequence,
                MoveIndex = moveIndex,
                Hit = result.Hit,
                Damage = result.Damage,
                PartLost = result.PartLost,
                LostPart = (int)result.LostPart,
                LostLimbIndex = result.LostLimbIndex,
                IsKnockout = result.IsKnockout,
                AttackSequence = attackSequence,
                MatchGeneration = ownerMatchGeneration.Value
            };
            ownerStrikeSequence.Value = sequence;
            SubmitStrikeServerRpc(ownerStrikeResult.Value);
        }

        /// <summary>
        /// ローカルふっとばしを通知する
        /// </summary>
        /// <param name="resultingDistance">適用後の間合い</param>
        public void SubmitKnockback(float resultingDistance)
        {
            if (!IsOwner)
            {
                return;
            }

            ownerKnockbackDistance.Value = resultingDistance;
            ownerKnockbackSequence.Value++;
            PublishKnockbackRpc(
                resultingDistance,
                ownerKnockbackSequence.Value,
                ownerMatchGeneration.Value);
        }

        /// <summary>
        /// 相手攻撃をカウンターで無効化したことを通知する
        /// </summary>
        public void SubmitCounter(int counteredAttackSequence)
        {
            if (!IsOwner || counteredAttackSequence <= 0)
            {
                return;
            }

            ownerCounteredAttackSequence.Value = counteredAttackSequence;
            ownerCounterSequence.Value++;
            PublishCounterRpc(
                counteredAttackSequence,
                ownerCounterSequence.Value,
                ownerMatchGeneration.Value);
        }

        [Rpc(SendTo.Server)]
        public void SubmitStrikeServerRpc(BattlePvpStrikeResult strike)
        {
            if (strike.Sequence <= 0)
            {
                return;
            }

            BroadcastStrikeClientRpc(strike);
        }

        [Rpc(SendTo.NotOwner)]
        public void BroadcastStrikeClientRpc(BattlePvpStrikeResult strike)
        {
            if (strike.Sequence <= 0)
            {
                return;
            }

            RemoteStrikePublished?.Invoke(strike);
        }

        /// <summary>
        /// ローカルプレイヤーの部位修復完了を通知する
        /// </summary>
        public void SubmitPartRestore(int limbIndex)
        {
            if (!IsOwner || limbIndex < 0)
            {
                return;
            }

            int sequence = ownerPartRestoreSequence.Value + 1;
            ownerPartRestoreLimbIndex.Value = limbIndex;
            ownerPartRestoreSequence.Value = sequence;
            PublishPartRestoreRpc(limbIndex, sequence, ownerMatchGeneration.Value);
        }

        [Rpc(SendTo.NotOwner)]
        private void PublishKnockbackRpc(float resultingDistance, int sequence, int matchGeneration)
        {
            if (sequence <= 0)
            {
                return;
            }

            if (matchGeneration < ownerMatchGeneration.Value)
            {
                return;
            }

            if (sequence <= consumedRemoteKnockbackSequence)
            {
                return;
            }

            pendingRemoteKnockbackDistance = resultingDistance;
            pendingRemoteKnockbackSequence = sequence;
        }

        [Rpc(SendTo.NotOwner)]
        private void PublishAttackStartRpc(int moveIndex, int sequence, int matchGeneration)
        {
            if (sequence <= 0 || moveIndex < 0)
            {
                return;
            }

            if (matchGeneration < ownerMatchGeneration.Value)
            {
                return;
            }

            if (sequence <= consumedRemoteAttackStartSequence)
            {
                return;
            }

            pendingRemoteAttackMoveIndex = moveIndex;
            pendingRemoteAttackSequence = sequence;
        }

        [Rpc(SendTo.NotOwner)]
        public void PublishStepRpc(int stepIntent, float targetDistance, int sequence, int matchGeneration)
        {
            if (sequence <= 0 || stepIntent == 0)
            {
                return;
            }

            RemoteStepPublished?.Invoke(
                new BattleRemoteStepPayload(stepIntent, targetDistance, sequence, matchGeneration));
        }

        [Rpc(SendTo.NotOwner)]
        public void PublishPartRestoreRpc(int limbIndex, int sequence, int matchGeneration)
        {
            if (sequence <= 0 || limbIndex < 0)
            {
                return;
            }

            RemotePartRestorePublished?.Invoke(
                new BattleRemotePartRestorePayload(limbIndex, sequence, matchGeneration));
        }

        [Rpc(SendTo.NotOwner)]
        private void PublishCounterRpc(int counteredAttackSequence, int sequence, int matchGeneration)
        {
            if (sequence <= 0 || counteredAttackSequence <= 0)
            {
                return;
            }

            RemoteCounterPublished?.Invoke(counteredAttackSequence, matchGeneration);
        }

        /// <summary>
        /// 相手のスロット選択完了を待つ
        /// </summary>
        public async UniTask WaitForBothSlotsAsync(CancellationToken cancellationToken)
        {
            float nextLogTime = 0f;
            await UniTask.WaitUntil(
                () =>
                {
                    if (!AreBothSlotsReady && Time.realtimeSinceStartup >= nextLogTime)
                    {
                        nextLogTime = Time.realtimeSinceStartup + 1f;
                        BattlePvpInputRelay opponent = ResolveOpponentRelay();
                        Debug.Log(
                            "[BattlePvpRelay] スロット待機中"
                            + $" ownerSlot={ownerSlotIndex.Value}"
                            + $" opponentRelay={(opponent != null)}"
                            + $" opponentSlot={(opponent != null ? opponent.ownerSlotIndex.Value : -99)}");
                    }

                    return AreBothSlotsReady;
                },
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 両者の対戦開始準備完了を待つ
        /// </summary>
        public async UniTask WaitForBothMatchupReadyAsync(CancellationToken cancellationToken)
        {
            float nextLogTime = 0f;
            await UniTask.WaitUntil(
                () =>
                {
                    if (!AreBothMatchupReady && Time.realtimeSinceStartup >= nextLogTime)
                    {
                        nextLogTime = Time.realtimeSinceStartup + 1f;
                        Debug.Log(
                            "[BattlePvpRelay] 対戦開始待機中"
                            + $" localReady={ownerMatchupReady.Value}"
                            + $" opponentReady={OpponentMatchupReady}");
                    }

                    return AreBothMatchupReady;
                },
                cancellationToken: cancellationToken);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ownerAttackSequence.OnValueChanged += OnOwnerAttackSequenceChanged;
            ownerStrikeSequence.OnValueChanged += OnOwnerStrikeSequenceChanged;
            ownerPartRestoreSequence.OnValueChanged += OnOwnerPartRestoreSequenceChanged;
            ownerCounterSequence.OnValueChanged += OnOwnerCounterSequenceChanged;
            ownerKnockbackSequence.OnValueChanged += OnOwnerKnockbackSequenceChanged;
        }

        public override void OnNetworkDespawn()
        {
            ownerAttackSequence.OnValueChanged -= OnOwnerAttackSequenceChanged;
            ownerStrikeSequence.OnValueChanged -= OnOwnerStrikeSequenceChanged;
            ownerPartRestoreSequence.OnValueChanged -= OnOwnerPartRestoreSequenceChanged;
            ownerCounterSequence.OnValueChanged -= OnOwnerCounterSequenceChanged;
            ownerKnockbackSequence.OnValueChanged -= OnOwnerKnockbackSequenceChanged;
            base.OnNetworkDespawn();
        }

        private void OnOwnerAttackSequenceChanged(int previousValue, int newValue)
        {
            if (newValue > 0)
            {
                return;
            }

            consumedRemoteAttackStartSequence = 0;
            pendingRemoteAttackSequence = 0;
            pendingRemoteAttackMoveIndex = -1;
        }

        private void OnOwnerKnockbackSequenceChanged(int previousValue, int newValue)
        {
            if (newValue <= 0)
            {
                consumedRemoteKnockbackSequence = 0;
                pendingRemoteKnockbackSequence = 0;
                return;
            }

            if (newValue == previousValue || newValue <= consumedRemoteKnockbackSequence)
            {
                return;
            }

            pendingRemoteKnockbackDistance = ownerKnockbackDistance.Value;
            pendingRemoteKnockbackSequence = newValue;
        }

        private void OnOwnerStrikeSequenceChanged(int previousValue, int newValue)
        {
            if (newValue <= 0 || newValue == previousValue)
            {
                return;
            }

            BattlePvpStrikeResult strike = ownerStrikeResult.Value;
            if (strike.Sequence != newValue)
            {
                return;
            }

            RemoteStrikePublished?.Invoke(strike);
        }

        private void OnOwnerPartRestoreSequenceChanged(int previousValue, int newValue)
        {
            if (newValue <= 0 || newValue == previousValue)
            {
                return;
            }

            int limbIndex = ownerPartRestoreLimbIndex.Value;
            if (limbIndex < 0)
            {
                return;
            }

            RemotePartRestorePublished?.Invoke(new BattleRemotePartRestorePayload(
                limbIndex,
                newValue,
                ownerMatchGeneration.Value));
        }

        private void OnOwnerCounterSequenceChanged(int previousValue, int newValue)
        {
            if (newValue <= 0 || newValue == previousValue)
            {
                return;
            }

            int counteredAttackSequence = ownerCounteredAttackSequence.Value;
            if (counteredAttackSequence <= 0)
            {
                return;
            }

            RemoteCounterPublished?.Invoke(counteredAttackSequence, ownerMatchGeneration.Value);
        }

        private void Update()
        {
            if (!IsSpawned || localInput == null || !IsOwner)
            {
                return;
            }

            BattleKeyboardInputState state = localInput.PeekInputState();
            ownerInput.Value = new BattlePvpInputSnapshot
            {
                MovementIntent = state.MovementIntent,
                PendingStepIntent = 0,
                KnockbackPressed = state.KnockbackPressed,
                PendingAttackIndex = -1,
                IsHoldingPartRepair = state.IsHoldingPartRepair
            };
        }

        // 対戦相手のリレーを探す2体しかいないので自分以外がそのまま相手になる
        private BattlePvpInputRelay ResolveOpponentRelay()
        {
            if (opponentRelay != null && opponentRelay.IsSpawned)
            {
                return opponentRelay;
            }

            BattlePvpInputRelay[] relays = FindObjectsByType<BattlePvpInputRelay>(FindObjectsSortMode.None);
            for (int i = 0; i < relays.Length; i++)
            {
                BattlePvpInputRelay candidate = relays[i];
                if (candidate != this && candidate.IsSpawned)
                {
                    opponentRelay = candidate;
                    break;
                }
            }

            return opponentRelay;
        }
    }
}
