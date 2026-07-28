using System.Collections.Generic;
using System.IO;
using System.Threading;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData.Interface;
using UnityEngine;

namespace SaveData.Service
{
    /// <summary>
    /// プレイヤー用と敵用のモデルをスロット単位でセーブ・ロードするサービス
    /// glbはプールごとのファイルへ出力しメタ情報をJSONで保存する
    /// </summary>
    public class ClayModelSaveService : IClayModelSaveService
    {
        public const int AttackMotionCount = ModelAttackMotionUtility.SlotCount;

        private readonly IClayModelExporter exporter;

        /// <summary>
        /// セーブサービスを生成する
        /// </summary>
        /// <param name="exporter">glb出力に使うエクスポーター</param>
        public ClayModelSaveService(IClayModelExporter exporter)
        {
            this.exporter = exporter;
        }

        /// <inheritdoc />
        public async UniTask<bool> SaveAsync(
            ModelSavePool pool,
            int slotIndex,
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions,
            SkinnedMeshRenderer runtimeRenderer,
            Transform boneRoot,
            byte[] thumbnailPng,
            CancellationToken cancellationToken)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndex))
            {
                Debug.LogError($"[ClayModelSaveService] スロット番号が範囲外です: {slotIndex}");
                return false;
            }

            if (runtimeRenderer == null
                || runtimeRenderer.sharedMesh == null
                || runtimeRenderer.sharedMesh.vertexCount == 0)
            {
                Debug.LogWarning("[ClayModelSaveService] メッシュが無いため保存できません");
                return false;
            }

            if (boneRoot == null || boneRoot.childCount == 0)
            {
                Debug.LogWarning("[ClayModelSaveService] ボーンが無いため保存できません");
                return false;
            }

            if (pool == ModelSavePool.Enemy && !EnemySaveAvailability.IsAvailable)
            {
                Debug.LogWarning("[ClayModelSaveService] 敵保存はEditorまたはROM敵保存Build Profileでのみ利用できます");
                return false;
            }

            string glbFileName = ModelSavePoolSettings.GetGlbFileName(pool, slotIndex);
            string glbFilePath = Path.Combine(Application.persistentDataPath, glbFileName);

            bool exported = await exporter.ExportToGlbAsync(runtimeRenderer, boneRoot, glbFilePath, cancellationToken);
            if (!exported)
            {
                Debug.LogError("[ClayModelSaveService] glbの出力に失敗しました");
                return false;
            }

            if (!ModelSaveStorage.CompressWritableFile(glbFileName))
            {
                Debug.LogError("[ClayModelSaveService] glbの圧縮に失敗しました");
                return false;
            }

            string thumbnailFileName = null;
            if (thumbnailPng != null && thumbnailPng.Length > 0)
            {
                thumbnailFileName = ModelSavePoolSettings.GetThumbnailFileName(pool, slotIndex);
                ModelSaveStorage.WriteAllBytes(thumbnailFileName, thumbnailPng);
            }

            List<MotionType> attacks = attackMotions != null
                ? new List<MotionType>(attackMotions)
                : new List<MotionType>();
            if (attacks.Count != AttackMotionCount)
            {
                Debug.LogError(
                    $"[ClayModelSaveService] 攻撃スロット数が不正です({attacks.Count}/{AttackMotionCount})");
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            TrainingSlotProgress existingProgress = data.slots[slotIndex].trainingProgress;
            ModelSaveSlot written = new ModelSaveSlot
            {
                isUsed = true,
                modelName = modelName,
                status = status,
                glbFileName = glbFileName,
                thumbnailFileName = thumbnailFileName,
                attackMotions = attacks,
                // モデル再保存で育成途中データを消さない
                trainingProgress = existingProgress
            };
            if (pool == ModelSavePool.Enemy)
            {
                EnemyStrengthStatusCatalog.ApplyGeneratedFromBase(written, status);
            }

            data.slots[slotIndex] = written;

            WriteToFile(pool, data);
            return true;
        }

        /// <inheritdoc />
        public ClayModelSaveData Load(ModelSavePool pool)
        {
            return LoadOrCreate(pool);
        }

        /// <inheritdoc />
        public ModelSaveSlot GetSlot(ModelSavePool pool, int slotIndex)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndex))
            {
                return null;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            ModelSaveSlot slot = data.slots[slotIndex];
            return slot.isUsed ? slot : null;
        }

        /// <inheritdoc />
        public Texture2D LoadThumbnail(ModelSavePool pool, int slotIndex)
        {
            ModelSaveSlot slot = GetSlot(pool, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.thumbnailFileName))
            {
                return null;
            }

            byte[] bytes = ModelSaveStorage.ReadAllBytes(slot.thumbnailFileName);
            if (bytes == null)
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!texture.LoadImage(bytes))
            {
                Object.Destroy(texture);
                return null;
            }

            return texture;
        }

        /// <inheritdoc />
        public bool HasAnySavedModel(ModelSavePool pool)
        {
            ClayModelSaveData data = LoadOrCreate(pool);
            for (int i = 0; i < ModelSavePoolSettings.GetSlotCount(pool); i++)
            {
                ModelSaveSlot slot = data.slots[i];
                if (!slot.isUsed || string.IsNullOrEmpty(slot.glbFileName))
                {
                    continue;
                }

                if (ModelSaveStorage.Exists(slot.glbFileName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public bool UpdateSlotStatus(ModelSavePool pool, int slotIndex, ModelStatus status)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndex) || status == null)
            {
                return false;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            ModelSaveSlot slot = data.slots[slotIndex];
            if (!slot.isUsed)
            {
                return false;
            }

            slot.status = new ModelStatus
            {
                hp = status.hp,
                attack = status.attack,
                defense = status.defense,
                speed = status.speed,
                hit = status.hit
            };
            data.slots[slotIndex] = slot;
            WriteToFile(pool, data);
            return true;
        }

        /// <inheritdoc />
        public bool UpdateSlotAttackMotions(ModelSavePool pool, int slotIndex, IReadOnlyList<MotionType> attackMotions)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndex) || attackMotions == null || attackMotions.Count == 0)
            {
                return false;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            ModelSaveSlot slot = data.slots[slotIndex];
            if (!slot.isUsed)
            {
                return false;
            }

            slot.attackMotions = ModelAttackMotionUtility.Normalize(attackMotions, AttackMotionCount);

            data.slots[slotIndex] = slot;
            WriteToFile(pool, data);
            return true;
        }

        /// <inheritdoc />
        public TrainingSlotProgress GetTrainingProgress(ModelSavePool pool, int slotIndex)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndex))
            {
                return null;
            }

            TrainingSlotProgress fileProgress = ReadTrainingProgressFile(pool, slotIndex);
            if (fileProgress != null && fileProgress.inProgress)
            {
                return CloneTrainingProgress(fileProgress);
            }

            ModelSaveSlot slot = GetSlot(pool, slotIndex);
            if (slot?.trainingProgress == null || !slot.trainingProgress.inProgress)
            {
                return null;
            }

            return CloneTrainingProgress(slot.trainingProgress);
        }

        /// <inheritdoc />
        public bool SaveTrainingProgress(ModelSavePool pool, int slotIndex, TrainingSlotProgress progress)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndex))
            {
                Debug.LogError($"[ClayModelSaveService] 育成途中データのスロット番号が不正です: {slotIndex}");
                return false;
            }

            if (progress == null || !progress.inProgress)
            {
                Debug.LogError("[ClayModelSaveService] 育成途中データが進行中ではありません");
                return false;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            ModelSaveSlot slot = data.slots[slotIndex];
            if (!slot.isUsed)
            {
                Debug.LogError($"[ClayModelSaveService] 未使用スロット{slotIndex}へ育成途中データを保存できません");
                return false;
            }

            TrainingSlotProgress cloned = CloneTrainingProgress(progress);
            slot.trainingProgress = cloned;
            data.slots[slotIndex] = slot;
            WriteToFile(pool, data);
            WriteTrainingProgressFile(pool, slotIndex, cloned);

            TrainingSlotProgress verified = GetTrainingProgress(pool, slotIndex);
            if (verified == null || !verified.inProgress)
            {
                Debug.LogError(
                    $"[ClayModelSaveService] 育成途中データの保存検証に失敗しました slot={slotIndex}");
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public void ClearTrainingProgress(ModelSavePool pool, int slotIndex)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndex))
            {
                return;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            ModelSaveSlot slot = data.slots[slotIndex];
            if (slot.trainingProgress != null)
            {
                slot.trainingProgress = null;
                data.slots[slotIndex] = slot;
                WriteToFile(pool, data);
            }

            DeleteTrainingProgressFile(pool, slotIndex);
        }

        /// <inheritdoc />
        public bool HasTrainingProgress(ModelSavePool pool, int slotIndex)
        {
            return GetTrainingProgress(pool, slotIndex) != null;
        }

        /// <inheritdoc />
        public async UniTask<bool> SaveTrainedResultAsync(
            int trainedSlotIndex,
            int playerSlotIndex,
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions,
            SkinnedMeshRenderer runtimeRenderer,
            Transform boneRoot,
            CancellationToken cancellationToken)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(ModelSavePool.TrainedPlayer, trainedSlotIndex)
                || !ModelSavePoolSettings.IsValidSlotIndex(ModelSavePool.Player, playerSlotIndex)
                || status == null)
            {
                return false;
            }

            ModelSaveSlot playerSlot = ResolvePlayerSourceSlot(playerSlotIndex);
            if (playerSlot == null)
            {
                Debug.LogError($"[ClayModelSaveService] 未育成スロット{playerSlotIndex}のデータが見つかりません");
                return false;
            }

            string resolvedModelName = string.IsNullOrEmpty(modelName)
                ? (string.IsNullOrEmpty(playerSlot.modelName) ? "Monster" : playerSlot.modelName)
                : modelName;
            ModelStatus statusCopy = ModelStatus.CloneOrDefault(status);
            List<MotionType> attacks = ModelAttackMotionUtility.Normalize(attackMotions, AttackMotionCount);

            if (runtimeRenderer != null && boneRoot != null)
            {
                byte[] thumbnailPng = ModelSaveStorage.ReadThumbnailPng(playerSlot);
                return await SaveAsync(
                    ModelSavePool.TrainedPlayer,
                    trainedSlotIndex,
                    resolvedModelName,
                    statusCopy,
                    attacks,
                    runtimeRenderer,
                    boneRoot,
                    thumbnailPng,
                    cancellationToken);
            }

            return SaveTrainedResultFromPlayerSlot(
                trainedSlotIndex,
                playerSlotIndex,
                resolvedModelName,
                statusCopy,
                attacks);
        }

        /// <inheritdoc />
        public bool SaveTrainedResultFromPlayerSlot(
            int trainedSlotIndex,
            int playerSlotIndex,
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(ModelSavePool.TrainedPlayer, trainedSlotIndex)
                || !ModelSavePoolSettings.IsValidSlotIndex(ModelSavePool.Player, playerSlotIndex)
                || status == null)
            {
                return false;
            }

            ModelSaveSlot playerSlot = ResolvePlayerSourceSlot(playerSlotIndex);
            if (playerSlot == null)
            {
                return false;
            }

            byte[] sourceGlb = ModelSaveStorage.ReadAllBytes(playerSlot.glbFileName);
            if (sourceGlb == null)
            {
                Debug.LogError($"[ClayModelSaveService] 未育成glbが見つかりません: {playerSlot.glbFileName}");
                return false;
            }

            string trainedGlbFileName = ModelSavePoolSettings.GetGlbFileName(ModelSavePool.TrainedPlayer, trainedSlotIndex);
            ModelSaveStorage.WriteAllBytes(trainedGlbFileName, sourceGlb);

            string trainedThumbnailFileName = null;
            if (!string.IsNullOrEmpty(playerSlot.thumbnailFileName))
            {
                byte[] sourceThumbnail = ModelSaveStorage.ReadAllBytes(playerSlot.thumbnailFileName);
                if (sourceThumbnail != null)
                {
                    trainedThumbnailFileName = ModelSavePoolSettings.GetThumbnailFileName(
                        ModelSavePool.TrainedPlayer,
                        trainedSlotIndex);
                    ModelSaveStorage.WriteAllBytes(trainedThumbnailFileName, sourceThumbnail);
                }
            }

            string resolvedModelName = string.IsNullOrEmpty(modelName)
                ? playerSlot.modelName
                : modelName;
            List<MotionType> attacks = ModelAttackMotionUtility.Normalize(attackMotions, AttackMotionCount);
            ClayModelSaveData data = LoadOrCreate(ModelSavePool.TrainedPlayer);
            data.slots[trainedSlotIndex] = new ModelSaveSlot
            {
                isUsed = true,
                modelName = resolvedModelName,
                status = ModelStatus.CloneOrDefault(status),
                glbFileName = trainedGlbFileName,
                thumbnailFileName = trainedThumbnailFileName,
                attackMotions = attacks
            };
            WriteToFile(ModelSavePool.TrainedPlayer, data);
            return true;
        }

        private ModelSaveSlot ResolvePlayerSourceSlot(int slotIndex)
        {
            ClayModelSaveData data = LoadOrCreate(ModelSavePool.Player);
            ModelSaveSlot slot = data.slots[slotIndex];
            if (string.IsNullOrEmpty(slot.glbFileName))
            {
                return null;
            }

            if (!ModelSaveStorage.Exists(slot.glbFileName))
            {
                return null;
            }

            return slot;
        }

        private static TrainingSlotProgress CloneTrainingProgress(TrainingSlotProgress source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new TrainingSlotProgress
            {
                inProgress = source.inProgress,
                day = source.day,
                turnIndexInDay = source.turnIndexInDay,
                stamina = source.stamina,
                motivation = source.motivation,
                money = source.money,
                trainGreatSuccessBonusPercent = source.trainGreatSuccessBonusPercent,
                trainGreatSuccessBonusWeeks = source.trainGreatSuccessBonusWeeks,
                inventory = CloneInventory(source.inventory),
                shopOfferItemIds = source.shopOfferItemIds != null
                    ? new List<string>(source.shopOfferItemIds)
                    : new List<string>(),
                status = source.status != null
                    ? new ModelStatus
                    {
                        hp = source.status.hp,
                        attack = source.status.attack,
                        defense = source.status.defense,
                        speed = source.status.speed,
                        hit = source.status.hit
                    }
                    : new ModelStatus(),
                attackMotions = source.attackMotions != null
                    ? new List<MotionType>(source.attackMotions)
                    : new List<MotionType>()
            };
            return clone;
        }

        private static List<TrainingInventoryEntry> CloneInventory(
            IReadOnlyList<TrainingInventoryEntry> source)
        {
            var clone = new List<TrainingInventoryEntry>();
            if (source == null)
            {
                return clone;
            }

            for (int i = 0; i < source.Count; i++)
            {
                TrainingInventoryEntry entry = source[i];
                if (entry == null)
                {
                    continue;
                }

                clone.Add(new TrainingInventoryEntry
                {
                    itemId = entry.itemId,
                    count = entry.count
                });
            }

            return clone;
        }

        private static void WriteTrainingProgressFile(
            ModelSavePool pool,
            int slotIndex,
            TrainingSlotProgress progress)
        {
            string fileName = ModelSavePoolSettings.GetTrainingProgressFileName(pool, slotIndex);
            string json = JsonUtility.ToJson(progress, true);
            ModelSaveStorage.WriteAllText(fileName, json);
        }

        private static TrainingSlotProgress ReadTrainingProgressFile(ModelSavePool pool, int slotIndex)
        {
            string fileName = ModelSavePoolSettings.GetTrainingProgressFileName(pool, slotIndex);
            if (!ModelSaveStorage.Exists(fileName))
            {
                return null;
            }

            string json = ModelSaveStorage.ReadAllText(fileName);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonUtility.FromJson<TrainingSlotProgress>(json);
        }

        private static void DeleteTrainingProgressFile(ModelSavePool pool, int slotIndex)
        {
            string fileName = ModelSavePoolSettings.GetTrainingProgressFileName(pool, slotIndex);
            ModelSaveStorage.Delete(fileName);
        }

        /// <inheritdoc />
        public void DeleteSlot(ModelSavePool pool, int slotIndex)
        {
            // 育成前と育成済みは別プールのため指定プールのみ削除する
            DeleteSlotInternal(pool, slotIndex);
        }

        /// <inheritdoc />
        public bool SwapSlots(ModelSavePool pool, int slotIndexA, int slotIndexB)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndexA)
                || !ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndexB))
            {
                Debug.LogError(
                    "[ClayModelSaveService] スロット番号が範囲外です"
                    + $" a={slotIndexA} b={slotIndexB}");
                return false;
            }

            if (slotIndexA == slotIndexB)
            {
                return true;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            SwapLogicalFiles(
                ModelSavePoolSettings.GetGlbFileName(pool, slotIndexA),
                ModelSavePoolSettings.GetGlbFileName(pool, slotIndexB));
            SwapLogicalFiles(
                ModelSavePoolSettings.GetThumbnailFileName(pool, slotIndexA),
                ModelSavePoolSettings.GetThumbnailFileName(pool, slotIndexB));
            SwapLogicalFiles(
                ModelSavePoolSettings.GetVoxelFileName(pool, slotIndexA),
                ModelSavePoolSettings.GetVoxelFileName(pool, slotIndexB));
            SwapLogicalFiles(
                ModelSavePoolSettings.GetTrainingProgressFileName(pool, slotIndexA),
                ModelSavePoolSettings.GetTrainingProgressFileName(pool, slotIndexB));

            ModelSaveSlot slotA = data.slots[slotIndexA] ?? new ModelSaveSlot();
            ModelSaveSlot slotB = data.slots[slotIndexB] ?? new ModelSaveSlot();
            data.slots[slotIndexA] = slotB;
            data.slots[slotIndexB] = slotA;
            AssignCanonicalFileNames(pool, slotIndexA, data.slots[slotIndexA]);
            AssignCanonicalFileNames(pool, slotIndexB, data.slots[slotIndexB]);
            WriteToFile(pool, data);
            return true;
        }

        private static void SwapLogicalFiles(string fileNameA, string fileNameB)
        {
            byte[] bytesA = ModelSaveStorage.ReadAllBytes(fileNameA);
            byte[] bytesB = ModelSaveStorage.ReadAllBytes(fileNameB);
            ModelSaveStorage.Delete(fileNameA);
            ModelSaveStorage.Delete(fileNameB);
            if (bytesB != null)
            {
                ModelSaveStorage.WriteAllBytes(fileNameA, bytesB);
            }

            if (bytesA != null)
            {
                ModelSaveStorage.WriteAllBytes(fileNameB, bytesA);
            }
        }

        private static void AssignCanonicalFileNames(
            ModelSavePool pool,
            int slotIndex,
            ModelSaveSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            if (!slot.isUsed)
            {
                slot.glbFileName = string.Empty;
                slot.thumbnailFileName = string.Empty;
                return;
            }

            slot.glbFileName = ModelSavePoolSettings.GetGlbFileName(pool, slotIndex);
            slot.thumbnailFileName = ModelSavePoolSettings.GetThumbnailFileName(pool, slotIndex);
        }

        private void DeleteSlotInternal(ModelSavePool pool, int slotIndex)
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndex))
            {
                return;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            ModelSaveSlot slot = data.slots[slotIndex];

            if (!string.IsNullOrEmpty(slot.glbFileName))
            {
                ModelSaveStorage.Delete(slot.glbFileName);
            }

            if (!string.IsNullOrEmpty(slot.thumbnailFileName))
            {
                ModelSaveStorage.Delete(slot.thumbnailFileName);
            }

            string voxelFileName = ModelSavePoolSettings.GetVoxelFileName(pool, slotIndex);
            ModelSaveStorage.Delete(voxelFileName);
            DeleteTrainingProgressFile(pool, slotIndex);

            data.slots[slotIndex] = new ModelSaveSlot();
            WriteToFile(pool, data);
        }

        private static bool IsValidSlotIndex(ModelSavePool pool, int slotIndex)
        {
            return ModelSavePoolSettings.IsValidSlotIndex(pool, slotIndex);
        }

        /// <summary>
        /// 骨格解析で使用可能と判定した攻撃から保存登録用の攻撃を選ぶ
        /// 基本は星1運が良ければ星2星3は選ばない
        /// </summary>
        /// <param name="analyzer">部位分類器</param>
        /// <param name="bones">ボーン配列</param>
        /// <param name="count">選ぶ件数</param>
        /// <returns>保存登録用の攻撃</returns>
        public static List<MotionType> PickRandomAttacks(
            SkeletonPartAnalyzer analyzer,
            Transform[] bones,
            int count = AttackMotionCount)
        {
            return AttackMotionSelector.PickRandomAttacks(analyzer, bones, count);
        }

        private ClayModelSaveData LoadOrCreate(ModelSavePool pool)
        {
            string metadataFileName = ModelSavePoolSettings.GetMetadataFileName(pool);
            ClayModelSaveData data = null;

            string json = ModelSaveStorage.ReadAllText(metadataFileName);
            if (!string.IsNullOrEmpty(json))
            {
                data = JsonUtility.FromJson<ClayModelSaveData>(json);
            }

            if (data == null)
            {
                data = new ClayModelSaveData();
            }

            if (data.slots == null)
            {
                data.slots = new List<ModelSaveSlot>();
            }

            int slotCount = ModelSavePoolSettings.GetSlotCount(pool);
            while (data.slots.Count < slotCount)
            {
                data.slots.Add(new ModelSaveSlot());
            }

            while (data.slots.Count > slotCount)
            {
                data.slots.RemoveAt(data.slots.Count - 1);
            }

            MigrateLegacyWritableFiles(pool, data);
            EnsureEnemyStrengthStatuses(pool, data);
            return data;
        }

        private static void EnsureEnemyStrengthStatuses(ModelSavePool pool, ClayModelSaveData data)
        {
            if (pool != ModelSavePool.Enemy || data?.slots == null)
            {
                return;
            }

            for (int i = 0; i < data.slots.Count; i++)
            {
                ModelSaveSlot slot = data.slots[i];
                if (slot == null || !slot.isUsed)
                {
                    continue;
                }

                EnemyStrengthStatusCatalog.EnsureFromBase(slot);
            }
        }

        /// <summary>
        /// 旧形式の書込可能なモデルセーブを圧縮形式へ移行する
        /// 読み込み互換を維持しながら初回ロード時に一度だけ実行する
        /// </summary>
        /// <param name="pool">対象プール</param>
        /// <param name="data">読み込んだメタデータ</param>
        private static void MigrateLegacyWritableFiles(ModelSavePool pool, ClayModelSaveData data)
        {
            ModelSaveStorage.MigrateLegacyWritableFile(ModelSavePoolSettings.GetMetadataFileName(pool));
            for (int i = 0; i < data.slots.Count; i++)
            {
                ModelSaveSlot slot = data.slots[i];
                if (!slot.isUsed)
                {
                    continue;
                }

                ModelSaveStorage.MigrateLegacyWritableFile(slot.glbFileName);
                ModelSaveStorage.MigrateLegacyWritableFile(slot.thumbnailFileName);
                ModelSaveStorage.MigrateLegacyWritableFile(ModelSavePoolSettings.GetVoxelFileName(pool, i));
            }
        }

        private void WriteToFile(ModelSavePool pool, ClayModelSaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            ModelSaveStorage.WriteAllText(ModelSavePoolSettings.GetMetadataFileName(pool), json);
        }
    }
}
