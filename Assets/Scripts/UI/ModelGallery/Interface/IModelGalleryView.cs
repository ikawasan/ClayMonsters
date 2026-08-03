using SaveData.Interface;
using System;
using UI.ModelGallery.Data;
using UnityEngine;
using UnityEngine.Events;

namespace UI.ModelGallery.Interface
{
    /// <summary>
    /// 展示室UIのView契約
    /// Canvas.enabledで表示切替する
    /// </summary>
    public interface IModelGalleryView
    {
        /// <summary>
        /// 画面を表示する
        /// </summary>
        void Show();

        /// <summary>
        /// 画面を非表示にする
        /// </summary>
        void Hide();

        /// <summary>
        /// 投稿タブを表示する
        /// </summary>
        void ShowPostTab();

        /// <summary>
        /// 閲覧タブを表示する
        /// </summary>
        void ShowBrowseTab();

        /// <summary>
        /// 所持ポイント表示を更新する
        /// </summary>
        /// <param name="points">所持ポイント</param>
        void SetPoints(int points);

        /// <summary>
        /// 閲覧並び替えモードの選択表示を更新する
        /// </summary>
        /// <param name="sortMode">選択中モード</param>
        void SetBrowseSortMode(ModelGalleryBrowseSortMode sortMode);

        /// <summary>
        /// 投稿タブの育成前セーブスロット一覧を再描画する
        /// </summary>
        /// <param name="saveService">セーブスロット参照</param>
        void RefreshPostSlotList(IClayModelSaveService saveService);

        /// <summary>
        /// ダウンロード保存先スロット一覧を再描画する
        /// </summary>
        /// <param name="saveService">セーブスロット参照</param>
        void RefreshDownloadSlotList(IClayModelSaveService saveService);

        /// <summary>
        /// 閲覧一覧を描画する
        /// </summary>
        /// <param name="bindCell">セル番号を受け取り描画する</param>
        /// <param name="visibleCount">表示するセル数</param>
        void BindBrowseItems(Action<int> bindCell, int visibleCount);

        /// <summary>
        /// 閲覧セルへ表示内容を設定する
        /// </summary>
        /// <param name="cellIndex">セル番号</param>
        /// <param name="title">タイトル</param>
        /// <param name="favoriteCount">お気に入り数</param>
        /// <param name="isFavorited">自分がお気に入り中か</param>
        /// <param name="thumbnail">プレビュー</param>
        /// <param name="isSelected">選択中か</param>
        void SetBrowseItemCell(
            int cellIndex,
            string title,
            int favoriteCount,
            bool isFavorited,
            Texture2D thumbnail,
            bool isSelected);

        /// <summary>
        /// ポイント不足ウィンドウを表示する
        /// </summary>
        void ShowPointsInsufficient();

        /// <summary>
        /// ポイント不足ウィンドウを閉じる
        /// </summary>
        void HidePointsInsufficient();

        /// <summary>
        /// ダウンロード保存先スロット選択を表示する
        /// </summary>
        void ShowDownloadSlotSelect();

        /// <summary>
        /// ダウンロード保存先スロット選択を閉じる
        /// </summary>
        void HideDownloadSlotSelect();

        /// <summary>
        /// 投稿確認ウィンドウを表示する
        /// </summary>
        /// <param name="modelName">モデル名</param>
        /// <param name="thumbnail">サムネイル</param>
        void ShowPostConfirm(string modelName, Texture2D thumbnail);

        /// <summary>
        /// 投稿確認ウィンドウを閉じる
        /// </summary>
        void HidePostConfirm();

        /// <summary>
        /// ダウンロード保存確認ウィンドウを表示する
        /// </summary>
        /// <param name="modelName">保存するモデル名</param>
        /// <param name="isOverwrite">上書き確認か</param>
        /// <param name="thumbnail">サムネイル</param>
        void ShowDownloadConfirm(string modelName, bool isOverwrite, Texture2D thumbnail);

        /// <summary>
        /// ダウンロード保存確認ウィンドウを閉じる
        /// </summary>
        void HideDownloadConfirm();

        /// <summary>
        /// 閉じるボタン押下を購読する
        /// </summary>
        IDisposable SubscribeCloseButtonClick(UnityAction action);

        /// <summary>
        /// 投稿タブボタン押下を購読する
        /// </summary>
        IDisposable SubscribePostTabButtonClick(UnityAction action);

        /// <summary>
        /// 閲覧タブボタン押下を購読する
        /// </summary>
        IDisposable SubscribeBrowseTabButtonClick(UnityAction action);

        /// <summary>
        /// 投稿確認の投稿するボタン押下を購読する
        /// </summary>
        IDisposable SubscribePostConfirmPublishButtonClick(UnityAction action);

        /// <summary>
        /// 投稿確認の閉じるボタン押下を購読する
        /// </summary>
        IDisposable SubscribePostConfirmCloseButtonClick(UnityAction action);

        /// <summary>
        /// ダウンロード保存確認の保存するボタン押下を購読する
        /// </summary>
        IDisposable SubscribeDownloadConfirmSaveButtonClick(UnityAction action);

        /// <summary>
        /// ダウンロード保存確認の閉じるボタン押下を購読する
        /// </summary>
        IDisposable SubscribeDownloadConfirmCloseButtonClick(UnityAction action);

        /// <summary>
        /// ランダム表示ボタン押下を購読する
        /// </summary>
        IDisposable SubscribeRandomSortButtonClick(UnityAction action);

        /// <summary>
        /// 月間ランキングボタン押下を購読する
        /// </summary>
        IDisposable SubscribeMonthlyRankingButtonClick(UnityAction action);

        /// <summary>
        /// 総合ランキングボタン押下を購読する
        /// </summary>
        IDisposable SubscribeOverallRankingButtonClick(UnityAction action);

        /// <summary>
        /// 閲覧ランダム更新ボタン押下を購読する
        /// </summary>
        IDisposable SubscribeBrowseRefreshButtonClick(UnityAction action);

        /// <summary>
        /// 投稿元スロット選択を購読する
        /// </summary>
        /// <param name="action">選択されたスロット番号</param>
        IDisposable SubscribePostSlotSelected(UnityAction<int> action);

        /// <summary>
        /// ダウンロード保存先スロット選択を購読する
        /// </summary>
        /// <param name="action">選択されたスロット番号</param>
        IDisposable SubscribeDownloadSlotSelected(UnityAction<int> action);

        /// <summary>
        /// 閲覧セル選択を購読する
        /// </summary>
        IDisposable SubscribeBrowseItemSelected(UnityAction<int> action);

        /// <summary>
        /// 閲覧お気に入り押下を購読する
        /// </summary>
        IDisposable SubscribeBrowseFavoriteClicked(UnityAction<int> action);

        /// <summary>
        /// ポイント不足ウィンドウの閉じるを購読する
        /// </summary>
        IDisposable SubscribePointsInsufficientCloseButtonClick(UnityAction action);

        /// <summary>
        /// ダウンロード保存先選択の閉じるを購読する
        /// </summary>
        IDisposable SubscribeDownloadSlotSelectCloseButtonClick(UnityAction action);

        /// <summary>
        /// 閲覧セル数を返す
        /// </summary>
        int BrowseItemCellCount { get; }
    }
}
