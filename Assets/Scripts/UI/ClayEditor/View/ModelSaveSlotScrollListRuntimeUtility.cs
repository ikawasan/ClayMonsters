using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 配置済みModelSaveSlotScrollListの参照を解決する
    /// 実行時にUI生成やHierarchy差し替えは行わない
    /// </summary>
    public static class ModelSaveSlotScrollListRuntimeUtility
    {
        /// <summary>
        /// スロット一覧ホストを配置済みHierarchyから解決する
        /// </summary>
        /// <param name="ownerRoot">スロット一覧の親Transform</param>
        /// <param name="currentScrollList">現在の参照</param>
        /// <returns>利用可能なスロット一覧View</returns>
        public static ModelSaveSlotScrollListView EnsureHostUnderTransform(
            Transform ownerRoot,
            ModelSaveSlotScrollListView currentScrollList)
        {
            if (ownerRoot == null)
            {
                return currentScrollList;
            }

            ModelSaveSlotScrollListView resolvedScrollList =
                ResolveScrollList(ownerRoot, currentScrollList);
            if (resolvedScrollList == null)
            {
                Debug.LogError(
                    "[ModelSaveSlotScrollListRuntimeUtility] ModelSaveSlotScrollListが未配置です。"
                        + "SaveSlotCanvasまたはLoadSlotCanvas配下にプレハブインスタンスがあるか確認してください",
                    ownerRoot);
                return null;
            }

            if (resolvedScrollList.NeedsReferenceResolve())
            {
                resolvedScrollList.ResolveSerializedReferencesFromHierarchy();
            }

            return resolvedScrollList;
        }

        private static ModelSaveSlotScrollListView ResolveScrollList(
            Transform ownerRoot,
            ModelSaveSlotScrollListView currentScrollList)
        {
            ModelSaveSlotScrollListView hierarchyScrollList =
                ownerRoot.GetComponentInChildren<ModelSaveSlotScrollListView>(true);
            if (hierarchyScrollList != null)
            {
                return hierarchyScrollList;
            }

            if (IsAliveScrollListUnderOwner(currentScrollList, ownerRoot))
            {
                return currentScrollList;
            }

            return null;
        }

        private static bool IsAliveScrollListUnderOwner(
            ModelSaveSlotScrollListView scrollList,
            Transform ownerRoot)
        {
            if (!scrollList)
            {
                return false;
            }

            Transform host = scrollList.transform;
            if (host == null)
            {
                return false;
            }

            return host.IsChildOf(ownerRoot);
        }
    }
}
