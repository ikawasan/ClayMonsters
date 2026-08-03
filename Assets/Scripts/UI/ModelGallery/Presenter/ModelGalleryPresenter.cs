using Cysharp.Threading.Tasks;
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

        private bool isSetup;
        private bool isBusy;
        private int selectedPostSlotIndex = -1;
        private int pendingDownloadItemIndex = -1;
        private int pendingDownloadDestinationSlotIndex = -1;
        private ModelGalleryBrowseSortMode browseSortMode = ModelGalleryBrowseSortMode.Random;
        private IReadOnlyList<ModelGalleryItemSummary> browseItems = Array.Empty<ModelGalleryItemSummary>();
        private CancellationTokenSource lifetimeCts;
        private IDisposable pointsSubscription;

        /// <summary>
        /// 依存を注入する
        /// </summary>
        [Inject]
        public ModelGalleryPresenter(
            IModelGalleryView view,
            IModelGalleryService galleryService,
            IClayModelSaveService saveService,
            IPointsService pointsService)
        {
            this.view = view;
            this.galleryService = galleryService;
            this.saveService = saveService;
            this.pointsService = pointsService;
        }

        /// <inheritdoc />
        public void Setup()
        {
            if (isSetup)
            {
                return;
            }

            lifetimeCts = new CancellationTokenSource();
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
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ModelGalleryPresenter] 保存確認プレビュー読込失敗: {exception.Message}");
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
            if (isBusy)
            {
                return;
            }

            isBusy = true;
            try
            {
                browseItems = await galleryService.QueryAsync(browseSortMode, GetToken());
                view.SetBrowseSortMode(browseSortMode);
                RefreshBrowseListVisual();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ModelGalleryPresenter] 閲覧取得に失敗: {exception.Message}");
            }
            finally
            {
                isBusy = false;
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
                // BindBrowseCellの同期続きでnull上書きされないよう1フレーム空ける
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
                    return;
                }

                item.isFavoritedByMe = favorited.Value;
                item.favoriteCount = Mathf.Max(0, item.favoriteCount + (favorited.Value ? 1 : -1));
                Texture2D preview = await galleryService.LoadPreviewAsync(item.itemId, GetToken());
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
                Debug.LogError($"[ModelGalleryPresenter] お気に入り失敗: {exception.Message}");
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
            try
            {
                string itemId = await galleryService.PublishAsync(
                    selectedPostSlotIndex,
                    slot.modelName,
                    GetToken());
                if (string.IsNullOrEmpty(itemId))
                {
                    return;
                }

                selectedPostSlotIndex = -1;
                view.HidePostConfirm();
                RefreshPostSlots();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError($"[ModelGalleryPresenter] 投稿失敗: {exception.Message}");
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
                    return;
                }

                pendingDownloadItemIndex = -1;
                pendingDownloadDestinationSlotIndex = -1;
                view.HideDownloadConfirm();
                view.HideDownloadSlotSelect();
            }
            catch (OperationCanceledException)
            {
                pointsService.AddPoints(cost);
            }
            catch (Exception exception)
            {
                pointsService.AddPoints(cost);
                Debug.LogError($"[ModelGalleryPresenter] ダウンロード失敗: {exception.Message}");
            }
            finally
            {
                isBusy = false;
                view.SetPoints(pointsService.Points);
            }
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
    }
}
