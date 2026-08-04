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

        private readonly NetworkVariable<bool> ownerModelReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private const int ModelChunkSize = 16 * 1024;
        private const int MaxModelBytes = 8 * 1024 * 1024;
        private const float SlotSyncTimeoutSeconds = 60f;
        private const float ModelSyncTimeoutSeconds = 120f;
        private const float MatchupReadyTimeoutSeconds = 60f;

        private int receiveTransferId = -1;
        private int receiveTotalBytes;
        private int receiveTotalChunks;
        private bool[] receiveChunkFlags;
        private byte[] receiveBuffer;
        private string receiveMetaJson = string.Empty;
        private BattlePvpReceivedRemoteModel receivedRemoteModel;
        private bool isPublishingModel;

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

        private readonly NetworkVariable<bool> ownerAttackIsCounter = new NetworkVariable<bool>(
            false,
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
        private bool pendingRemoteAttackIsCounter;
        private int consumedRemoteAttackStartSequence;
        private int pendingRemoteKnockbackSequence;
        private float pendingRemoteKnockbackDistance;
        private int consumedRemoteKnockbackSequence;
        private bool hasPublishedLocalInput;
        private BattlePvpInputSnapshot lastPublishedLocalInput;

        /// <summary>
        /// 相手の攻撃開始通知を消費する
        /// </summary>
        /// <param name="moveIndex">攻撃技番号</param>
        /// <param name="sequence">同期番号</param>
        /// <param name="isCounter">カウンター攻撃か</param>
        /// <returns>通知があればtrue</returns>
        public bool TryConsumeRemoteAttackStart(out int moveIndex, out int sequence, out bool isCounter)
        {
            moveIndex = -1;
            sequence = 0;
            isCounter = false;
            if (pendingRemoteAttackSequence <= 0
                || pendingRemoteAttackSequence <= consumedRemoteAttackStartSequence)
            {
                return false;
            }

            moveIndex = pendingRemoteAttackMoveIndex;
            sequence = pendingRemoteAttackSequence;
            isCounter = pendingRemoteAttackIsCounter;
            consumedRemoteAttackStartSequence = sequence;
            pendingRemoteAttackSequence = 0;
            pendingRemoteAttackMoveIndex = -1;
            pendingRemoteAttackIsCounter = false;
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
        /// 相手の最新攻撃がカウンターか
        /// </summary>
        public bool OpponentAttackIsCounter
        {
            get
            {
                BattlePvpInputRelay opponent = ResolveOpponentRelay();
                return opponent != null && opponent.ownerAttackIsCounter.Value;
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
            ownerModelReady.Value = false;
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
            ownerModelReady.Value = false;
            ownerStepSequence.Value = 0;
            ownerStepDirection.Value = 0;
            ownerStepStartDistance.Value = 0f;
            ownerStepTargetDistance.Value = 0f;
            ownerAttackSequence.Value = 0;
            ownerAttackMoveIndex.Value = -1;
            ownerAttackIsCounter.Value = false;
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
            ClearReceivedRemoteModel();
            ResolveOpponentRelay()?.ClearReceivedRemoteModel();
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
        /// <param name="isCounter">カウンター攻撃か</param>
        /// <returns>攻撃開始同期番号・未送信時0</returns>
        public int SubmitAttackStart(int moveIndex, bool isCounter = false)
        {
            if (!IsOwner || moveIndex < 0)
            {
                return 0;
            }

            ownerAttackMoveIndex.Value = moveIndex;
            ownerAttackIsCounter.Value = isCounter;
            ownerAttackSequence.Value++;
            int sequence = ownerAttackSequence.Value;
            PublishAttackStartRpc(moveIndex, sequence, isCounter, ownerMatchGeneration.Value);
            return sequence;
        }

        /// <summary>
        /// 選択したスロットを通知する
        /// </summary>
        public void SubmitSlotSelection(int slotIndex)
        {
            if (!IsOwner)
            {
                Debug.LogWarning("[BattlePvpRelay] スロット送信スキップ IsOwner=false");
                return;
            }

            if (slotIndex < 0)
            {
                Debug.LogWarning($"[BattlePvpRelay] 無効なスロット送信を無視しました slotIndex={slotIndex}");
                return;
            }

            ownerSlotIndex.Value = slotIndex;
            Debug.Log($"[BattlePvpRelay] スロット送信完了 slotIndex={slotIndex}");
        }

        /// <summary>
        /// ローカル選択モデルを相手へ送信する
        /// </summary>
        /// <param name="metaJson">メタJSON</param>
        /// <param name="glbBytes">glbバイナリ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask PublishLocalModelAsync(
            string metaJson,
            byte[] glbBytes,
            CancellationToken cancellationToken)
        {
            if (!IsOwner)
            {
                Debug.LogWarning("[BattlePvpRelay] モデル送信スキップ IsOwner=false");
                return;
            }

            if (glbBytes == null || glbBytes.Length == 0)
            {
                Debug.LogError("[BattlePvpRelay] 送信するglbが空です");
                ownerModelReady.Value = false;
                return;
            }

            if (glbBytes.Length > MaxModelBytes)
            {
                Debug.LogError(
                    $"[BattlePvpRelay] glbが大きすぎます size={glbBytes.Length} max={MaxModelBytes}");
                ownerModelReady.Value = false;
                return;
            }

            if (isPublishingModel)
            {
                Debug.LogWarning("[BattlePvpRelay] モデル送信中のため再送を無視します");
                return;
            }

            isPublishingModel = true;
            ownerModelReady.Value = false;
            byte[] chunkBuffer = null;
            try
            {
                int transferId = ownerMatchGeneration.Value;
                int totalChunks = Mathf.Max(1, (glbBytes.Length + ModelChunkSize - 1) / ModelChunkSize);
                BeginRemoteModelRpc(transferId, glbBytes.Length, totalChunks, metaJson ?? string.Empty);
                await UniTask.Yield(cancellationToken);

                for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int offset = chunkIndex * ModelChunkSize;
                    int length = Mathf.Min(ModelChunkSize, glbBytes.Length - offset);
                    if (chunkBuffer == null || chunkBuffer.Length != length)
                    {
                        chunkBuffer = new byte[length];
                    }

                    Buffer.BlockCopy(glbBytes, offset, chunkBuffer, 0, length);
                    // RPC側でコピーされるため同一バッファの再利用でよい
                    ReceiveModelChunkRpc(transferId, chunkIndex, chunkBuffer);
                    await UniTask.Yield(cancellationToken);
                }

                ownerModelReady.Value = true;
                Debug.Log(
                    "[BattlePvpRelay] モデル送信完了"
                    + $" bytes={glbBytes.Length}"
                    + $" chunks={totalChunks}"
                    + $" transferId={transferId}");
            }
            finally
            {
                isPublishingModel = false;
                chunkBuffer = null;
            }
        }

        /// <summary>
        /// 相手モデルの受信完了を待つ
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask WaitForOpponentModelAsync(CancellationToken cancellationToken)
        {
            float startedAt = Time.realtimeSinceStartup;
            float nextLogTime = 0f;
            await UniTask.WaitUntil(
                () =>
                {
                    BattlePvpReceivedRemoteModel model = GetOpponentReceivedModel();
                    bool ready = model != null && model.IsValid;
                    if (!ready && Time.realtimeSinceStartup >= nextLogTime)
                    {
                        nextLogTime = Time.realtimeSinceStartup + 1f;
                        BattlePvpInputRelay opponent = ResolveOpponentRelay();
                        Debug.Log(
                            "[BattlePvpRelay] 相手モデル待機中"
                            + $" opponent={(opponent != null)}"
                            + $" published={(opponent != null && opponent.ownerModelReady.Value)}"
                            + $" received={(model != null && model.IsValid)}");
                    }

                    return ready || Time.realtimeSinceStartup - startedAt >= ModelSyncTimeoutSeconds;
                },
                cancellationToken: cancellationToken);

            BattlePvpReceivedRemoteModel received = GetOpponentReceivedModel();
            if (received == null || !received.IsValid)
            {
                throw new TimeoutException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpModelTimeout,
                        "相手モデルの受信がタイムアウトしました ({seconds}秒)",
                        "seconds",
                        ModelSyncTimeoutSeconds));
            }
        }

        /// <summary>
        /// 受信済みの相手モデルを返す
        /// </summary>
        /// <returns>相手モデル</returns>
        public BattlePvpReceivedRemoteModel GetOpponentReceivedModel()
        {
            BattlePvpInputRelay opponent = ResolveOpponentRelay();
            return opponent != null ? opponent.receivedRemoteModel : null;
        }

        /// <summary>
        /// 受信バッファを破棄する
        /// </summary>
        public void ClearReceivedRemoteModel()
        {
            receiveTransferId = -1;
            receiveTotalBytes = 0;
            receiveTotalChunks = 0;
            receiveChunkFlags = null;
            receiveBuffer = null;
            receiveMetaJson = string.Empty;
            receivedRemoteModel = null;
        }

        /// <summary>
        /// 自分と相手のモデル受信バッファを破棄する
        /// </summary>
        public void ClearAllReceivedRemoteModels()
        {
            ClearReceivedRemoteModel();
            ResolveOpponentRelay()?.ClearReceivedRemoteModel();
        }

        [Rpc(SendTo.NotOwner)]
        private void BeginRemoteModelRpc(int transferId, int totalBytes, int totalChunks, string metaJson)
        {
            if (totalBytes <= 0 || totalBytes > MaxModelBytes || totalChunks <= 0)
            {
                Debug.LogError(
                    "[BattlePvpRelay] 不正なモデル転送ヘッダ"
                    + $" transferId={transferId}"
                    + $" totalBytes={totalBytes}"
                    + $" totalChunks={totalChunks}");
                ClearReceivedRemoteModel();
                return;
            }

            receiveTransferId = transferId;
            receiveTotalBytes = totalBytes;
            receiveTotalChunks = totalChunks;
            receiveChunkFlags = new bool[totalChunks];
            receiveBuffer = new byte[totalBytes];
            receiveMetaJson = metaJson ?? string.Empty;
            receivedRemoteModel = null;
            Debug.Log(
                "[BattlePvpRelay] モデル受信開始"
                + $" transferId={transferId}"
                + $" totalBytes={totalBytes}"
                + $" totalChunks={totalChunks}");
        }

        [Rpc(SendTo.NotOwner)]
        private void ReceiveModelChunkRpc(int transferId, int chunkIndex, byte[] chunk)
        {
            if (receiveBuffer == null
                || receiveChunkFlags == null
                || receiveTransferId != transferId
                || chunk == null
                || chunk.Length == 0
                || chunkIndex < 0
                || chunkIndex >= receiveTotalChunks)
            {
                return;
            }

            if (receiveChunkFlags[chunkIndex])
            {
                return;
            }

            int offset = chunkIndex * ModelChunkSize;
            if (offset >= receiveTotalBytes)
            {
                return;
            }

            int length = Mathf.Min(chunk.Length, receiveTotalBytes - offset);
            Buffer.BlockCopy(chunk, 0, receiveBuffer, offset, length);
            receiveChunkFlags[chunkIndex] = true;

            for (int i = 0; i < receiveChunkFlags.Length; i++)
            {
                if (!receiveChunkFlags[i])
                {
                    return;
                }
            }

            BattlePvpRemoteModelMeta meta = BattlePvpRemoteModelMeta.FromJson(receiveMetaJson);
            receivedRemoteModel = new BattlePvpReceivedRemoteModel(meta, receiveBuffer);
            Debug.Log(
                "[BattlePvpRelay] モデル受信完了"
                + $" transferId={transferId}"
                + $" bytes={receiveTotalBytes}"
                + $" name={meta.modelName}");
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
        private void PublishAttackStartRpc(int moveIndex, int sequence, bool isCounter, int matchGeneration)
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
            pendingRemoteAttackIsCounter = isCounter;
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
            float startedAt = Time.realtimeSinceStartup;
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

                    return AreBothSlotsReady
                        || Time.realtimeSinceStartup - startedAt >= SlotSyncTimeoutSeconds;
                },
                cancellationToken: cancellationToken);

            if (!AreBothSlotsReady)
            {
                throw new TimeoutException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpSlotTimeout,
                        "相手のスロット選択待ちがタイムアウトしました ({seconds}秒)",
                        "seconds",
                        SlotSyncTimeoutSeconds));
            }
        }

        /// <summary>
        /// 両者の対戦開始準備完了を待つ
        /// </summary>
        public async UniTask WaitForBothMatchupReadyAsync(CancellationToken cancellationToken)
        {
            float startedAt = Time.realtimeSinceStartup;
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

                    return AreBothMatchupReady
                        || Time.realtimeSinceStartup - startedAt >= MatchupReadyTimeoutSeconds;
                },
                cancellationToken: cancellationToken);

            if (!AreBothMatchupReady)
            {
                throw new TimeoutException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpMatchupTimeout,
                        "対戦開始準備待ちがタイムアウトしました ({seconds}秒)",
                        "seconds",
                        MatchupReadyTimeoutSeconds));
            }
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
            pendingRemoteAttackIsCounter = false;
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
            var snapshot = new BattlePvpInputSnapshot
            {
                MovementIntent = state.MovementIntent,
                PendingStepIntent = 0,
                KnockbackPressed = state.KnockbackPressed,
                PendingAttackIndex = -1,
                IsHoldingPartRepair = state.IsHoldingPartRepair
            };

            if (hasPublishedLocalInput && InputSnapshotsEqual(lastPublishedLocalInput, snapshot))
            {
                return;
            }

            lastPublishedLocalInput = snapshot;
            hasPublishedLocalInput = true;
            ownerInput.Value = snapshot;
        }

        private static bool InputSnapshotsEqual(BattlePvpInputSnapshot a, BattlePvpInputSnapshot b)
        {
            return a.MovementIntent == b.MovementIntent
                && a.PendingStepIntent == b.PendingStepIntent
                && a.KnockbackPressed == b.KnockbackPressed
                && a.PendingAttackIndex == b.PendingAttackIndex
                && a.IsHoldingPartRepair == b.IsHoldingPartRepair;
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
