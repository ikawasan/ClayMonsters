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

        private readonly NetworkVariable<bool> ownerStagingReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<bool> ownerSessionAbort = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<bool> ownerRematchReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private const int ModelChunkSize = 16 * 1024;
        private const float SlotSyncTimeoutSeconds = 60f;
        private const float ModelSyncTimeoutSeconds = 120f;
        private const float MatchupReadyTimeoutSeconds = 60f;
        private const float StagingReadyTimeoutSeconds = 120f;
        private const float TransferAckTimeoutSeconds = 30f;
        private const float RematchReadyTimeoutSeconds = 120f;

        private int receiveTransferId = -1;
        private int receiveTotalBytes;
        private int receiveTotalChunks;
        private bool[] receiveChunkFlags;
        private byte[] receiveBuffer;
        private string receiveMetaJson = string.Empty;
        private BattlePvpReceivedRemoteModel receivedRemoteModel;
        private bool isPublishingModel;
        private bool receiveTransferFailed;
        private string receiveTransferFailReason = string.Empty;
        private int publishedTransferId = -1;
        private bool transferAcked;
        private bool transferRejected;
        private string transferRejectReason = string.Empty;

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

        /// <summary>
        /// 両者が見せ合い演出へ進む準備ができたか
        /// </summary>
        public bool AreBothStagingReady =>
            ownerStagingReady.Value
            && OpponentStagingReady;

        /// <summary>
        /// 自分または相手が同期失敗を宣言しているか
        /// </summary>
        public bool IsSessionAborted =>
            ownerSessionAbort.Value
            || OpponentSessionAbort;

        /// <summary>
        /// 両者が再戦に合意したか
        /// </summary>
        public bool AreBothRematchReady =>
            ownerRematchReady.Value
            && OpponentRematchReady;

        private bool OpponentMatchupReady
        {
            get
            {
                BattlePvpInputRelay opponent = ResolveOpponentRelay();
                return opponent != null && opponent.ownerMatchupReady.Value;
            }
        }

        private bool OpponentStagingReady
        {
            get
            {
                BattlePvpInputRelay opponent = ResolveOpponentRelay();
                return opponent != null && opponent.ownerStagingReady.Value;
            }
        }

        private bool OpponentSessionAbort
        {
            get
            {
                BattlePvpInputRelay opponent = ResolveOpponentRelay();
                return opponent != null && opponent.ownerSessionAbort.Value;
            }
        }

        private bool OpponentRematchReady
        {
            get
            {
                BattlePvpInputRelay opponent = ResolveOpponentRelay();
                return opponent != null && opponent.ownerRematchReady.Value;
            }
        }

        /// <summary>
        /// 対戦前状態をリセットする
        /// 失敗通知はReportSessionFailureを使う
        /// </summary>
        public void ResetSessionState()
        {
            if (!IsOwner)
            {
                return;
            }

            ownerSlotIndex.Value = -1;
            ownerMatchupReady.Value = false;
            ownerModelReady.Value = false;
            ownerStagingReady.Value = false;
            ownerSessionAbort.Value = false;
            ownerRematchReady.Value = false;
            publishedTransferId = -1;
            transferAcked = false;
            transferRejected = false;
            transferRejectReason = string.Empty;
        }

        /// <summary>
        /// 同期失敗を相手へ即時通知し対戦前状態を落とす
        /// </summary>
        public void ReportSessionFailure()
        {
            if (!IsOwner)
            {
                return;
            }

            ownerSessionAbort.Value = true;
            ownerSlotIndex.Value = -1;
            ownerMatchupReady.Value = false;
            ownerModelReady.Value = false;
            ownerStagingReady.Value = false;
            ownerRematchReady.Value = false;
            publishedTransferId = -1;
            transferAcked = false;
            transferRejected = false;
            transferRejectReason = string.Empty;
            ClearAllReceivedRemoteModels();
            Debug.LogWarning("[BattlePvpRelay] 同期失敗を通知しました");
        }

        /// <summary>
        /// 再戦用にスロット選択と同期状態を初期化する
        /// 再戦合意後に呼ぶ
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
            ownerStagingReady.Value = false;
            ownerSessionAbort.Value = false;
            ownerRematchReady.Value = false;
            publishedTransferId = -1;
            transferAcked = false;
            transferRejected = false;
            transferRejectReason = string.Empty;
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
                throw new InvalidOperationException("[BattlePvpRelay] スロット送信スキップ IsOwner=false");
            }

            if (slotIndex < 0)
            {
                throw new InvalidOperationException(
                    $"[BattlePvpRelay] 無効なスロット送信 slotIndex={slotIndex}");
            }

            ownerSessionAbort.Value = false;
            ownerMatchupReady.Value = false;
            ownerModelReady.Value = false;
            ownerStagingReady.Value = false;
            ownerSlotIndex.Value = slotIndex;
            Debug.Log($"[BattlePvpRelay] スロット送信完了 slotIndex={slotIndex}");
        }

        /// <summary>
        /// ローカル選択モデルを相手へ送信する
        /// wireBytesはGZip推奨
        /// </summary>
        /// <param name="metaJson">メタJSON</param>
        /// <param name="wireBytes">送信用バイナリ</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask PublishLocalModelAsync(
            string metaJson,
            byte[] wireBytes,
            CancellationToken cancellationToken)
        {
            if (!IsOwner)
            {
                throw new InvalidOperationException("[BattlePvpRelay] モデル送信スキップ IsOwner=false");
            }

            if (wireBytes == null || wireBytes.Length == 0)
            {
                ownerModelReady.Value = false;
                throw new InvalidOperationException(
                    "[BattlePvpRelay] 送信するモデルペイロードが空です");
            }

            if (wireBytes.Length > BattlePvpModelTransfer.MaxTransferByteCount)
            {
                ownerModelReady.Value = false;
                throw new InvalidOperationException(
                    BattlePvpModelTransfer.BuildTransferTooLargeMessage(wireBytes.Length));
            }

            if (isPublishingModel)
            {
                throw new InvalidOperationException("[BattlePvpRelay] モデル送信中のため再送できません");
            }

            isPublishingModel = true;
            ownerModelReady.Value = false;
            publishedTransferId = -1;
            transferAcked = false;
            transferRejected = false;
            transferRejectReason = string.Empty;
            byte[] chunkBuffer = null;
            try
            {
                ThrowIfSyncUnavailable();
                int transferId = ownerMatchGeneration.Value;
                publishedTransferId = transferId;
                int totalChunks = Mathf.Max(1, (wireBytes.Length + ModelChunkSize - 1) / ModelChunkSize);
                BeginRemoteModelRpc(transferId, wireBytes.Length, totalChunks, metaJson ?? string.Empty);
                await UniTask.Yield(cancellationToken);

                for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ThrowIfSyncUnavailable();
                    int offset = chunkIndex * ModelChunkSize;
                    int length = Mathf.Min(ModelChunkSize, wireBytes.Length - offset);
                    if (chunkBuffer == null || chunkBuffer.Length != length)
                    {
                        chunkBuffer = new byte[length];
                    }

                    Buffer.BlockCopy(wireBytes, offset, chunkBuffer, 0, length);
                    // RPC側でコピーされるため同一バッファの再利用でよい
                    ReceiveModelChunkRpc(transferId, chunkIndex, chunkBuffer);
                    await UniTask.Yield(cancellationToken);
                }

                await WaitUntilOrFailAsync(
                    () => transferAcked || transferRejected,
                    TransferAckTimeoutSeconds,
                    Localization.GameTextKeys.BattlePvpTransferAckTimeout,
                    "相手のモデル受信確認がタイムアウトしました ({seconds}秒)",
                    cancellationToken);

                if (transferRejected)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrEmpty(transferRejectReason)
                            ? "[BattlePvpRelay] 相手がモデル転送を拒否しました"
                            : transferRejectReason);
                }

                ownerModelReady.Value = true;
                Debug.Log(
                    "[BattlePvpRelay] モデル送信完了"
                    + $" bytes={wireBytes.Length}"
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
        /// ワイヤ完了後に展開まで行う
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask WaitForOpponentModelAsync(CancellationToken cancellationToken)
        {
            float startedAt = Time.realtimeSinceStartup;
            float nextLogTime = 0f;
            await WaitUntilOrFailAsync(
                () =>
                {
                    BattlePvpReceivedRemoteModel model = GetOpponentReceivedModel();
                    bool wireReady = model != null && model.IsWireReady;
                    BattlePvpInputRelay opponent = ResolveOpponentRelay();
                    bool receiveFailed = opponent != null && opponent.receiveTransferFailed;
                    if (!wireReady
                        && !receiveFailed
                        && Time.realtimeSinceStartup >= nextLogTime)
                    {
                        nextLogTime = Time.realtimeSinceStartup + 1f;
                        Debug.Log(
                            "[BattlePvpRelay] 相手モデル待機中"
                            + $" opponent={(opponent != null)}"
                            + $" published={(opponent != null && opponent.ownerModelReady.Value)}"
                            + $" wireReady={wireReady}");
                    }

                    if (receiveFailed)
                    {
                        return true;
                    }

                    return wireReady;
                },
                ModelSyncTimeoutSeconds,
                Localization.GameTextKeys.BattlePvpModelTimeout,
                "相手モデルの受信がタイムアウトしました ({seconds}秒)",
                cancellationToken);

            BattlePvpInputRelay opponentRelay = ResolveOpponentRelay();
            if (opponentRelay != null && opponentRelay.receiveTransferFailed)
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(opponentRelay.receiveTransferFailReason)
                        ? "[BattlePvpRelay] 相手モデル転送が拒否されました"
                        : opponentRelay.receiveTransferFailReason);
            }

            BattlePvpReceivedRemoteModel received = GetOpponentReceivedModel();
            if (received == null || !received.IsWireReady)
            {
                throw new TimeoutException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpModelTimeout,
                        "相手モデルの受信がタイムアウトしました ({seconds}秒)",
                        "seconds",
                        ModelSyncTimeoutSeconds));
            }

            await received.DecodeAsync(cancellationToken);
            if (!received.IsValid)
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(received.DecodeError)
                        ? "[BattlePvpRelay] 相手モデルの展開に失敗しました"
                        : received.DecodeError);
            }

            Debug.Log(
                "[BattlePvpRelay] 相手モデル展開完了"
                + $" elapsed={Time.realtimeSinceStartup - startedAt:0.0}s");
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
            receiveTransferFailed = false;
            receiveTransferFailReason = string.Empty;
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
            if (totalBytes <= 0
                || totalBytes > BattlePvpModelTransfer.MaxTransferByteCount
                || totalChunks <= 0)
            {
                string reason =
                    "[BattlePvpRelay] 不正なモデル転送ヘッダ"
                    + $" transferId={transferId}"
                    + $" totalBytes={totalBytes}"
                    + $" totalChunks={totalChunks}"
                    + $" max={BattlePvpModelTransfer.MaxTransferByteCount}";
                Debug.LogError(reason);
                MarkReceiveTransferFailed(reason);
                return;
            }

            receiveTransferFailed = false;
            receiveTransferFailReason = string.Empty;
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
            if (receiveTransferFailed)
            {
                return;
            }

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
                MarkReceiveTransferFailed(
                    $"[BattlePvpRelay] チャンク範囲外 transferId={transferId} chunkIndex={chunkIndex}");
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
            // receiveBufferをそのまま保持し展開後に参照解除する
            receivedRemoteModel = new BattlePvpReceivedRemoteModel(meta, receiveBuffer);
            Debug.Log(
                "[BattlePvpRelay] モデル受信完了"
                + $" transferId={transferId}"
                + $" bytes={receiveTotalBytes}"
                + $" name={meta.modelName}"
                + $" gzip={meta.isGzipCompressed}");
            ConfirmModelTransferRpc(transferId);
        }

        /// <summary>
        /// 受信側がワイヤ完了を送信側へ返す
        /// </summary>
        /// <param name="transferId">転送ID</param>
        [Rpc(SendTo.Owner)]
        private void ConfirmModelTransferRpc(int transferId)
        {
            if (transferId == publishedTransferId)
            {
                transferAcked = true;
                Debug.Log($"[BattlePvpRelay] モデル転送ACK受信 transferId={transferId}");
            }
        }

        private void MarkReceiveTransferFailed(string reason)
        {
            receiveTransferFailed = true;
            receiveTransferFailReason = reason ?? string.Empty;
            receiveBuffer = null;
            receiveChunkFlags = null;
            receivedRemoteModel = null;
            if (receiveTransferId >= 0)
            {
                RejectModelTransferRpc(receiveTransferId, receiveTransferFailReason);
            }
        }

        /// <summary>
        /// 受信側が転送拒否を送信側へ返す
        /// </summary>
        /// <param name="transferId">転送ID</param>
        /// <param name="reason">拒否理由</param>
        [Rpc(SendTo.Owner)]
        private void RejectModelTransferRpc(int transferId, string reason)
        {
            if (transferId != publishedTransferId)
            {
                return;
            }

            transferRejected = true;
            transferRejectReason = reason ?? string.Empty;
            Debug.LogError(
                $"[BattlePvpRelay] モデル転送NACK受信 transferId={transferId} reason={transferRejectReason}");
        }

        /// <summary>
        /// 対戦開始ボタン押下を通知する
        /// </summary>
        public void SubmitMatchupReady()
        {
            if (!IsOwner)
            {
                throw new InvalidOperationException("[BattlePvpRelay] 対戦開始準備 IsOwner=false");
            }

            ownerMatchupReady.Value = true;
            Debug.Log("[BattlePvpRelay] 対戦開始準備完了");
        }

        /// <summary>
        /// 見せ合い演出へ進む準備完了を通知する
        /// </summary>
        public void SubmitStagingReady()
        {
            if (!IsOwner)
            {
                throw new InvalidOperationException("[BattlePvpRelay] 見せ合い準備 IsOwner=false");
            }

            ownerStagingReady.Value = true;
            Debug.Log("[BattlePvpRelay] 見せ合い準備完了");
        }

        /// <summary>
        /// 再戦希望を通知する
        /// ResetForRematch前に両者で呼ぶ
        /// </summary>
        public void SubmitRematchReady()
        {
            if (!IsOwner)
            {
                throw new InvalidOperationException("[BattlePvpRelay] 再戦準備 IsOwner=false");
            }

            ownerRematchReady.Value = true;
            Debug.Log("[BattlePvpRelay] 再戦準備完了");
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
            float nextLogTime = 0f;
            await WaitUntilOrFailAsync(
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
                SlotSyncTimeoutSeconds,
                Localization.GameTextKeys.BattlePvpSlotTimeout,
                "相手のスロット選択待ちがタイムアウトしました ({seconds}秒)",
                cancellationToken);
        }

        /// <summary>
        /// 両者の対戦開始準備完了を待つ
        /// </summary>
        public async UniTask WaitForBothMatchupReadyAsync(CancellationToken cancellationToken)
        {
            float nextLogTime = 0f;
            await WaitUntilOrFailAsync(
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
                MatchupReadyTimeoutSeconds,
                Localization.GameTextKeys.BattlePvpMatchupTimeout,
                "対戦開始準備待ちがタイムアウトしました ({seconds}秒)",
                cancellationToken);
        }

        /// <summary>
        /// 両者の見せ合い演出準備完了を待つ
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask WaitForBothStagingReadyAsync(CancellationToken cancellationToken)
        {
            float nextLogTime = 0f;
            await WaitUntilOrFailAsync(
                () =>
                {
                    if (!AreBothStagingReady && Time.realtimeSinceStartup >= nextLogTime)
                    {
                        nextLogTime = Time.realtimeSinceStartup + 1f;
                        Debug.Log(
                            "[BattlePvpRelay] 見せ合い準備待機中"
                            + $" localReady={ownerStagingReady.Value}"
                            + $" opponentReady={OpponentStagingReady}");
                    }

                    return AreBothStagingReady;
                },
                StagingReadyTimeoutSeconds,
                Localization.GameTextKeys.BattlePvpStagingTimeout,
                "相手の見せ合い準備待ちがタイムアウトしました ({seconds}秒)",
                cancellationToken);
        }

        /// <summary>
        /// 両者の再戦合意を待つ
        /// 一度揃ったことをラッチしReset後に待ちが外れないようにする
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask WaitForBothRematchReadyAsync(CancellationToken cancellationToken)
        {
            bool bothSeen = false;
            float nextLogTime = 0f;
            float startedAt = Time.realtimeSinceStartup;
            await UniTask.WaitUntil(
                () =>
                {
                    if (AreBothRematchReady)
                    {
                        bothSeen = true;
                    }

                    if (!bothSeen && Time.realtimeSinceStartup >= nextLogTime)
                    {
                        nextLogTime = Time.realtimeSinceStartup + 1f;
                        Debug.Log(
                            "[BattlePvpRelay] 再戦合意待機中"
                            + $" localReady={ownerRematchReady.Value}"
                            + $" opponentReady={OpponentRematchReady}");
                    }

                    if (bothSeen)
                    {
                        return true;
                    }

                    if (IsSessionAborted || !IsNetworkSessionAlive())
                    {
                        return true;
                    }

                    return Time.realtimeSinceStartup - startedAt >= RematchReadyTimeoutSeconds;
                },
                cancellationToken: cancellationToken);

            if (bothSeen)
            {
                return;
            }

            if (IsSessionAborted)
            {
                throw new InvalidOperationException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpSessionAborted,
                        "相手が対戦準備を中断しました"));
            }

            if (!IsNetworkSessionAlive())
            {
                throw new InvalidOperationException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpNetworkLost,
                        "通信が切断されたため対戦を中断しました"));
            }

            throw new TimeoutException(
                Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.BattlePvpRematchTimeout,
                    "相手の再戦準備待ちがタイムアウトしました ({seconds}秒)",
                    "seconds",
                    RematchReadyTimeoutSeconds));
        }

        private async UniTask WaitUntilOrFailAsync(
            Func<bool> isReady,
            float timeoutSeconds,
            string timeoutKey,
            string timeoutFallback,
            CancellationToken cancellationToken)
        {
            float startedAt = Time.realtimeSinceStartup;
            await UniTask.WaitUntil(
                () =>
                {
                    if (isReady())
                    {
                        return true;
                    }

                    if (IsSessionAborted || !IsNetworkSessionAlive())
                    {
                        return true;
                    }

                    return Time.realtimeSinceStartup - startedAt >= timeoutSeconds;
                },
                cancellationToken: cancellationToken);

            if (isReady())
            {
                return;
            }

            if (IsSessionAborted)
            {
                throw new InvalidOperationException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpSessionAborted,
                        "相手が対戦準備を中断しました"));
            }

            if (!IsNetworkSessionAlive())
            {
                throw new InvalidOperationException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpNetworkLost,
                        "通信が切断されたため対戦を中断しました"));
            }

            throw new TimeoutException(
                Localization.LocalizedText.GetOrFallback(
                    timeoutKey,
                    timeoutFallback,
                    "seconds",
                    timeoutSeconds));
        }

        private void ThrowIfSyncUnavailable()
        {
            if (IsSessionAborted)
            {
                throw new InvalidOperationException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpSessionAborted,
                        "相手が対戦準備を中断しました"));
            }

            if (!IsNetworkSessionAlive())
            {
                throw new InvalidOperationException(
                    Localization.LocalizedText.GetOrFallback(
                        Localization.GameTextKeys.BattlePvpNetworkLost,
                        "通信が切断されたため対戦を中断しました"));
            }
        }

        private static bool IsNetworkSessionAlive()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.ShutdownInProgress)
            {
                return false;
            }

            if (manager.IsServer || manager.IsHost)
            {
                // ホストは相手未接続を即失敗とみなす
                return manager.IsListening
                    && manager.ConnectedClientsIds != null
                    && manager.ConnectedClientsIds.Count >= 2;
            }

            // クライアントはサーバ接続が生きているか
            return manager.IsClient && manager.IsConnectedClient;
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
