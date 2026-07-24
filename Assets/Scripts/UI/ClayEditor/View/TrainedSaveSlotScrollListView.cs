using SaveData;
using SaveData.Interface;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 育成済みセーブスロット一覧
    /// 名前とサムネイルのみ表示し選択後の確認画面でステータスを見せる
    /// </summary>
    public sealed class TrainedSaveSlotScrollListView : MonoBehaviour
    {
        [SerializeField] private ModelSaveSlotScrollListView scrollList;

        private readonly List<UnityEngine.Object> runtimeThumbnailObjects = new List<UnityEngine.Object>();

        /// <summary>
        /// 内部のスクロール一覧
        /// </summary>
        public ModelSaveSlotScrollListView ScrollList => ResolveScrollList();

        /// <summary>
        /// クリック購読を初期化する
        /// </summary>
        /// <param name="slotSelectedHandler">選択時コールバック</param>
        public void Initialize(Action<int> slotSelectedHandler)
        {
            ModelSaveSlotScrollListView list = ResolveScrollList();
            if (list == null)
            {
                Debug.LogError(
                    "[TrainedSaveSlotScrollListView] scrollListが未設定ですHierarchyで接続してください",
                    this);
                return;
            }

            list.Initialize(slotSelectedHandler);
        }

        /// <summary>
        /// 育成済みスロット一覧を更新する
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="emptySlotLabel">空スロット文言</param>
        /// <param name="allowEmptySlotSelection">空スロット選択を許可するか</param>
        public void Refresh(
            IClayModelSaveService saveService,
            string emptySlotLabel,
            bool allowEmptySlotSelection)
        {
            ModelSaveSlotScrollListView list = ResolveScrollList();
            if (list == null)
            {
                Debug.LogError(
                    "[TrainedSaveSlotScrollListView] scrollListが未設定ですHierarchyで接続してください",
                    this);
                return;
            }

            ClearRuntimeThumbnails();
            TrainedSaveSlotListPresentation.RefreshSlots(
                list,
                saveService,
                emptySlotLabel,
                runtimeThumbnailObjects,
                allowEmptySlotSelection);
            list.RefreshHostLayout();
        }

        /// <summary>
        /// ホストレイアウトを再計算する
        /// </summary>
        public void RefreshHostLayout()
        {
            ResolveScrollList()?.RefreshHostLayout();
        }

        private ModelSaveSlotScrollListView ResolveScrollList()
        {
            if (scrollList == null)
            {
                scrollList = GetComponent<ModelSaveSlotScrollListView>();
            }

            return scrollList;
        }

        private void ClearRuntimeThumbnails()
        {
            for (int i = 0; i < runtimeThumbnailObjects.Count; i++)
            {
                UnityEngine.Object obj = runtimeThumbnailObjects[i];
                if (obj != null)
                {
                    Destroy(obj);
                }
            }

            runtimeThumbnailObjects.Clear();
        }

        private void OnDestroy()
        {
            ClearRuntimeThumbnails();
        }
    }
}
