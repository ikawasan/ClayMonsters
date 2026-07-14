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
        public const int AttackMotionCount = 4;

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
            if (!IsValidSlotIndex(slotIndex))
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
                Debug.LogWarning("[ClayModelSaveService] 敵保存はUnityエディタでのみ利用できます");
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

            string thumbnailFileName = null;
            if (thumbnailPng != null && thumbnailPng.Length > 0)
            {
                thumbnailFileName = ModelSavePoolSettings.GetThumbnailFileName(pool, slotIndex);
                string thumbnailFilePath = Path.Combine(Application.persistentDataPath, thumbnailFileName);
                File.WriteAllBytes(thumbnailFilePath, thumbnailPng);
            }

            List<MotionType> attacks = attackMotions != null
                ? new List<MotionType>(attackMotions)
                : new List<MotionType>();

            ClayModelSaveData data = LoadOrCreate(pool);
            data.slots[slotIndex] = new ModelSaveSlot
            {
                isUsed = true,
                modelName = modelName,
                status = status,
                glbFileName = glbFileName,
                thumbnailFileName = thumbnailFileName,
                attackMotions = attacks
            };

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
            if (!IsValidSlotIndex(slotIndex))
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
            for (int i = 0; i < ClayModelSaveData.SlotCount; i++)
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
            if (!IsValidSlotIndex(slotIndex) || status == null)
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
                speed = status.speed
            };
            data.slots[slotIndex] = slot;
            WriteToFile(pool, data);
            return true;
        }

        /// <inheritdoc />
        public bool UpdateSlotAttackMotions(ModelSavePool pool, int slotIndex, IReadOnlyList<MotionType> attackMotions)
        {
            if (!IsValidSlotIndex(slotIndex) || attackMotions == null || attackMotions.Count == 0)
            {
                return false;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            ModelSaveSlot slot = data.slots[slotIndex];
            if (!slot.isUsed)
            {
                return false;
            }

            slot.attackMotions = new List<MotionType>();
            int count = Mathf.Min(attackMotions.Count, AttackMotionCount);
            for (int i = 0; i < count; i++)
            {
                slot.attackMotions.Add(attackMotions[i]);
            }

            while (slot.attackMotions.Count < AttackMotionCount)
            {
                slot.attackMotions.Add(MotionType.Punch);
            }

            data.slots[slotIndex] = slot;
            WriteToFile(pool, data);
            return true;
        }

        /// <inheritdoc />
        public TrainingSlotProgress GetTrainingProgress(ModelSavePool pool, int slotIndex)
        {
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
            if (!IsValidSlotIndex(slotIndex) || progress == null || !progress.inProgress)
            {
                return false;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            ModelSaveSlot slot = data.slots[slotIndex];
            if (!slot.isUsed)
            {
                return false;
            }

            slot.trainingProgress = CloneTrainingProgress(progress);
            data.slots[slotIndex] = slot;
            WriteToFile(pool, data);
            return true;
        }

        /// <inheritdoc />
        public void ClearTrainingProgress(ModelSavePool pool, int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            ModelSaveSlot slot = data.slots[slotIndex];
            if (slot.trainingProgress == null)
            {
                return;
            }

            slot.trainingProgress = null;
            data.slots[slotIndex] = slot;
            WriteToFile(pool, data);
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
            if (!IsValidSlotIndex(trainedSlotIndex)
                || !IsValidSlotIndex(playerSlotIndex)
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
            ModelStatus statusCopy = CloneStatus(status);
            List<MotionType> attacks = NormalizeAttackMotions(attackMotions);

            if (runtimeRenderer != null && boneRoot != null)
            {
                byte[] thumbnailPng = ReadPlayerThumbnailBytes(playerSlot);
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
            if (!IsValidSlotIndex(trainedSlotIndex)
                || !IsValidSlotIndex(playerSlotIndex)
                || status == null)
            {
                return false;
            }

            ModelSaveSlot playerSlot = ResolvePlayerSourceSlot(playerSlotIndex);
            if (playerSlot == null)
            {
                return false;
            }

            string sourceGlbPath = Path.Combine(Application.persistentDataPath, playerSlot.glbFileName);
            if (!File.Exists(sourceGlbPath))
            {
                Debug.LogError($"[ClayModelSaveService] 未育成glbが見つかりません: {sourceGlbPath}");
                return false;
            }

            string trainedGlbFileName = ModelSavePoolSettings.GetGlbFileName(ModelSavePool.TrainedPlayer, trainedSlotIndex);
            string trainedGlbPath = Path.Combine(Application.persistentDataPath, trainedGlbFileName);
            File.Copy(sourceGlbPath, trainedGlbPath, true);

            string trainedThumbnailFileName = null;
            if (!string.IsNullOrEmpty(playerSlot.thumbnailFileName))
            {
                string sourceThumbnailPath = Path.Combine(Application.persistentDataPath, playerSlot.thumbnailFileName);
                if (File.Exists(sourceThumbnailPath))
                {
                    trainedThumbnailFileName = ModelSavePoolSettings.GetThumbnailFileName(
                        ModelSavePool.TrainedPlayer,
                        trainedSlotIndex);
                    string trainedThumbnailPath = Path.Combine(Application.persistentDataPath, trainedThumbnailFileName);
                    File.Copy(sourceThumbnailPath, trainedThumbnailPath, true);
                }
            }

            string resolvedModelName = string.IsNullOrEmpty(modelName)
                ? playerSlot.modelName
                : modelName;
            List<MotionType> attacks = NormalizeAttackMotions(attackMotions);
            ClayModelSaveData data = LoadOrCreate(ModelSavePool.TrainedPlayer);
            data.slots[trainedSlotIndex] = new ModelSaveSlot
            {
                isUsed = true,
                modelName = resolvedModelName,
                status = CloneStatus(status),
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

        private static byte[] ReadPlayerThumbnailBytes(ModelSaveSlot playerSlot)
        {
            if (playerSlot == null || string.IsNullOrEmpty(playerSlot.thumbnailFileName))
            {
                return null;
            }

            return ModelSaveStorage.ReadAllBytes(playerSlot.thumbnailFileName);
        }

        private static ModelStatus CloneStatus(ModelStatus status)
        {
            if (status == null)
            {
                return new ModelStatus();
            }

            return new ModelStatus
            {
                hp = status.hp,
                attack = status.attack,
                defense = status.defense,
                speed = status.speed
            };
        }

        private static List<MotionType> NormalizeAttackMotions(IReadOnlyList<MotionType> attackMotions)
        {
            List<MotionType> attacks = attackMotions != null
                ? new List<MotionType>(attackMotions)
                : new List<MotionType>();
            int count = Mathf.Min(attacks.Count, AttackMotionCount);
            if (count < attacks.Count)
            {
                attacks.RemoveRange(count, attacks.Count - count);
            }

            while (attacks.Count < AttackMotionCount)
            {
                attacks.Add(MotionType.Punch);
            }

            return attacks;
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
                status = source.status != null
                    ? new ModelStatus
                    {
                        hp = source.status.hp,
                        attack = source.status.attack,
                        defense = source.status.defense,
                        speed = source.status.speed
                    }
                    : new ModelStatus(),
                attackMotions = source.attackMotions != null
                    ? new List<MotionType>(source.attackMotions)
                    : new List<MotionType>()
            };
            return clone;
        }

        /// <inheritdoc />
        public void DeleteSlot(ModelSavePool pool, int slotIndex)
        {
            DeleteSlotInternal(pool, slotIndex);
            if (pool == ModelSavePool.Player)
            {
                DeleteSlotInternal(ModelSavePool.TrainedPlayer, slotIndex);
            }
        }

        private void DeleteSlotInternal(ModelSavePool pool, int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return;
            }

            ClayModelSaveData data = LoadOrCreate(pool);
            ModelSaveSlot slot = data.slots[slotIndex];

            if (!string.IsNullOrEmpty(slot.glbFileName))
            {
                string glbPath = Path.Combine(Application.persistentDataPath, slot.glbFileName);
                if (File.Exists(glbPath))
                {
                    File.Delete(glbPath);
                }
            }

            if (!string.IsNullOrEmpty(slot.thumbnailFileName))
            {
                string thumbnailPath = Path.Combine(Application.persistentDataPath, slot.thumbnailFileName);
                if (File.Exists(thumbnailPath))
                {
                    File.Delete(thumbnailPath);
                }
            }

            string voxelFileName = ModelSavePoolSettings.GetVoxelFileName(pool, slotIndex);
            string voxelPath = Path.Combine(Application.persistentDataPath, voxelFileName);
            if (File.Exists(voxelPath))
            {
                File.Delete(voxelPath);
            }

            data.slots[slotIndex] = new ModelSaveSlot();
            WriteToFile(pool, data);
        }

        private static bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < ModelSavePoolSettings.SlotCount;
        }

        private static string GetSaveFilePath(ModelSavePool pool)
        {
            return ModelSaveStorage.GetWritablePath(ModelSavePoolSettings.GetMetadataFileName(pool));
        }

        /// <summary>
        /// 骨格解析で使用可能と判定した攻撃から保存登録用の攻撃を選ぶ
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

            while (data.slots.Count < ModelSavePoolSettings.SlotCount)
            {
                data.slots.Add(new ModelSaveSlot());
            }

            while (data.slots.Count > ModelSavePoolSettings.SlotCount)
            {
                data.slots.RemoveAt(data.slots.Count - 1);
            }

            return data;
        }

        private void WriteToFile(ModelSavePool pool, ClayModelSaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetSaveFilePath(pool), json);
        }
    }
}
