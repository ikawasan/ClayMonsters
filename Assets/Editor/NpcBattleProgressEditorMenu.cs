using SaveData;
using UnityEditor;
using UnityEngine;

namespace ClayMonsters.Editor
{
    /// <summary>
    /// CPU戦進捗セーブのEditor操作
    /// </summary>
    public static class NpcBattleProgressEditorMenu
    {
        private const string MenuPath = "ClayMonsters/セーブ/CPU戦進捗を初期化";

        /// <summary>
        /// CPU戦のアンロック進捗を初期化する
        /// </summary>
        [MenuItem(MenuPath)]
        public static void ResetNpcBattleProgress()
        {
            if (!EditorUtility.DisplayDialog(
                    "CPU戦進捗の初期化",
                    "CPU戦の敵種類・強さアンロック進捗を初期状態に戻しますか？\n"
                    + "(敵スロット0の「弱い」のみ)",
                    "初期化する",
                    "キャンセル"))
            {
                return;
            }

            SaveDataManager.Update(data =>
            {
                data.NpcBattleProgress = new NpcBattleProgressSaveData
                {
                    unlockedEnemyCount = NpcBattleProgressRules.InitialUnlockedEnemyCount,
                    slotUnlockedStrengthCounts = NpcBattleProgressRules.CreateInitialSlotStrengthCounts()
                };
            });

            Debug.Log(
                "[NpcBattleProgressEditorMenu] CPU戦進捗を初期化しました"
                + $" path={Application.persistentDataPath}/savedata.json");
        }
    }
}
