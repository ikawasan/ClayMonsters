using SaveData;
using UI.ClayEditor.View;
using UnityEditor;
using UnityEngine;

namespace ClayMonsters.Editor
{
    /// <summary>
    /// モデルスロット一覧プレハブの行数を未育成・敵スロット数へ揃える
    /// </summary>
    public static class ModelSaveSlotScrollListExpandMenu
    {
        private const string PrefabPath = "Assets/Resources/UI/ModelSaveSlotScrollList.prefab";
        private const string MenuPath = "ClayMonsters/セーブ/モデルスロット一覧を25行へ拡張";

        /// <summary>
        /// ModelSaveSlotScrollListの行を未育成・敵スロット数まで複製する
        /// </summary>
        [MenuItem(MenuPath)]
        public static void ExpandToPlayerSlotCount()
        {
            int targetCount = Mathf.Max(
                ModelSavePoolSettings.PlayerSlotCount,
                ModelSavePoolSettings.EnemySlotCount);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "スロット一覧拡張",
                    $"プレハブを開けませんでした\n{PrefabPath}",
                    "OK");
                return;
            }

            try
            {
                ModelSaveSlotRowElementRefs[] existing =
                    prefabRoot.GetComponentsInChildren<ModelSaveSlotRowElementRefs>(true);
                if (existing.Length == 0)
                {
                    EditorUtility.DisplayDialog(
                        "スロット一覧拡張",
                        "複製元のスロット行が見つかりません",
                        "OK");
                    return;
                }

                if (existing.Length >= targetCount)
                {
                    EditorUtility.DisplayDialog(
                        "スロット一覧拡張",
                        $"すでに{existing.Length}行あります(必要数{targetCount})",
                        "OK");
                    return;
                }

                Transform template = existing[existing.Length - 1].transform.parent;
                if (template == null)
                {
                    template = existing[existing.Length - 1].transform;
                }

                Transform content = template.parent;
                if (content == null)
                {
                    EditorUtility.DisplayDialog(
                        "スロット一覧拡張",
                        "Content親が見つかりません",
                        "OK");
                    return;
                }

                int created = 0;
                for (int i = existing.Length; i < targetCount; i++)
                {
                    GameObject clone = Object.Instantiate(template.gameObject, content);
                    clone.name = $"{template.name} ({i})";
                    clone.transform.SetSiblingIndex(i);
                    created++;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(
                    "スロット一覧拡張",
                    $"{created}行を追加して合計{targetCount}行にしました",
                    "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
