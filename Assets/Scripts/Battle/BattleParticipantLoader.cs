using System.Collections.Generic;
using System.IO;
using System.Threading;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData;
using SaveData.Interface;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘参加者(モデルとBattleUnit)
    /// </summary>
    public struct BattleParticipant
    {
        public GameObject Model;
        public BattleUnit Unit;

        /// <summary>
        /// 有効に構築できたか
        /// </summary>
        public bool IsValid => Unit != null && Model != null;
    }

    /// <summary>
    /// セーブスロットから戦闘参加者(モデル配置・コンポーネント付与・BattleUnit生成)を作る
    /// </summary>
    public sealed class BattleParticipantLoader
    {
        private const int ImportTimeoutMilliseconds = 120000;

        private static readonly MotionType[] DefaultAttackMotions =
        {
            MotionType.Punch,
            MotionType.Kick,
            MotionType.Elbow,
            MotionType.Headbutt,
            MotionType.Tackle,
            MotionType.Stomp,
            MotionType.SpinTackle,
            MotionType.BodySlam,
            MotionType.Uppercut,
            MotionType.Knee,
            MotionType.TailWhip,
            MotionType.ShoulderRam,
            MotionType.BellyFlop,
            MotionType.HipCheck,
            MotionType.GroundPound,
            MotionType.Slap,
            MotionType.LowSweep,
            MotionType.Bite
        };

        private readonly IClayModelImporter importer;
        private readonly IClayModelSaveService saveService;
        private readonly LoadedModelConfigurator configurator;

        public BattleParticipantLoader(IClayModelImporter importer, IClayModelSaveService saveService, LoadedModelConfigurator configurator)
        {
            this.importer = importer;
            this.saveService = saveService;
            this.configurator = configurator;
        }

        /// <summary>
        /// スロットのglbを読み込み・spawnに配置・構築して敵参加者を返す
        /// </summary>
        public async UniTask<BattleParticipant> LoadEnemyAsync(int slotIndex, Transform spawn, CancellationToken cancellationToken)
        {
            return await LoadFromPoolAsync(ModelSavePool.Enemy, slotIndex, spawn, cancellationToken);
        }

        /// <summary>
        /// 育成後スロットのglbを読み込み対戦相手として配置する
        /// </summary>
        public async UniTask<BattleParticipant> LoadPlayerSlotAsync(int slotIndex, Transform spawn, CancellationToken cancellationToken)
        {
            return await LoadFromPoolAsync(ModelSavePool.TrainedPlayer, slotIndex, spawn, cancellationToken);
        }

        /// <summary>
        /// すでに読み込み済みのモデルから参加者を構築する(プレイヤーの選択済みモデル用)
        /// </summary>
        public BattleParticipant BuildFromLoadedModel(
            GameObject model,
            ModelSavePool pool,
            int slotIndex,
            Transform spawn)
        {
            ModelSaveSlot slot = saveService.GetSlot(pool, slotIndex);
            if (model == null || slot == null)
            {
                return default;
            }

            BattleParticipant participant = BuildParticipant(model, slot);
            return FinalizeSpawnPlacement(participant, spawn);
        }

        /// <summary>
        /// 育成中ステータスを反映した参加者を構築する
        /// </summary>
        /// <param name="model">モデル</param>
        /// <param name="modelName">表示名</param>
        /// <param name="status">現在ステータス</param>
        /// <param name="attackMotions">攻撃構成</param>
        /// <param name="spawn">配置先</param>
        public BattleParticipant BuildFromTrainingModel(
            GameObject model,
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions,
            Transform spawn)
        {
            if (model == null || status == null)
            {
                return default;
            }

            LoadedModelConfigurator.Result configured = ResolveTrainingModelConfiguration(model);
            if (configured.Motion == null || !configured.Motion.IsReady)
            {
                Debug.LogWarning($"[BattleParticipantLoader] {modelName}のモーション初期化に失敗しました。モデルが動かない可能性があります");
            }
            else
            {
                configured.Motion.ClearBattlePositionConstraint();
                configured.Motion.SetRootTranslationEnabled(false);
                configured.Motion.Play(MotionType.Idle);
            }

            configured.PartLoss?.SuspendMeshRebuild();
            NormalizeStatus(status, out int hp, out int attack, out int defense, out int speed, out int hit);

            IReadOnlyList<MotionType> motions = attackMotions != null && attackMotions.Count > 0
                ? attackMotions
                : DefaultAttackMotions;

            var unit = new BattleUnit(
                modelName,
                hp,
                attack,
                defense,
                speed,
                hit,
                motions,
                configured.Motion,
                configured.PartLoss);

            var participant = new BattleParticipant { Model = model, Unit = unit };
            return FinalizeSpawnPlacement(participant, spawn);
        }

        private LoadedModelConfigurator.Result ResolveTrainingModelConfiguration(GameObject model)
        {
            ProceduralMotionCharacter existingMotion = model.GetComponent<ProceduralMotionCharacter>();
            if (existingMotion != null && existingMotion.IsReady)
            {
                ModelPartLossController existingPartLoss = model.GetComponent<ModelPartLossController>();
                existingPartLoss?.RestoreAll();
                return new LoadedModelConfigurator.Result
                {
                    Renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true),
                    Motion = existingMotion,
                    PartLoss = existingPartLoss
                };
            }

            return configurator.Configure(model);
        }

        private async UniTask<BattleParticipant> LoadFromPoolAsync(ModelSavePool pool, int slotIndex, Transform spawn, CancellationToken cancellationToken)
        {
            ModelSaveSlot slot = saveService.GetSlot(pool, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                Debug.LogWarning($"[BattleParticipantLoader] {pool}スロット{slotIndex}にモデルがありません");
                return default;
            }

            string filePath = ModelSaveStorage.ResolveReadPath(slot.glbFileName);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[BattleParticipantLoader] glbが見つかりません: {filePath}");
                return default;
            }

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            GameObject model = await ImportWithTimeoutAsync(pool, slotIndex, filePath, cancellationToken);
            if (model == null)
            {
                Debug.LogError($"[BattleParticipantLoader] {pool}スロット{slotIndex}のインポートに失敗しました");
                return default;
            }

            BattleParticipant participant = BuildParticipant(model, slot);
            return FinalizeSpawnPlacement(participant, spawn);
        }

        private async UniTask<GameObject> ImportWithTimeoutAsync(
            ModelSavePool pool,
            int slotIndex,
            string filePath,
            CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ImportTimeoutMilliseconds);

            try
            {
                return await importer.ImportFromGlbAsync(filePath, null, timeoutCts.Token);
            }
            catch (System.OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Debug.LogError(
                    $"[BattleParticipantLoader] {pool}スロット{slotIndex}のインポートがタイムアウトしました: {filePath}");
                return null;
            }
        }

        private static BattleParticipant FinalizeSpawnPlacement(BattleParticipant participant, Transform spawn)
        {
            if (!participant.IsValid || spawn == null)
            {
                return participant;
            }

            BattleSpawnPlacement.Apply(participant.Model.transform, spawn);
            participant.Unit.SyncMotionLayoutPosition();
            return participant;
        }

        private BattleParticipant BuildParticipant(GameObject model, ModelSaveSlot slot)
        {
            LoadedModelConfigurator.Result cfg = configurator.Configure(model);

            if (cfg.Motion == null || !cfg.Motion.IsReady)
            {
                Debug.LogWarning($"[BattleParticipantLoader] {slot.modelName}のモーション初期化に失敗しました。モデルが動かない可能性があります");
            }
            else
            {
                cfg.Motion.Play(MotionType.Idle);
            }

            cfg.PartLoss?.SuspendMeshRebuild();

            NormalizeStatus(slot.status, out int hp, out int attack, out int defense, out int speed, out int hit);

            var unit = new BattleUnit(
                slot.modelName,
                hp, attack, defense, speed, hit,
                ResolveAttackMotions(slot),
                cfg.Motion,
                cfg.PartLoss);

            return new BattleParticipant { Model = model, Unit = unit };
        }

        // 未設定や旧データのステータスを戦闘向けに補正する
        private static void NormalizeStatus(
            ModelStatus status,
            out int hp,
            out int attack,
            out int defense,
            out int speed,
            out int hit)
        {
            BattleStatusBalance.Normalize(status, out hp, out attack, out defense, out speed, out hit);
        }

        // 攻撃モーションが空のスロット向けに既定技を返す
        private static IReadOnlyList<MotionType> ResolveAttackMotions(ModelSaveSlot slot)
        {
            if (slot.attackMotions != null && slot.attackMotions.Count > 0)
            {
                return slot.attackMotions;
            }

            return DefaultAttackMotions;
        }
    }
}