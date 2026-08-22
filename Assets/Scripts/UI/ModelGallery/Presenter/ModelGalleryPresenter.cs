using Cysharp.Threading.Tasks;
using Localization;
using R3;
using SaveData;
using SaveData.Interface;
using System;
using System.Collections.Generic;
using System.Threading;
using UI.ModelGallery.Data;
using UI.ModelGallery.Interface;
using UnityEngine;
using VContainer;

namespace UI.ModelGallery.Presenter
{
    /// <summary>
    /// 展示室の投稿閲覧ダウンロード操作を調停する
    /// </summary>
    public sealed class ModelGalleryPresenter : IModelGalleryPresenter
    {
        private readonly IModelGalleryView view;
        private readonly IModelGalleryService galleryService;
        private readonly IClayModelSaveService saveService;
        private readonly IPointsService pointsService;
        private readonly IModelGalleryUserMessage userMessage;

        private bool isSetup;
        private bool isBusy;
        private int selectedPostSlotIndex = -1;
        private int pendingDownloadItemIndex = -1;
        private int pendingDownloadDestinationSlotIndex = -1;
        private ModelGalleryBrowseSortMode browseSortMode = ModelGalleryBrowseSortMode.Random;
        private IReadOnlyList<ModelGalleryItemSummary> browseItems = Array.Empty<ModelGalleryItemSummary>();
        private CancellationTokenSource lifetimeCts;
        private IDisposable pointsSubscription;
        private int browseQuerySerial;

        /// <summary>
        /// 依存を注入する
        /// </summary>
        [Inject]
        public ModelGalleryPresenter(
            IModelGalleryView view,
            IModelGalleryService galleryService,
            IClayModelSaveService saveService,
            IPointsService pointsService,
            IModelGalleryUserMessage userMessage)
        {
            this.view = view;
            this.galleryService = galleryService;
            this.saveService = saveService;
            this.pointsService = pointsService;
            this.userMessage = userMessage;
        }

        /// <inheritdoc />
        public void Setup()
        {
            if (isSetup)
            {
                return;
            }

            view.SubscribeCloseButtonClick(Hide);
            view.SubscribePostTabButtonClick(OnClickPostTab);
            view.SubscribeBrowseTabButtonClick(OnClickBrowseTab);
            view.SubscribePostConfirmPublishButtonClick(OnClickPostConfirmPublish);
            view.SubscribePostConfirmCloseButtonClick(OnClickPostConfirmClose);
            view.SubscribeDownloadConfirmSaveButtonClick(OnClickDownloadConfirmSave);
            view.SubscribeDownloadConfirmCloseButtonClick(OnClickDownloadConfirmClose);
            view.SubscribeRandomSortButtonClick(() => ChangeBrowseSortMode(ModelGalleryBrowseSortMode.Random));
            view.SubscribeMonthlyRankingButtonClick(() => ChangeBrowseSortMode(ModelGalleryBrowseSortMode.MonthlyRanking));
            view.SubscribeOverallRankingButtonClick(() => ChangeBrowseSortMode(ModelGalleryBrowseSortMode.OverallRanking));
            view.SubscribeLatestSortButtonClick(() => ChangeBrowseSortMode(ModelGalleryBrowseSortMode.Latest));
            view.SubscribeBrowseRefreshButtonClick(OnClickBrowseRefresh);
            view.SubscribePostSlotSelected(OnPostSlotSelected);
            view.SubscribeDownloadSlotSelected(OnDownloadSlotSelected);
            view.SubscribeBrowseItemSelected(OnBrowseItemSelected);
            view.SubscribeBrowseFavoriteClicked(OnBrowseFavoriteClicked);
            view.SubscribePointsInsufficientCloseButtonClick(OnClickPointsInsufficientClose);
            view.SubscribeDownloadSlotSelectCloseButtonClick(OnClickDownloadSlotSelectClose);
            pointsSubscription = pointsService.PointsObservable.Subscribe(points => view.SetPoints(points));
            isSetup = true;
        }

        /// <inheritdoc />
        public void Show()
        {
            Setup();
            ResetLifetimeToken();
            selectedPostSlotIndex = -1;
            pendingDownloadItemIndex = -1;
            pendingDownloadDestinationSlotIndex = -1;
            browseSortMode = ModelGalleryBrowseSortMode.Random;
            pointsService.Reload();
            view.SetPoints(pointsService.Points);
            view.SetBrowseSortMode(browseSortMode);
            view.HidePointsInsufficient();
            view.HideDownloadSlotSelect();
            view.HidePostConfirm();
            view.HideDownloadConfirm();
            view.Show();
            view.ShowPostTab();
            RefreshPostSlots();
        }

        /// <inheritdoc />
        public void Hide()
        {
            CancelLifetimeToken();
            pendingDownloadItemIndex = -1;
            pendingDownloadDestinationSlotIndex = -1;
            view.HidePointsInsufficient();
            view.HideDownloadSlotSelect();
            view.HidePostConfirm();
            view.HideDownloadConfirm();
            view.Hide();
        }

        private void OnClickPostTab()
        {
            pendingDownloadItemIndex = -1;
            pendingDownloadDestinationSlotIndex = -1;
            view.HidePointsInsufficient();
            view.HideDownloadSlotSelect();
            view.HidePostConfirm();
            view.HideDownloadConfirm();
            view.ShowPostTab();
            RefreshPostSlots();
        }

        private void OnClickBrowseTab()
        {
            pendingDownloadItemIndex = -1;
            pendingDownloadDestinationSlotIndex = -1;
            view.HidePointsInsufficient();
            view.HideDownloadSlotSelect();
            view.HidePostConfirm();
            view.HideDownloadConfirm();
            view.ShowBrowseTab();
            view.SetBrowseSortMode(browseSortMode);
            RefreshBrowseItemsAsync().Forget();
        }

        private void OnClickPostConfirmPublish()
        {
            PublishAsync().Forget();
        }

        private void OnClickPostConfirmClose()
        {
            if (isBusy)
            {
                return;
            }

            selectedPostSlotIndex = -1;
            view.HidePostConfirm();
        }

        private void OnClickDownloadConfirmSave()
        {
            if (pendingDownloadDestinationSlotIndex < 0)
            {
                return;
            }

            DownloadAsync(pendingDownloadDestinationSlotIndex).Forget();
        }

        private void OnClickDownloadConfirmClose()
        {
            if (isBusy)
            {
                return;
            }

            pendingDownloadDestinationSlotIndex = -1;
            view.HideDownloadConfirm();
        }

        private void ChangeBrowseSortMode(ModelGalleryBrowseSortMode sortMode)
        {
            browseSortMode = sortMode;
            view.SetBrowseSortMode(browseSortMode);
            RefreshBrowseItemsAsync().Forget();
        }

        private void OnClickBrowseRefresh()
        {
            if (browseSortMode != ModelGalleryBrowseSortMode.Random)
            {
                return;
            }

            RefreshBrowseItemsAsync().Forget();
        }

        private void OnPostSlotSelected(int slotIndex)
        {
            if (slotIndex < 0 || isBusy)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
            if (slot == null)
            {
                selectedPostSlotIndex = -1;
                view.HidePostConfirm();
                return;
            }

            selectedPostSlotIndex = slotIndex;
            Texture2D thumbnail = saveService.LoadThumbnail(ModelSavePool.Player, slotIndex);
            view.ShowPostConfirm(slot.modelName, thumbnail);
        }

        private void OnBrowseItemSelected(int cellIndex)
        {
            int itemIndex = ResolveBrowseItemIndex(cellIndex);
            if (itemIndex < 0 || isBusy)
            {
                return;
            }

            if (pointsService.Points < ModelGalleryDownloadSettings.DownloadCostPoints)
            {
                pendingDownloadItemIndex = -1;
                pendingDownloadDestinationSlotIndex = -1;
                view.HideDownloadConfirm();
                view.HideDownloadSlotSelect();
                view.ShowPointsInsufficient();
                return;
            }

            pendingDownloadItemIndex = itemIndex;
            pendingDownloadDestinationSlotIndex = -1;
            view.HidePointsInsufficient();
            view.HideDownloadConfirm();
            view.HidePostConfirm();
            view.RefreshDownloadSlotList(saveService);
            view.ShowDownloadSlotSelect();
        }

        private void OnBrowseFavoriteClicked(int cellIndex)
        {
            ToggleFavoriteAsync(cellIndex).Forget();
        }

        private void OnClickPointsInsufficientClose()
        {
            view.HidePointsInsufficient();
        }

        private void OnClickDownloadSlotSelectClose()
        {
            pendingDownloadItemIndex = -1;
            pendingDownloadDestinationSlotIndex = -1;
            view.HideDownloadConfirm();
            view.HideDownloadSlotSelect();
        }

        private void OnDownloadSlotSelected(int slotIndex)
        {
            if (slotIndex < 0 || isBusy)
            {
                return;
            }

            if (pendingDownloadItemIndex < 0 || pendingDownloadItemIndex >= browseItems.Count)
            {
                return;
            }

            OpenDownloadConfirmAsync(slotIndex).Forget();
        }

        private async UniTaskVoid OpenDownloadConfirmAsync(int destinationSlotIndex)
        {
            if (pendingDownloadItemIndex < 0 || pendingDownloadItemIndex >= browseItems.Count)
            {
                return;
            }

            ModelGalleryItemSummary item = browseItems[pendingDownloadItemIndex];
            ModelSaveSlot existing = saveService.GetSlot(ModelSavePool.Player, destinationSlotIndex);
            bool isOverwrite = existing != null;

            Texture2D preview = null;
            try
            {
                preview = await galleryService.LoadPreviewAsync(item.itemId, GetToken());
                GetToken().ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                if (preview != null)
                {
                    UnityEngine.Object.Destroy(preview);
                }

                return;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ModelGalleryPresenter] 保存確認プレビュー読込失敗: {exception.Message}");
            }

            if (pendingDownloadItemIndex < 0 || pendingDownloadItemIndex >= browseItems.Count)
            {
                if (preview != null)
                {
                    UnityEngine.Object.Destroy(preview);
                }

                return;
            }

            pendingDownloadDestinationSlotIndex = destinationSlotIndex;
            view.HidePostConfirm();
            view.ShowDownloadConfirm(item.title, isOverwrite, preview);
        }

        private void RefreshPostSlots()
        {
            view.RefreshPostSlotList(saveService);
        }

        private async UniTaskVoid RefreshBrowseItemsAsync()
        {
            int serial = ++browseQuerySerial;
            ModelGalleryBrowseSortMode requestedSortMode = browseSortMode;
            try
            {
                IReadOnlyList<ModelGalleryItemSummary> queried =
                    await galleryService.QueryAsync(requestedSortMode, GetToken());
                if (serial != browseQuerySerial)
                {
                    return;
                }

                GetToken().ThrowIfCancellationRequested();
                browseItems = queried;
                view.SetBrowseSortMode(browseSortMode);
                RefreshBrowseListVisual();
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
                if (serial != browseQuerySerial)
                {
                    return;
                }

                browseItems = Array.Empty<ModelGalleryItemSummary>();
                RefreshBrowseListVisual();
                ShowUserMessage(
                    GameTextKeys.ModelGalleryErrorTimedOut,
                    "通信がタイムアウトしました時間をおいて再試行してください");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "SteamUnavailable")
            {
                if (serial != browseQuerySerial)
                {
                    return;
                }

                browseItems = Array.Empty<ModelGalleryItemSummary>();
                RefreshBrowseListVisual();
                ShowUserMessage(
                    GameTextKeys.ModelGalleryErrorSteamUnavailable,
                    "Steamに接続できませんSteamを起動してログインしてください");
            }
            catch (Exception exception)
            {
                if (serial != browseQuerySerial)
                {
                    return;
                }

                browseItems = Array.Empty<ModelGalleryItemSummary>();
                RefreshBrowseListVisual();
                Debug.LogError($"[ModelGalleryPresenter] 閲覧取得に失敗: {exception.Message}");
                ShowUserMessage(
                    GameTextKeys.ModelGalleryErrorQueryFailed,
                    "展示室の一覧取得に失敗しました");
            }
        }

        private void RefreshBrowseListVisual()
        {
            int visibleCount = Mathf.Min(browseItems.Count, view.BrowseItemCellCount);
            view.BindBrowseItems(BindBrowseCell, visibleCount);
        }

        private void BindBrowseCell(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= browseItems.Count)
            {
                return;
            }

            ModelGalleryItemSummary item = browseItems[cellIndex];
            view.SetBrowseItemCell(
                cellIndex,
                item.title,
                item.favoriteCount,
                item.isFavoritedByMe,
                null,
                false);
            LoadBrowsePreviewAsync(cellIndex, item.itemId).Forget();
        }

        private async UniTaskVoid LoadBrowsePreviewAsync(int cellIndex, string itemId)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, GetToken());
                Texture2D preview = await galleryService.LoadPreviewAsync(itemId, GetToken());
                if (preview == null)
                {
                    return;
                }

                if (cellIndex < 0 || cellIndex >= browseItems.Count)
                {
                    UnityEngine.Object.Destroy(preview);
                    return;
                }

                if (browseItems[cellIndex].itemId != itemId)
                {
                    UnityEngine.Object.Destroy(preview);
                    return;
                }

                ModelGalleryItemSummary item = browseItems[cellIndex];
                view.SetBrowseItemCell(
                    cellIndex,
                    item.title,
                    item.favoriteCount,
                    item.isFavoritedByMe,
                    preview,
                    false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ModelGalleryPresenter] プレビュー読込失敗: {exception.Message}");
            }
        }

        private async UniTaskVoid ToggleFavoriteAsync(int cellIndex)
        {
            int itemIndex = ResolveBrowseItemIndex(cellIndex);
            if (itemIndex < 0 || isBusy)
            {
                return;
            }

            isBusy = true;
            try
            {
                ModelGalleryItemSummary item = browseItems[itemIndex];
                bool? favorited = await galleryService.ToggleFavoriteAsync(item.itemId, GetToken());
                if (favorited == null)
                {
                    ShowUserMessage(
                        GameTextKeys.ModelGalleryErrorFavoriteFailed,
                        "お気に入りの更新に失敗しました");
                    return;
                }

                item.isFavoritedByMe = favorited.Value;
                item.favoriteCount = Mathf.Max(0, item.favoriteCount + (favorited.Value ? 1 : -1));
                Texture2D preview = await galleryService.LoadPreviewAsync(item.itemId, GetToken());
                if (GetToken().IsCancellationRequested)
                {
                    if (preview != null)
                    {
                        UnityEngine.Object.Destroy(preview);
                    }

                    return;
                }

                view.SetBrowseItemCell(
                    cellIndex,
                    item.title,
                    item.favoriteCount,
                    item.isFavoritedByMe,
                    preview,
                    false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
                ShowUserMessage(
                    GameTextKeys.ModelGalleryErrorTimedOut,
                    "通信がタイムアウトしました時間をおいて再試行してください");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ModelGalleryPresenter] お気に入り失敗: {exception.Message}");
                ShowUserMessage(
                    GameTextKeys.ModelGalleryErrorFavoriteFailed,
                    "お気に入りの更新に失敗しました");
            }
            finally
            {
                isBusy = false;
            }
        }

        private async UniTaskVoid PublishAsync()
        {
            if (isBusy || selectedPostSlotIndex < 0)
            {
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, selectedPostSlotIndex);
            if (slot == null)
            {
                return;
            }

            isBusy = true;
            view.ShowPostConfirmPublishing();
            try
            {
                ModelGalleryPublishResult result = await galleryService.PublishAsync(
                    selectedPostSlotIndex,
                    slot.modelName,
                    GetToken());
                GetToken().ThrowIfCancellationRequested();
                switch (result.Status)
                {
                    case ModelGalleryOperationStatus.Success:
                        selectedPostSlotIndex = -1;
                        RefreshPostSlots();
                        if (result.NeedsLegalAgreement)
                        {
                            view.ShowPostConfirmResult(
                                GameTextKeys.ModelGalleryPublishAcceptedNeedsLegal,
                                "投稿は受け付けましたSteamワークショップ利用規約への同意後に公開されます");
                        }
                        else
                        {
                            view.ShowPostConfirmResult(
                                GameTextKeys.ModelGalleryPublishSuccess,
                                "投稿が完了しました");
                        }

                        break;
                    case ModelGalleryOperationStatus.NeedsWorkshopLegalAgreement:
                        view.ShowPostConfirmResult(
                            GameTextKeys.ModelGalleryErrorNeedsLegalAgreement,
                            "Steamワークショップ利用規約への同意が必要ですオーバーレイで同意後に再投稿してください");
                        break;
                    case ModelGalleryOperationStatus.SteamUnavailable:
                        view.ShowPostConfirmResult(
                            GameTextKeys.ModelGalleryErrorSteamUnavailable,
                            "Steamに接続できませんSteamを起動してログインしてください");
                        break;
                    case ModelGalleryOperationStatus.TimedOut:
                        view.ShowPostConfirmResult(
                            GameTextKeys.ModelGalleryErrorPublishTimedOut,
                            "通信がタイムアウトしました投稿状況が不明なため時間をおいて一覧を確認してください");
                        break;
                    default:
                        view.ShowPostConfirmResult(
                            GameTextKeys.ModelGalleryErrorPublishFailed,
                            "展示室への投稿に失敗しました");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                view.HidePostConfirm();
            }
            catch (TimeoutException)
            {
                view.ShowPostConfirmResult(
                    GameTextKeys.ModelGalleryErrorPublishTimedOut,
                    "通信がタイムアウトしました投稿状況が不明なため時間をおいて一覧を確認してください");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ModelGalleryPresenter] 投稿失敗: {exception.Message}");
                view.ShowPostConfirmResult(
                    GameTextKeys.ModelGalleryErrorPublishFailed,
                    "展示室への投稿に失敗しました");
            }
            finally
            {
                isBusy = false;
            }
        }

        private async UniTaskVoid DownloadAsync(int destinationSlotIndex)
        {
            if (isBusy)
            {
                return;
            }

            if (pendingDownloadItemIndex < 0 || pendingDownloadItemIndex >= browseItems.Count)
            {
                return;
            }

            int cost = ModelGalleryDownloadSettings.DownloadCostPoints;
            if (pointsService.Points < cost)
            {
                view.HideDownloadConfirm();
                view.HideDownloadSlotSelect();
                view.ShowPointsInsufficient();
                return;
            }

            if (!pointsService.TrySpendPoints(cost))
            {
                view.HideDownloadConfirm();
                view.HideDownloadSlotSelect();
                view.ShowPointsInsufficient();
                return;
            }

            isBusy = true;
            view.ShowDownloadConfirmSaving();
            try
            {
                ModelGalleryItemSummary item = browseItems[pendingDownloadItemIndex];
                bool success = await galleryService.DownloadAsync(
                    item.itemId,
                    destinationSlotIndex,
                    GetToken());
                if (!success)
                {
                    pointsService.AddPoints(cost);
                    if (!GetToken().IsCancellationRequested)
                    {
                        view.ShowDownloadConfirmResult(
                            GameTextKeys.ModelGalleryErrorDownloadFailed,
                            "ダウンロードに失敗しましたポイントは返還しました");
                    }

                    return;
                }

                // 保存成功後はキャンセルでもポイントを返還しない
                if (GetToken().IsCancellationRequested)
                {
                    return;
                }

                pendingDownloadItemIndex = -1;
                pendingDownloadDestinationSlotIndex = -1;
                view.HideDownloadSlotSelect();
                view.ShowDownloadConfirmResult(
                    GameTextKeys.ModelGallerySaveSuccess,
                    "保存が完了しました");
            }
            catch (OperationCanceledException)
            {
                pointsService.AddPoints(cost);
                view.HideDownloadConfirm();
            }
            catch (TimeoutException)
            {
                pointsService.AddPoints(cost);
                view.ShowDownloadConfirmResult(
                    GameTextKeys.ModelGalleryErrorTimedOut,
                    "通信がタイムアウトしました時間をおいて再試行してください");
            }
            catch (Exception exception)
            {
                pointsService.AddPoints(cost);
                Debug.LogError($"[ModelGalleryPresenter] ダウンロード失敗: {exception.Message}");
                view.ShowDownloadConfirmResult(
                    GameTextKeys.ModelGalleryErrorDownloadFailed,
                    "ダウンロードに失敗しましたポイントは返還しました");
            }
            finally
            {
                isBusy = false;
                view.SetPoints(pointsService.Points);
            }
        }

        private void ShowUserMessage(string key, string fallback)
        {
            if (userMessage == null)
            {
                Debug.LogError($"[ModelGalleryPresenter] userMessage未配線: {fallback}");
                return;
            }

            userMessage.ShowLocalized(key, fallback);
        }

        private int ResolveBrowseItemIndex(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= browseItems.Count)
            {
                return -1;
            }

            return cellIndex;
        }

        private CancellationToken GetToken()
        {
            return lifetimeCts != null ? lifetimeCts.Token : CancellationToken.None;
        }

        private void ResetLifetimeToken()
        {
            CancelLifetimeToken();
            lifetimeCts = new CancellationTokenSource();
        }

        private void CancelLifetimeToken()
        {
            if (lifetimeCts == null)
            {
                return;
            }

            lifetimeCts.Cancel();
            lifetimeCts.Dispose();
            lifetimeCts = null;
        }
    }
}
