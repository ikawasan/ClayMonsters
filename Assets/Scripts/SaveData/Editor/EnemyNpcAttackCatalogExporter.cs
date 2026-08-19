#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SaveData.Editor
{
    /// <summary>
    /// 同梱敵カタログの強さ段階技を抽選しStreamingAssetsへ書き出す
    /// </summary>
    public static class EnemyNpcAttackCatalogExporter
    {
        /// <summary>
        /// 同梱EnemyModelSaveからNPC対戦用の段階技を再抽選して書き出す
        /// </summary>
        public static void ExportFromBundledCatalog()
        {
            ClayModelSaveData data = LoadBundledEnemyCatalog();
            if (data?.slots == null)
            {
                Debug.LogError("[EnemyNpcAttackCatalogExporter] 敵カタログ読込に失敗しました");
                EditorApplication.Exit(1);
                return;
            }

            int usedCount = 0;
            int attackRegenerated = 0;
            int statusRegenerated = 0;
            for (int i = 0; i < data.slots.Count; i++)
            {
                ModelSaveSlot slot = data.slots[i];
                if (slot == null || !slot.isUsed)
                {
                    continue;
                }

                usedCount++;
                int balanceBefore = slot.enemyStrengthBalanceVersion;
                EnemyStrengthStatusCatalog.EnsureFromBase(slot);
                if (slot.enemyStrengthBalanceVersion != balanceBefore
                    || !slot.hasEnemyStrengthStatuses)
                {
                    statusRegenerated++;
                }

                slot.hasEnemyStrengthAttacks = false;
                slot.enemyStrengthAttackVersion = 0;
                if (EnemyStrengthAttackCatalog.EnsureFromSaved(slot, i))
                {
                    attackRegenerated++;
                }
            }

            string metadataFileName = ModelSavePoolSettings.GetMetadataFileName(ModelSavePool.Enemy);
            string json = JsonUtility.ToJson(data, true);
            ModelSaveStorage.WriteAllText(metadataFileName, json);
            ModelSaveStorage.MirrorEnemyPoolToStreaming(data);
            AssetDatabase.Refresh();

            Debug.Log(
                "[EnemyNpcAttackCatalogExporter] 書き出し完了"
                    + $" used={usedCount}"
                    + $" statusUpdated={statusRegenerated}"
                    + $" attacksUpdated={attackRegenerated}"
                    + $" attackVersion={EnemyStrengthAttackCatalog.AttackDrawVersion}"
                    + $" balanceVersion={EnemyStrengthStatusCatalog.BalanceVersion}");
            EditorApplication.Exit(0);
        }

        private static ClayModelSaveData LoadBundledEnemyCatalog()
        {
            string metadataFileName = ModelSavePoolSettings.GetMetadataFileName(ModelSavePool.Enemy);
            string rawPath = Path.Combine(ModelSaveStorage.StreamingRoot, metadataFileName);
            string compressedPath = rawPath + ".gz";
            string path = File.Exists(compressedPath) ? compressedPath : rawPath;
            if (!File.Exists(path))
            {
                Debug.LogError(
                    "[EnemyNpcAttackCatalogExporter] 同梱敵メタがありません"
                        + $" path={path}");
                return null;
            }

            string json;
            using (Stream stream = OpenStoredReadStream(path))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                json = reader.ReadToEnd();
            }

            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            ClayModelSaveData data = JsonUtility.FromJson<ClayModelSaveData>(json);
            if (data?.slots == null)
            {
                return data;
            }

            int slotCount = ModelSavePoolSettings.GetSlotCount(ModelSavePool.Enemy);
            while (data.slots.Count < slotCount)
            {
                data.slots.Add(new ModelSaveSlot());
            }

            while (data.slots.Count > slotCount)
            {
                data.slots.RemoveAt(data.slots.Count - 1);
            }

            return data;
        }

        private static Stream OpenStoredReadStream(string path)
        {
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return path.EndsWith(".gz", System.StringComparison.OrdinalIgnoreCase)
                ? new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress)
                : fileStream;
        }
    }
}
#endif
