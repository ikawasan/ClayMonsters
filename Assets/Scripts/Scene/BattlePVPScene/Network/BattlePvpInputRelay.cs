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

        private BattleKeyboardMovementInput localInput;
        private BattlePvpInputRelay opponentRelay;
        private int pendingRemoteAttackMoveIndex = -1;
        private int pendingRemoteAttackSequence;
        private int consumedRemoteAttackStartSequence;

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
        /// 公開中の部位修復同期番号
        /// </summary>
        public int PartRestoreSequence => ownerPartRestoreSequence.Value;

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
                ownerStepSequence.Value);
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
            if (localInput != null)
            {
                localInput.AttackPressed += OnLocalAttackPressed;
            }
        }

        /// <summary>
        /// 戦闘入力の送信を停止する
        /// </summary>
        public void EndBattleInput()
        {
            if (localInput != null)
            {
                localInput.AttackPressed -= OnLocalAttackPressed;
            }

            localInput = null;
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
            PublishStepRpc(stepIntent, targetDistance, ownerStepSequence.Value);
        }

        /// <summary>
        /// ローカルプレイヤーの攻撃結果を通知する
        /// </summary>
        public void SubmitStrikeResult(MoveUsedResult result, int moveIndex)
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
                IsKnockout = result.IsKnockout
            };
            ownerStrikeSequence.Value = sequence;
            SubmitStrikeServerRpc(ownerStrikeResult.Value);
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
            PublishPartRestoreRpc(limbIndex, sequence);
        }

        [Rpc(SendTo.NotOwner)]
        private void PublishAttackStartRpc(int moveIndex, int sequence)
        {
            if (sequence <= 0 || moveIndex < 0)
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
        public void PublishStepRpc(int stepIntent, float targetDistance, int sequence)
        {
            if (sequence <= 0 || stepIntent == 0)
            {
                return;
            }

            RemoteStepPublished?.Invoke(new BattleRemoteStepPayload(stepIntent, targetDistance, sequence));
        }

        [Rpc(SendTo.NotOwner)]
        public void PublishPartRestoreRpc(int limbIndex, int sequence)
        {
            if (sequence <= 0 || limbIndex < 0)
            {
                return;
            }

            RemotePartRestorePublished?.Invoke(new BattleRemotePartRestorePayload(limbIndex, sequence));
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
        }

        public override void OnNetworkDespawn()
        {
            ownerAttackSequence.OnValueChanged -= OnOwnerAttackSequenceChanged;
            ownerStrikeSequence.OnValueChanged -= OnOwnerStrikeSequenceChanged;
            ownerPartRestoreSequence.OnValueChanged -= OnOwnerPartRestoreSequenceChanged;
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

            RemotePartRestorePublished?.Invoke(new BattleRemotePartRestorePayload(limbIndex, newValue));
        }

        private void OnLocalAttackPressed(int moveIndex)
        {
            if (!IsOwner || moveIndex < 0)
            {
                return;
            }

            ownerAttackMoveIndex.Value = moveIndex;
            ownerAttackSequence.Value++;
            PublishAttackStartRpc(moveIndex, ownerAttackSequence.Value);
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
