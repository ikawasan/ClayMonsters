using SaveData;
using SaveData.Interface;
using System.Collections.Generic;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 育成済みセーブスロット一覧の表示契約
    /// 5列×10行の名前とサムネイルボタン選択後の確認画面でステータスを表示する
    /// </summary>
    public static class TrainedSaveSlotListPresentation
    {
        /// <summary>
        /// 育成済み一覧の行表示モード
        /// </summary>
        public const ModelSaveSlotListContentMode ListContentMode =
            ModelSaveSlotListContentMode.NameAndThumbnail;

        /// <summary>
        /// プールに応じた一覧表示モードを返す
        /// </summary>
        /// <param name="pool">セーブプール</param>
        public static ModelSaveSlotListContentMode ResolveContentMode(ModelSavePool pool)
        {
            return pool == ModelSavePool.TrainedPlayer
                ? ListContentMode
                : ModelSaveSlotListContentMode.Full;
        }

        /// <summary>
        /// 育成済みスロット一覧を更新する
        /// </summary>
        /// <param name="gridView">5×10グリッド</param>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="emptySlotLabel">空スロット文言</param>
        /// <param name="allowEmptySlotSelection">空スロット選択を許可するか</param>
        public static void RefreshGrid(
            TrainedSaveSlotGridView gridView,
            IClayModelSaveService saveService,
            string emptySlotLabel,
            bool allowEmptySlotSelection)
        {
            if (gridView == null)
            {
                return;
            }

            gridView.Refresh(saveService, emptySlotLabel, allowEmptySlotSelection);
        }

        /// <summary>
        /// 育成済みスロット一覧を更新する
        /// </summary>
        /// <param name="scrollList">スクロール一覧</param>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="emptySlotLabel">空スロット文言</param>
        /// <param name="runtimeThumbnailObjects">実行時サムネイル破棄用</param>
        /// <param name="allowEmptySlotSelection">空スロット選択を許可するか</param>
        public static void RefreshSlots(
            ModelSaveSlotScrollListView scrollList,
            IClayModelSaveService saveService,
            string emptySlotLabel,
            IList<UnityEngine.Object> runtimeThumbnailObjects,
            bool allowEmptySlotSelection)
        {
            if (scrollList == null)
            {
                return;
            }

            scrollList.RefreshSlots(
                ModelSavePool.TrainedPlayer,
                saveService,
                emptySlotLabel,
                runtimeThumbnailObjects,
                allowEmptySlotSelection,
                ListContentMode);
        }
    }
}
