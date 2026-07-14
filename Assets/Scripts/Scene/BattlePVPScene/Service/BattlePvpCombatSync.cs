using Battle;
using Battle.Interface;
using ClayEditor.Rigging;
using Scene.BattlePVPScene.Network;
using System.Collections.Generic;
using UnityEngine;

namespace Scene.BattlePVPScene.Service
{
    /// <summary>
    /// 入力リレー経由でPvP戦闘結果を同期する
    /// </summary>
    public sealed class BattlePvpCombatSync : IBattlePvpCombatSync
    {
        private readonly BattlePvpInputRelay inputRelay;
        private readonly Queue<BattleRemoteStepPayload> stepQueue = new Queue<BattleRemoteStepPayload>();
        private readonly Queue<BattleRemoteStrikePayload> strikeQueue = new Queue<BattleRemoteStrikePayload>();
        private readonly Queue<BattleRemotePartRestorePayload> partRestoreQueue = new Queue<BattleRemotePartRestorePayload>();
        private readonly HashSet<int> consumedStepSequences = new HashSet<int>();
        private readonly HashSet<int> queuedStepSequences = new HashSet<int>();
        private readonly HashSet<int> queuedStrikeSequences = new HashSet<int>();
        private readonly HashSet<int> queuedPartRestoreSequences = new HashSet<int>();
        private BattlePvpInputRelay subscribedOpponent;
        private BattleRemoteStepPayload pendingStep;
        private bool hasPendingStep;
        private int lastPolledOpponentStepSequence;
        private int lastPolledOpponentStrikeSequence;
        private int lastPolledOpponentPartRestoreSequence;

        /// <summary>
        /// 入力リレーを参照して同期サービスを生成する
        /// </summary>
        public BattlePvpCombatSync(BattlePvpInputRelay inputRelay)
        {
            this.inputRelay = inputRelay;
        }

        /// <inheritdoc/>
        public bool ShouldDeferRemoteEnemyStrike => inputRelay != null;

        /// <inheritdoc/>
        public void BeginListening()
        {
            EndListening();
            if (inputRelay == null)
            {
                return;
            }

            subscribedOpponent = inputRelay.GetOpponentRelay();
            if (subscribedOpponent == null)
            {
                return;
            }

            subscribedOpponent.RemoteStepPublished += OnRemoteStepPublished;
            subscribedOpponent.RemoteStrikePublished += EnqueueRemoteStrike;
            subscribedOpponent.RemotePartRestorePublished += EnqueueRemotePartRestore;
            lastPolledOpponentStepSequence = subscribedOpponent.StepSequence;
            lastPolledOpponentStrikeSequence = subscribedOpponent.StrikeSequence;
            lastPolledOpponentPartRestoreSequence = subscribedOpponent.PartRestoreSequence;
        }

        /// <inheritdoc/>
        public void EnsureListening()
        {
            if (subscribedOpponent != null)
            {
                return;
            }

            BeginListening();
        }

        /// <inheritdoc/>
        public void PollRemoteSync()
        {
            BattlePvpInputRelay opponent = subscribedOpponent ?? inputRelay?.GetOpponentRelay();
            if (opponent == null)
            {
                return;
            }

            if (subscribedOpponent == null)
            {
                BeginListening();
            }

            int stepSequence = opponent.StepSequence;
            if (stepSequence > lastPolledOpponentStepSequence)
            {
                lastPolledOpponentStepSequence = stepSequence;
                if (!IsStepTracked(stepSequence))
                {
                    EnqueueRemoteStep(opponent.CreateStepPayload(), replaceExisting: false);
                }
            }

            int strikeSequence = opponent.StrikeSequence;
            if (strikeSequence > lastPolledOpponentStrikeSequence)
            {
                lastPolledOpponentStrikeSequence = strikeSequence;
                BattlePvpStrikeResult strike = opponent.CurrentStrikeResult;
                if (strike.Sequence == strikeSequence)
                {
                    EnqueueRemoteStrike(strike);
                }
            }

            int partRestoreSequence = opponent.PartRestoreSequence;
            if (partRestoreSequence > lastPolledOpponentPartRestoreSequence)
            {
                lastPolledOpponentPartRestoreSequence = partRestoreSequence;
                int limbIndex = opponent.CurrentPartRestoreLimbIndex;
                if (limbIndex >= 0)
                {
                    EnqueueRemotePartRestore(new BattleRemotePartRestorePayload(limbIndex, partRestoreSequence));
                }
            }
        }

        /// <inheritdoc/>
        public void EndListening()
        {
            if (subscribedOpponent != null)
            {
                subscribedOpponent.RemoteStepPublished -= OnRemoteStepPublished;
                subscribedOpponent.RemoteStrikePublished -= EnqueueRemoteStrike;
                subscribedOpponent.RemotePartRestorePublished -= EnqueueRemotePartRestore;
                subscribedOpponent = null;
            }

            stepQueue.Clear();
            strikeQueue.Clear();
            partRestoreQueue.Clear();
            consumedStepSequences.Clear();
            queuedStepSequences.Clear();
            queuedStrikeSequences.Clear();
            queuedPartRestoreSequences.Clear();
            pendingStep = default;
            hasPendingStep = false;
            lastPolledOpponentStepSequence = 0;
            lastPolledOpponentStrikeSequence = 0;
            lastPolledOpponentPartRestoreSequence = 0;
        }

        /// <inheritdoc/>
        public void ReportLocalPlayerStrike(MoveUsedResult result, int moveIndex)
        {
            inputRelay?.SubmitStrikeResult(result, moveIndex);
        }

        /// <inheritdoc/>
        public void ReportLocalPlayerStep(int stepIntent, float targetDistance)
        {
            inputRelay?.SubmitStepMove(stepIntent, targetDistance);
        }

        /// <inheritdoc/>
        public void ReportLocalPlayerPartRestored(int limbIndex)
        {
            inputRelay?.SubmitPartRestore(limbIndex);
        }

        /// <inheritdoc/>
        public bool TryConsumeRemoteStrike(out BattleRemoteStrikePayload payload)
        {
            payload = default;
            if (strikeQueue.Count <= 0)
            {
                return false;
            }

            payload = strikeQueue.Dequeue();
            return true;
        }

        /// <inheritdoc/>
        public bool TryConsumeRemoteStep(out BattleRemoteStepPayload payload)
        {
            payload = default;
            if (hasPendingStep)
            {
                payload = pendingStep;
                return true;
            }

            while (stepQueue.Count > 0)
            {
                BattleRemoteStepPayload candidate = stepQueue.Dequeue();
                queuedStepSequences.Remove(candidate.Sequence);
                if (consumedStepSequences.Contains(candidate.Sequence))
                {
                    continue;
                }

                pendingStep = candidate;
                hasPendingStep = true;
                payload = candidate;
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool TryConsumeRemotePartRestore(out BattleRemotePartRestorePayload payload)
        {
            payload = default;
            if (partRestoreQueue.Count <= 0)
            {
                return false;
            }

            payload = partRestoreQueue.Dequeue();
            return true;
        }

        /// <inheritdoc/>
        public void ConfirmRemoteStepConsumed()
        {
            if (!hasPendingStep)
            {
                return;
            }

            consumedStepSequences.Add(pendingStep.Sequence);
            pendingStep = default;
            hasPendingStep = false;
        }

        private void OnRemoteStepPublished(BattleRemoteStepPayload payload)
        {
            EnqueueRemoteStep(payload, replaceExisting: true);
        }

        private bool IsStepTracked(int sequence)
        {
            return consumedStepSequences.Contains(sequence)
                || queuedStepSequences.Contains(sequence)
                || (hasPendingStep && pendingStep.Sequence == sequence);
        }

        private void EnqueueRemoteStep(BattleRemoteStepPayload payload, bool replaceExisting)
        {
            if (payload.Sequence <= 0
                || payload.StepIntent == 0
                || consumedStepSequences.Contains(payload.Sequence))
            {
                return;
            }

            if (hasPendingStep && pendingStep.Sequence == payload.Sequence)
            {
                pendingStep = payload;
                return;
            }

            if (queuedStepSequences.Contains(payload.Sequence))
            {
                if (!replaceExisting)
                {
                    return;
                }

                ReplaceQueuedStep(payload);
                return;
            }

            queuedStepSequences.Add(payload.Sequence);
            stepQueue.Enqueue(payload);
        }

        private void ReplaceQueuedStep(BattleRemoteStepPayload payload)
        {
            int count = stepQueue.Count;
            for (int i = 0; i < count; i++)
            {
                BattleRemoteStepPayload item = stepQueue.Dequeue();
                stepQueue.Enqueue(item.Sequence == payload.Sequence ? payload : item);
            }
        }

        private void EnqueueRemoteStrike(BattlePvpStrikeResult strike)
        {
            if (strike.Sequence <= 0 || queuedStrikeSequences.Contains(strike.Sequence))
            {
                return;
            }

            queuedStrikeSequences.Add(strike.Sequence);
            strikeQueue.Enqueue(new BattleRemoteStrikePayload(
                strike.MoveIndex,
                strike.Hit,
                strike.Damage,
                strike.PartLost,
                (BonePart)strike.LostPart,
                strike.LostLimbIndex,
                strike.IsKnockout));
        }

        private void EnqueueRemotePartRestore(BattleRemotePartRestorePayload payload)
        {
            if (payload.Sequence <= 0
                || payload.LimbIndex < 0
                || queuedPartRestoreSequences.Contains(payload.Sequence))
            {
                return;
            }

            queuedPartRestoreSequences.Add(payload.Sequence);
            partRestoreQueue.Enqueue(payload);
        }
    }
}
