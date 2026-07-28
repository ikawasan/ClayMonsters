using SaveData;
using SaveData.Service;
using UnityEditor;
using UnityEngine;

namespace ClayMonsters.Editor
{
    /// <summary>
    /// 敵セーブスロットの順番入れ替えWizard
    /// </summary>
    public sealed class EnemySlotSwapWizard : ScriptableWizard
    {
        private const string MenuPath = "ClayMonsters/セーブ/敵スロットを入れ替え";

        [Tooltip("入れ替える一方のスロット番号(0始まり)")]
        public int slotIndexA;

        [Tooltip("入れ替えるもう一方のスロット番号(0始まり)")]
        public int slotIndexB = 1;

        [Tooltip("CPU戦のスロット別強さ進捗も一緒に入れ替える")]
        public bool swapNpcBattleStrengthProgress = true;

        [Tooltip("StreamingAssetsのModelSaveへも反映する")]
        public bool mirrorToStreamingAssets = true;

        /// <summary>
        /// 敵スロット入れ替えWizardを開く
        /// </summary>
        [MenuItem(MenuPath)]
        public static void OpenWizard()
        {
            DisplayWizard<EnemySlotSwapWizard>(
                "敵スロットを入れ替え",
                "入れ替える",
                "閉じる");
        }

        private void OnWizardCreate()
        {
            if (!TrySwap())
            {
                return;
            }

            EditorUtility.DisplayDialog(
                "敵スロット入れ替え",
                $"スロット{slotIndexA}とスロット{slotIndexB}を入れ替えました",
                "OK");
        }

        private void OnWizardOtherButton()
        {
            Close();
        }

        private bool TrySwap()
        {
            if (!ModelSavePoolSettings.IsValidSlotIndex(ModelSavePool.Enemy, slotIndexA)
                || !ModelSavePoolSettings.IsValidSlotIndex(ModelSavePool.Enemy, slotIndexB))
            {
                EditorUtility.DisplayDialog(
                    "敵スロット入れ替え",
                    $"スロット番号は0〜{ModelSavePoolSettings.SlotCount - 1}です",
                    "OK");
                return false;
            }

            if (slotIndexA == slotIndexB)
            {
                EditorUtility.DisplayDialog(
                    "敵スロット入れ替え",
                    "同じスロット同士は入れ替えできません",
                    "OK");
                return false;
            }

            var saveService = new ClayModelSaveService(new ClayModelGltfExporter());
            if (!saveService.SwapSlots(ModelSavePool.Enemy, slotIndexA, slotIndexB))
            {
                EditorUtility.DisplayDialog(
                    "敵スロット入れ替え",
                    "入れ替えに失敗しましたConsoleを確認してください",
                    "OK");
                return false;
            }

            if (swapNpcBattleStrengthProgress)
            {
                SwapNpcBattleStrengthProgress(slotIndexA, slotIndexB);
            }

            if (mirrorToStreamingAssets)
            {
                MirrorEnemySlotFilesToStreaming(slotIndexA);
                MirrorEnemySlotFilesToStreaming(slotIndexB);
                MirrorEnemyMetadataToStreaming();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                "[EnemySlotSwapWizard] 敵スロットを入れ替えました"
                + $" a={slotIndexA} b={slotIndexB}"
                + $" progress={swapNpcBattleStrengthProgress}"
                + $" streaming={mirrorToStreamingAssets}");
            return true;
        }

        private static void SwapNpcBattleStrengthProgress(int slotIndexA, int slotIndexB)
        {
            SaveDataManager.Update(data =>
            {
                data.NpcBattleProgress ??= new NpcBattleProgressSaveData();
                int maxEnemy = NpcBattleProgressRules.MaxEnemyCount;
                if (data.NpcBattleProgress.slotUnlockedStrengthCounts == null
                    || data.NpcBattleProgress.slotUnlockedStrengthCounts.Length != maxEnemy)
                {
                    data.NpcBattleProgress.slotUnlockedStrengthCounts =
                        NpcBattleProgressRules.CreateInitialSlotStrengthCounts();
                }

                int[] counts = data.NpcBattleProgress.slotUnlockedStrengthCounts;
                if (slotIndexA < 0
                    || slotIndexB < 0
                    || slotIndexA >= counts.Length
                    || slotIndexB >= counts.Length)
                {
                    return;
                }

                (counts[slotIndexA], counts[slotIndexB]) = (counts[slotIndexB], counts[slotIndexA]);
            });
        }

        private static void MirrorEnemySlotFilesToStreaming(int slotIndex)
        {
            ModelSaveStorage.MirrorWritableToStreaming(
                ModelSavePoolSettings.GetGlbFileName(ModelSavePool.Enemy, slotIndex));
            ModelSaveStorage.MirrorWritableToStreaming(
                ModelSavePoolSettings.GetThumbnailFileName(ModelSavePool.Enemy, slotIndex));
            ModelSaveStorage.MirrorWritableToStreaming(
                ModelSavePoolSettings.GetVoxelFileName(ModelSavePool.Enemy, slotIndex));
            ModelSaveStorage.MirrorWritableToStreaming(
                ModelSavePoolSettings.GetTrainingProgressFileName(ModelSavePool.Enemy, slotIndex));
        }

        private static void MirrorEnemyMetadataToStreaming()
        {
            ModelSaveStorage.MirrorWritableToStreaming(
                ModelSavePoolSettings.GetMetadataFileName(ModelSavePool.Enemy));
        }
    }
}
