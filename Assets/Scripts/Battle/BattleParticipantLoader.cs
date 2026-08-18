using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Localization;
using SaveData;
using SaveData.Interface;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
            MotionType.Bite,
            MotionType.Fireball,
            MotionType.WindSlasher,
            MotionType.DiamondDust,
            MotionType.ThunderShock,
            MotionType.Chop,
            MotionType.DoubleSlap,
            MotionType.HammerArm,
            MotionType.DoubleKick,
            MotionType.DropKick,
            MotionType.Peck,
            MotionType.HornAttack,
            MotionType.TailSlam,
            MotionType.Rollout
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
            return await LoadEnemyAsync(slotIndex, spawn, null, cancellationToken);
        }

        /// <summary>
        /// 強さ段階のステータスで敵参加者を返す
        /// </summary>
        /// <param name="slotIndex">敵スロット</param>
        /// <param name="spawn">配置先</param>
        /// <param name="strengthTier">強さ段階</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask<BattleParticipant> LoadEnemyAsync(
            int slotIndex,
            Transform spawn,
            EnemyStrengthTier strengthTier,
            CancellationToken cancellationToken)
        {
            return await LoadEnemyAsync(slotIndex, spawn, (EnemyStrengthTier?)strengthTier, cancellationToken);
        }

        /// <summary>
        /// 育成専用強さで敵参加者を返す
        /// NPC対戦の段階ステータスは使わない
        /// </summary>
        /// <param name="slotIndex">敵スロット</param>
        /// <param name="spawn">配置先</param>
        /// <param name="trainingTier">育成強さ段階</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask<BattleParticipant> LoadEnemyForTrainingAsync(
            int slotIndex,
            Transform spawn,
            TrainingEnemyStrengthTier trainingTier,
            CancellationToken cancellationToken)
        {
            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Enemy, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                Debug.LogWarning($"[BattleParticipantLoader] Enemyスロット{slotIndex}にモデルがありません");
                return default;
            }

            string filePath = ModelSaveStorage.ResolveReadPath(slot.glbFileName);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[BattleParticipantLoader] glbが見つかりません: {filePath}");
                return default;
            }

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            GameObject model = await ImportWithTimeoutAsync(
                ModelSavePool.Enemy,
                slotIndex,
                filePath,
                cancellationToken);
            if (model == null)
            {
                Debug.LogError($"[BattleParticipantLoader] Enemyスロット{slotIndex}のインポートに失敗しました");
                return default;
            }

            ModelStatus status = TrainingEnemyStrengthStatusCatalog.Resolve(slot, trainingTier);
            EnemyStrengthTier attackTier =
                TrainingEnemyStrengthStatusCatalog.ToAttackSelectionTier(trainingTier);
            BattleParticipant participant = BuildParticipant(
                model,
                slot,
                status,
                attackTier,
                slotIndex,
                ModelSavePool.Enemy);
            return FinalizeSpawnPlacement(participant, spawn);
        }

        private async UniTask<BattleParticipant> LoadEnemyAsync(
            int slotIndex,
            Transform spawn,
            EnemyStrengthTier? strengthTier,
            CancellationToken cancellationToken)
        {
            return await LoadFromPoolAsync(
                ModelSavePool.Enemy,
                slotIndex,
                spawn,
                strengthTier,
                cancellationToken);
        }

        /// <summary>
        /// 育成後スロットのglbを読み込み対戦相手として配置する
        /// </summary>
        public async UniTask<BattleParticipant> LoadPlayerSlotAsync(int slotIndex, Transform spawn, CancellationToken cancellationToken)
        {
            return await LoadFromPoolAsync(ModelSavePool.TrainedPlayer, slotIndex, spawn, null, cancellationToken);
        }

        /// <summary>
        /// glbバイナリとスロットメタから参加者を構築する
        /// </summary>
        /// <param name="slot">表示と技に使うスロット</param>
        /// <param name="glbBytes">glbバイナリ</param>
        /// <param name="spawn">配置先</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask<BattleParticipant> LoadFromGlbBytesAsync(
            ModelSaveSlot slot,
            byte[] glbBytes,
            Transform spawn,
            CancellationToken cancellationToken)
        {
            if (slot == null || glbBytes == null || glbBytes.Length == 0)
            {
                Debug.LogError("[BattleParticipantLoader] 対人戦の受信モデルが空です");
                return default;
            }

            string tempFileName = $"PvpRemoteOpponent_{System.Guid.NewGuid():N}.glb";
            string tempPath = Path.Combine(Application.temporaryCachePath, tempFileName);
            try
            {
                await UniTask.RunOnThreadPool(
                    () => File.WriteAllBytes(tempPath, glbBytes),
                    cancellationToken: cancellationToken);

                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                GameObject model = await ImportWithTimeoutAsync(
                    ModelSavePool.TrainedPlayer,
                    -1,
                    tempPath,
                    cancellationToken);
                if (model == null)
                {
                    Debug.LogError("[BattleParticipantLoader] 対人戦受信モデルのインポートに失敗しました");
                    return default;
                }

                BattleParticipant participant = BuildParticipant(
                    model,
                    slot,
                    ModelStatus.CloneOrDefault(slot.status),
                    null,
                    -1,
                    ModelSavePool.TrainedPlayer);
                return FinalizeSpawnPlacement(participant, spawn);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning(
                        $"[BattleParticipantLoader] 一時glb削除に失敗しました: {exception.Message}");
                }
            }
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

            BattleParticipant participant = BuildParticipant(model, slot, pool, slotIndex);
            return FinalizeSpawnPlacement(participant, spawn);
        }

        /// <summary>
        /// 読み込み済み敵モデルの強さ段階だけ差し替えて参加者を再構築する
        /// </summary>
        /// <param name="model">既存モデル</param>
        /// <param name="slotIndex">敵スロット</param>
        /// <param name="strengthTier">強さ段階</param>
        public BattleParticipant RebuildEnemyWithStrengthTier(
            GameObject model,
            int slotIndex,
            EnemyStrengthTier strengthTier)
        {
            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Enemy, slotIndex);
            if (model == null || slot == null)
            {
                return default;
            }

            ModelStatus status = EnemyStrengthStatusCatalog.Resolve(slot, strengthTier);
            ProceduralMotionCharacter motion = model.GetComponent<ProceduralMotionCharacter>();
            ModelPartLossController partLoss = model.GetComponent<ModelPartLossController>();
            if (motion == null || !motion.IsReady)
            {
                return BuildParticipant(model, slot, status, strengthTier, slotIndex, ModelSavePool.Enemy);
            }

            NormalizeStatus(status, out int hp, out int attack, out int defense, out int speed, out int hit);
            var unit = new BattleUnit(
                EnemyDisplayName.Resolve(slotIndex, slot.modelName),
                hp,
                attack,
                defense,
                speed,
                hit,
                ResolveAttackMotions(slot, model, partLoss, strengthTier, slotIndex),
                motion,
                partLoss);

            return new BattleParticipant { Model = model, Unit = unit };
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

            IReadOnlyList<MotionType> motions = ModelAttackMotionUtility.SanitizeForAvailableParts(
                attackMotions != null && attackMotions.Count > 0 ? attackMotions : DefaultAttackMotions,
                ModelAttackMotionUtility.CollectAvailableParts(configured.PartLoss),
                ModelAttackMotionUtility.SlotCount);

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
            return ResolveReusableOrConfigure(model);
        }

        /// <summary>
        /// 再利用モデルは再Initializeしない
        /// 再Initializeすると現在ポーズが基準回転になり見た目が崩れる
        /// </summary>
        /// <param name="model">対象モデル</param>
        private LoadedModelConfigurator.Result ResolveReusableOrConfigure(GameObject model)
        {
            ProceduralMotionCharacter existingMotion = model.GetComponent<ProceduralMotionCharacter>();
            if (existingMotion != null && existingMotion.IsReady)
            {
                ModelPartLossController existingPartLoss = model.GetComponent<ModelPartLossController>();
                existingPartLoss?.RestoreAll();
                existingMotion.ResetForTrainingDisplay();
                existingMotion.ClearBattlePositionConstraint();
                existingMotion.SetRootTranslationEnabled(false);
                return new LoadedModelConfigurator.Result
                {
                    Renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true),
                    Motion = existingMotion,
                    PartLoss = existingPartLoss
                };
            }

            return configurator.Configure(model);
        }

        private async UniTask<BattleParticipant> LoadFromPoolAsync(
            ModelSavePool pool,
            int slotIndex,
            Transform spawn,
            EnemyStrengthTier? strengthTier,
            CancellationToken cancellationToken)
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

            ModelStatus status = strengthTier.HasValue
                ? EnemyStrengthStatusCatalog.Resolve(slot, strengthTier.Value)
                : ModelStatus.CloneOrDefault(slot.status);
            BattleParticipant participant = BuildParticipant(
                model,
                slot,
                status,
                strengthTier,
                slotIndex,
                pool);
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

        private BattleParticipant BuildParticipant(
            GameObject model,
            ModelSaveSlot slot,
            ModelSavePool pool,
            int slotIndex)
        {
            return BuildParticipant(
                model,
                slot,
                ModelStatus.CloneOrDefault(slot.status),
                null,
                slotIndex,
                pool);
        }

        private BattleParticipant BuildParticipant(
            GameObject model,
            ModelSaveSlot slot,
            ModelStatus status,
            EnemyStrengthTier? strengthTier,
            int slotIndex,
            ModelSavePool pool)
        {
            LoadedModelConfigurator.Result cfg = ResolveReusableOrConfigure(model);

            string displayName = ResolveDisplayName(pool, slotIndex, slot);

            if (cfg.Motion == null || !cfg.Motion.IsReady)
            {
                Debug.LogWarning($"[BattleParticipantLoader] {displayName}のモーション初期化に失敗しました。モデルが動かない可能性があります");
            }
            else
            {
                cfg.Motion.ClearBattlePositionConstraint();
                cfg.Motion.SetRootTranslationEnabled(false);
                cfg.Motion.Play(MotionType.Idle);
            }

            cfg.PartLoss?.SuspendMeshRebuild();

            NormalizeStatus(status, out int hp, out int attack, out int defense, out int speed, out int hit);

            var unit = new BattleUnit(
                displayName,
                hp, attack, defense, speed, hit,
                ResolveAttackMotions(slot, model, cfg.PartLoss, strengthTier, slotIndex),
                cfg.Motion,
                cfg.PartLoss);

            return new BattleParticipant { Model = model, Unit = unit };
        }

        private static string ResolveDisplayName(ModelSavePool pool, int slotIndex, ModelSaveSlot slot)
        {
            string fallback = slot != null ? slot.modelName : string.Empty;
            if (pool == ModelSavePool.Enemy)
            {
                return EnemyDisplayName.Resolve(slotIndex, fallback);
            }

            return fallback ?? string.Empty;
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

        // 敵は強さ段階で技を差し替えプレイヤーは保存技を使う
        // 候補は骨格解析で使える技だけに限定する
        private IReadOnlyList<MotionType> ResolveAttackMotions(
            ModelSaveSlot slot,
            GameObject model,
            ModelPartLossController partLoss,
            EnemyStrengthTier? strengthTier,
            int slotIndex)
        {
            HashSet<BonePart> availableParts = ModelAttackMotionUtility.CollectAvailableParts(partLoss);

            if (strengthTier.HasValue)
            {
                List<MotionType> usableAttacks = CollectUsableAttacks(model);
                int seedSlot = slotIndex >= 0 ? slotIndex : 0;
                IReadOnlyList<MotionType> pool = usableAttacks.Count > 0
                    ? usableAttacks
                    : AttackMotionSelector.CollectAttacksForAvailableParts(availableParts);
                if (usableAttacks.Count == 0)
                {
                    Debug.LogError(
                        "[BattleParticipantLoader] 骨格の使用可能技が空のため部位集合から候補を作りました");
                }

                List<MotionType> byTier = EnemyStrengthAttackCatalog.Resolve(
                    strengthTier.Value,
                    pool,
                    seedSlot,
                    ModelAttackMotionUtility.SlotCount);

                // 強さ抽選結果を壊さないよう使用不可技だけ落とし補充はしない
                return KeepUsableOnly(byTier, pool, ModelAttackMotionUtility.SlotCount);
            }

            IReadOnlyList<MotionType> source = slot.attackMotions != null && slot.attackMotions.Count > 0
                ? slot.attackMotions
                : DefaultAttackMotions;

            // プレイヤーは保存技を部位欠損状態で絞る
            // 試合後ボーン縮尺が残った骨格再判定だと技数が減ることがある
            return ModelAttackMotionUtility.SanitizeForAvailableParts(
                source,
                availableParts,
                ModelAttackMotionUtility.SlotCount);
        }

        private List<MotionType> CollectUsableAttacks(GameObject model)
        {
            var empty = new List<MotionType>();
            if (model == null)
            {
                return empty;
            }

            if (configurator == null || configurator.PartAnalyzer == null)
            {
                Debug.LogError(
                    "[BattleParticipantLoader] PartAnalyzerが未配線のため使用可能技を骨格判定できません");
                return empty;
            }

            SkinnedMeshRenderer renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null || renderer.bones == null || renderer.bones.Length == 0)
            {
                Debug.LogError("[BattleParticipantLoader] ボーンが無いため使用可能技を判定できません");
                return empty;
            }

            return AttackMotionSelector.CollectUsableAttacks(configurator.PartAnalyzer, renderer.bones);
        }

        private static List<MotionType> KeepUsableOnly(
            IReadOnlyList<MotionType> attacks,
            IReadOnlyList<MotionType> usableAttacks,
            int slotCount)
        {
            var usableSet = new HashSet<MotionType>();
            if (usableAttacks != null)
            {
                for (int i = 0; i < usableAttacks.Count; i++)
                {
                    MotionType motion = usableAttacks[i];
                    if (ProceduralMotionCharacter.IsAttackMotion(motion))
                    {
                        usableSet.Add(motion);
                    }
                }
            }

            var result = new List<MotionType>();
            if (attacks == null)
            {
                return result;
            }

            for (int i = 0; i < attacks.Count; i++)
            {
                MotionType motion = attacks[i];
                if (!usableSet.Contains(motion))
                {
                    Debug.LogError(
                        $"[BattleParticipantLoader] 部位条件を満たさない攻撃{motion}を除外しました");
                    continue;
                }

                if (result.Contains(motion))
                {
                    continue;
                }

                result.Add(motion);
                if (result.Count >= slotCount)
                {
                    break;
                }
            }

            return result;
        }
    }
}
