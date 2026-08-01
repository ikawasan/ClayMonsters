using System;
using System.IO;
using UnityEngine;

namespace SaveData
{
    /// <summary>
    /// オプションと進捗を含む共通セーブの読み書き
    /// </summary>
    public static class SaveDataManager
    {
        private static readonly string SavePath =
            Path.Combine(Application.persistentDataPath, "savedata.json");

        /// <summary>
        /// セーブを読み込む
        /// </summary>
        public static SaveData Load()
        {
            if (!File.Exists(SavePath))
            {
                return CreateDefault();
            }

            string raw = File.ReadAllText(SavePath);
            if (!SaveDataProtection.TryOpenText(raw, out string json, out bool wasLegacyPlain))
            {
                Debug.LogError(
                    "[SaveDataManager] セーブの改ざんまたは破損を検知したため初期化します");
                BackupCorruptSave();
                return CreateDefault();
            }

            SaveData loaded = JsonUtility.FromJson<SaveData>(json);
            if (loaded == null)
            {
                Debug.LogError(
                    "[SaveDataManager] セーブの解析に失敗したため初期化します");
                BackupCorruptSave();
                return CreateDefault();
            }

            EnsureDefaults(loaded);
            if (wasLegacyPlain)
            {
                // 旧平文を暗号化形式へ移行する
                Save(loaded);
            }

            return loaded;
        }

        /// <summary>
        /// セーブを書き込む
        /// </summary>
        /// <param name="data">保存データ</param>
        public static void Save(SaveData data)
        {
            if (data == null)
            {
                return;
            }

            EnsureDefaults(data);
            string json = JsonUtility.ToJson(data, true);
            string sealedText = SaveDataProtection.SealText(json);
            File.WriteAllText(SavePath, sealedText);
        }

        /// <summary>
        /// 最新セーブを読み込み変更を適用して保存する
        /// </summary>
        /// <param name="mutate">変更処理</param>
        public static void Update(Action<SaveData> mutate)
        {
            if (mutate == null)
            {
                return;
            }

            SaveData data = Load();
            mutate(data);
            Save(data);
        }

        private static void BackupCorruptSave()
        {
            try
            {
                if (!File.Exists(SavePath))
                {
                    return;
                }

                string backupPath = SavePath + ".corrupt";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Move(SavePath, backupPath);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[SaveDataManager] 破損セーブの退避に失敗しました error={exception.Message}");
            }
        }

        private static SaveData CreateDefault()
        {
            var data = new SaveData();
            EnsureDefaults(data);
            return data;
        }

        private static void EnsureDefaults(SaveData data)
        {
            if (data.VideoOptionData == null)
            {
                data.VideoOptionData = new VideoOptionSaveData();
            }

            if (data.SoundOptionData == null)
            {
                data.SoundOptionData = new SoundOptionSaveData();
            }

            if (data.NpcBattleProgress == null)
            {
                data.NpcBattleProgress = new NpcBattleProgressSaveData
                {
                    unlockedEnemyCount = NpcBattleProgressRules.InitialUnlockedEnemyCount,
                    slotUnlockedStrengthCounts = NpcBattleProgressRules.CreateInitialSlotStrengthCounts()
                };
            }

            data.Points = BattlePointsRules.ClampHeldPoints(data.Points);

            if (data.SkillTree == null)
            {
                data.SkillTree = new SkillTreeSaveData();
            }

            data.SkillTree.nodeIds ??= Array.Empty<int>();
            data.SkillTree.levels ??= Array.Empty<int>();

            data.NpcBattleProgress.unlockedEnemyCount =
                NpcBattleProgressRules.ClampUnlockedEnemyCount(
                    data.NpcBattleProgress.unlockedEnemyCount);

            int maxEnemy = NpcBattleProgressRules.MaxEnemyCount;
            if (data.NpcBattleProgress.slotUnlockedStrengthCounts == null
                || data.NpcBattleProgress.slotUnlockedStrengthCounts.Length != maxEnemy)
            {
                int[] resized = new int[maxEnemy];
                int[] source = data.NpcBattleProgress.slotUnlockedStrengthCounts;
                if (source != null)
                {
                    int copy = source.Length < maxEnemy ? source.Length : maxEnemy;
                    for (int i = 0; i < copy; i++)
                    {
                        resized[i] = source[i];
                    }
                }

                data.NpcBattleProgress.slotUnlockedStrengthCounts = resized;
            }

            for (int i = 0; i < maxEnemy; i++)
            {
                bool unlocked = i < data.NpcBattleProgress.unlockedEnemyCount;
                data.NpcBattleProgress.slotUnlockedStrengthCounts[i] =
                    NpcBattleProgressRules.ClampSlotStrengthCount(
                        data.NpcBattleProgress.slotUnlockedStrengthCounts[i],
                        unlocked);
            }

            for (int i = 0; i < data.NpcBattleProgress.unlockedEnemyCount && i < maxEnemy; i++)
            {
                if (data.NpcBattleProgress.slotUnlockedStrengthCounts[i]
                    < NpcBattleProgressRules.InitialSlotStrengthCount)
                {
                    data.NpcBattleProgress.slotUnlockedStrengthCounts[i] =
                        NpcBattleProgressRules.InitialSlotStrengthCount;
                }
            }
        }
    }
}
