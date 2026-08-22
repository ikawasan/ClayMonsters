using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData;
using SaveData.Interface;
using SteamIntegration;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UI.ModelGallery.Data;
using UI.ModelGallery.Interface;
using UnityEngine;
using UnityEngine.Networking;

namespace UI.ModelGallery.Service
{
    /// <summary>
    /// SteamWorkshopのUGCで展示室の投稿閲覧ダウンロードを行う
    /// </summary>
    public sealed class SteamModelGalleryService : IModelGalleryService
    {
        private const string MetaFileName = "package.json";
        private const string ModelFileName = "model.glb";
        private const string PreviewFileName = "preview.png";
        private const string UploadFolderName = "SteamGalleryUpload";
        private const string WorkshopTag = "ModelGallery";
        private const string WorkshopLegalAgreementUrl =
            "https://steamcommunity.com/sharedfiles/workshoplegalagreement";
        private const int RandomDisplayCount = 30;
        private const int QueryPageSizeHint = 50;
        private const int MaxFavoritePages = 10;
        private const int OverallRankingPages = 2;
        // Steamに真の乱数クエリは無いので全件ページングしてからシャッフルする上限
        private const int MaxRandomQueryPages = 20;
        private const uint InstallFolderBufferSize = 1024;
        private const int DefaultSteamTimeoutMs = 45000;
        private const int PublishSteamTimeoutMs = 120000;
        private const int DownloadSteamTimeoutMs = 180000;

        private readonly IClayModelSaveService saveService;
        private readonly Dictionary<string, string> previewUrlByItemId = new();
        private readonly System.Random random = new();

        /// <summary>
        /// 依存を注入する
        /// </summary>
        /// <param name="saveService">モデルセーブサービス</param>
        public SteamModelGalleryService(IClayModelSaveService saveService)
        {
            this.saveService = saveService;
        }

        /// <inheritdoc />
        public async UniTask<ModelGalleryPublishResult> PublishAsync(
            int sourceSlotIndex,
            string title,
            CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            if (!EnsureSteamReady())
            {
                return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.SteamUnavailable);
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, sourceSlotIndex);
            if (slot == null)
            {
                Debug.LogError($"[SteamModelGalleryService] 未育成スロット{sourceSlotIndex}が空です");
                return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.Failed);
            }

            byte[] glbBytes = ModelSaveStorage.ReadAllBytes(slot.glbFileName);
            if (glbBytes == null || glbBytes.Length == 0)
            {
                Debug.LogError($"[SteamModelGalleryService] glbが読めません: {slot.glbFileName}");
                return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.Failed);
            }

            string resolvedTitle = string.IsNullOrWhiteSpace(title)
                ? (string.IsNullOrEmpty(slot.modelName) ? $"Slot{sourceSlotIndex}" : slot.modelName)
                : title.Trim();

            PublishedFileId_t createdFileId = PublishedFileId_t.Invalid;
            bool cleanupCreatedItemOnCancel = false;
            try
            {
                AppId_t appId = GetAppId();
                SteamCallOutcome<CreateItemResult_t> createOutcome =
                    await AwaitCallResultAsync<CreateItemResult_t>(
                        SteamUGC.CreateItem(appId, EWorkshopFileType.k_EWorkshopFileTypeCommunity),
                        PublishSteamTimeoutMs,
                        cancellationToken);
                if (createOutcome.TimedOut)
                {
                    Debug.LogWarning(
                        "[SteamModelGalleryService] CreateItemがタイムアウトしました空アイテムが残っている可能性があります");
                    return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.TimedOut);
                }

                if (createOutcome.IoFailure || createOutcome.Result.m_eResult != EResult.k_EResultOK)
                {
                    Debug.LogError(
                        $"[SteamModelGalleryService] CreateItem失敗: failed={createOutcome.IoFailure} result={createOutcome.Result.m_eResult}");
                    return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.Failed);
                }

                if (createOutcome.Result.m_bUserNeedsToAcceptWorkshopLegalAgreement)
                {
                    OpenWorkshopLegalAgreement();
                    await TryDeleteWorkshopItemAsync(
                        createOutcome.Result.m_nPublishedFileId,
                        CancellationToken.None);
                    return ModelGalleryPublishResult.FromStatus(
                        ModelGalleryOperationStatus.NeedsWorkshopLegalAgreement);
                }

                PublishedFileId_t fileId = createOutcome.Result.m_nPublishedFileId;
                createdFileId = fileId;
                cleanupCreatedItemOnCancel = true;
                string contentFolder = PrepareUploadFolder(fileId, slot, resolvedTitle, glbBytes);
                if (string.IsNullOrEmpty(contentFolder))
                {
                    cleanupCreatedItemOnCancel = false;
                    await TryDeleteWorkshopItemAsync(fileId, CancellationToken.None);
                    return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.Failed);
                }

                string previewPath = Path.Combine(contentFolder, PreviewFileName);
                UGCUpdateHandle_t updateHandle = SteamUGC.StartItemUpdate(appId, fileId);
                SteamUGC.SetItemTitle(updateHandle, resolvedTitle);
                SteamUGC.SetItemDescription(updateHandle, resolvedTitle);
                SteamUGC.SetItemVisibility(
                    updateHandle,
                    ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic);
                SteamUGC.SetItemTags(updateHandle, new List<string> { WorkshopTag });
                SteamUGC.SetItemContent(updateHandle, contentFolder);
                if (File.Exists(previewPath))
                {
                    SteamUGC.SetItemPreview(updateHandle, previewPath);
                }

                SteamCallOutcome<SubmitItemUpdateResult_t> submitOutcome =
                    await AwaitCallResultAsync<SubmitItemUpdateResult_t>(
                        SteamUGC.SubmitItemUpdate(updateHandle, "Publish gallery model"),
                        PublishSteamTimeoutMs,
                        cancellationToken);
                TryDeleteDirectory(contentFolder);

                if (submitOutcome.TimedOut)
                {
                    // タイムアウト時は削除しない成功済みの可能性がある
                    cleanupCreatedItemOnCancel = false;
                    Debug.LogWarning(
                        $"[SteamModelGalleryService] SubmitItemUpdateがタイムアウトしましたアイテムが残っている可能性があります: {fileId.m_PublishedFileId}");
                    return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.TimedOut);
                }

                if (submitOutcome.IoFailure || submitOutcome.Result.m_eResult != EResult.k_EResultOK)
                {
                    cleanupCreatedItemOnCancel = false;
                    Debug.LogError(
                        $"[SteamModelGalleryService] SubmitItemUpdate失敗: failed={submitOutcome.IoFailure} result={submitOutcome.Result.m_eResult}");
                    await TryDeleteWorkshopItemAsync(fileId, CancellationToken.None);
                    return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.Failed);
                }

                cleanupCreatedItemOnCancel = false;
                if (submitOutcome.Result.m_bUserNeedsToAcceptWorkshopLegalAgreement)
                {
                    OpenWorkshopLegalAgreement();
                    return ModelGalleryPublishResult.Success(
                        fileId.m_PublishedFileId.ToString(),
                        needsLegalAgreement: true);
                }

                return ModelGalleryPublishResult.Success(fileId.m_PublishedFileId.ToString());
            }
            catch (OperationCanceledException)
            {
                if (cleanupCreatedItemOnCancel)
                {
                    await TryDeleteWorkshopItemAsync(createdFileId, CancellationToken.None);
                }

                throw;
            }
        }

        /// <inheritdoc />
        public async UniTask<IReadOnlyList<ModelGalleryItemSummary>> QueryAsync(
            ModelGalleryBrowseSortMode sortMode,
            CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            previewUrlByItemId.Clear();
            if (!EnsureSteamReady())
            {
                throw new InvalidOperationException("SteamUnavailable");
            }

            HashSet<ulong> favoritedIds = await LoadFavoritedPublishedFileIdsAsync(cancellationToken);
            EUGCQuery queryType = ResolveQueryType(sortMode);
            int pageCount = ResolveQueryPageCount(sortMode);
            var working = new List<ModelGalleryItemSummary>();
            var seenIds = new HashSet<string>();

            for (uint page = 1; page <= (uint)pageCount; page++)
            {
                QueryPageResult pageResult;
                try
                {
                    pageResult = await QuerySinglePageAsync(
                        queryType,
                        sortMode,
                        page,
                        favoritedIds,
                        cancellationToken);
                }
                catch (Exception exception) when (page > 1 && !(exception is OperationCanceledException))
                {
                    Debug.LogWarning(
                        $"[SteamModelGalleryService] 追加ページ取得を打ち切ります: page={page} {exception.Message}");
                    break;
                }

                for (int i = 0; i < pageResult.Items.Count; i++)
                {
                    ModelGalleryItemSummary item = pageResult.Items[i];
                    if (item == null || string.IsNullOrEmpty(item.itemId) || !seenIds.Add(item.itemId))
                    {
                        continue;
                    }

                    working.Add(item);
                }

                if (sortMode == ModelGalleryBrowseSortMode.Random
                    && ShouldStopRandomPaging(page, pageResult))
                {
                    break;
                }
            }

            ApplySortMode(working, sortMode);
            return working;
        }

        /// <inheritdoc />
        public async UniTask<Texture2D> LoadPreviewAsync(string itemId, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            if (string.IsNullOrEmpty(itemId) || !EnsureSteamReady())
            {
                return null;
            }

            if (!previewUrlByItemId.TryGetValue(itemId, out string previewUrl)
                || string.IsNullOrEmpty(previewUrl))
            {
                previewUrl = await FetchPreviewUrlAsync(itemId, cancellationToken);
                if (string.IsNullOrEmpty(previewUrl))
                {
                    return null;
                }

                previewUrlByItemId[itemId] = previewUrl;
            }

            using UnityWebRequest request = UnityWebRequest.Get(previewUrl);
            await request.SendWebRequest()
                .WithCancellation(cancellationToken)
                .Timeout(TimeSpan.FromMilliseconds(DefaultSteamTimeoutMs));
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SteamModelGalleryService] プレビュー取得失敗: {request.error}");
                return null;
            }

            byte[] bytes = request.downloadHandler?.data;
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            return texture;
        }

        /// <inheritdoc />
        public async UniTask<bool?> ToggleFavoriteAsync(string itemId, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            if (string.IsNullOrEmpty(itemId) || !EnsureSteamReady())
            {
                return null;
            }

            if (!TryParsePublishedFileId(itemId, out PublishedFileId_t fileId))
            {
                Debug.LogError($"[SteamModelGalleryService] 不正なitemIdです: {itemId}");
                return null;
            }

            HashSet<ulong> favoritedIds = await LoadFavoritedPublishedFileIdsAsync(cancellationToken);
            bool currentlyFavorited = favoritedIds.Contains(fileId.m_PublishedFileId);
            AppId_t appId = GetAppId();
            SteamAPICall_t apiCall = currentlyFavorited
                ? SteamUGC.RemoveItemFromFavorites(appId, fileId)
                : SteamUGC.AddItemToFavorites(appId, fileId);

            SteamCallOutcome<UserFavoriteItemsListChanged_t> outcome =
                await AwaitCallResultAsync<UserFavoriteItemsListChanged_t>(
                    apiCall,
                    DefaultSteamTimeoutMs,
                    cancellationToken);
            if (outcome.TimedOut || outcome.IoFailure || outcome.Result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError(
                    $"[SteamModelGalleryService] お気に入り切替失敗: timedOut={outcome.TimedOut} failed={outcome.IoFailure} result={outcome.Result.m_eResult}");
                return null;
            }

            return !currentlyFavorited;
        }

        /// <inheritdoc />
        public async UniTask<bool> DownloadAsync(
            string itemId,
            int destinationSlotIndex,
            CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            if (string.IsNullOrEmpty(itemId) || !EnsureSteamReady())
            {
                return false;
            }

            if (!TryParsePublishedFileId(itemId, out PublishedFileId_t fileId))
            {
                Debug.LogError($"[SteamModelGalleryService] 不正なitemIdです: {itemId}");
                return false;
            }

            if (!await EnsureItemInstalledAsync(fileId, cancellationToken))
            {
                return false;
            }

            if (!SteamUGC.GetItemInstallInfo(
                    fileId,
                    out _,
                    out string folder,
                    InstallFolderBufferSize,
                    out _))
            {
                Debug.LogError($"[SteamModelGalleryService] インストール情報取得失敗: {itemId}");
                return false;
            }

            string metaPath = Path.Combine(folder, MetaFileName);
            string modelPath = Path.Combine(folder, ModelFileName);
            if (!File.Exists(metaPath) || !File.Exists(modelPath))
            {
                Debug.LogError($"[SteamModelGalleryService] パッケージが見つかりません: {folder}");
                return false;
            }

            ModelGalleryPackageMeta meta =
                JsonUtility.FromJson<ModelGalleryPackageMeta>(File.ReadAllText(metaPath));
            if (meta == null)
            {
                Debug.LogError($"[SteamModelGalleryService] package.jsonの解析に失敗しました: {itemId}");
                return false;
            }

            byte[] glbBytes = File.ReadAllBytes(modelPath);
            if (glbBytes == null || glbBytes.Length == 0)
            {
                Debug.LogError($"[SteamModelGalleryService] model.glbが空です: {itemId}");
                return false;
            }

            byte[] thumbnailPng = null;
            string previewPath = Path.Combine(folder, PreviewFileName);
            if (File.Exists(previewPath))
            {
                thumbnailPng = File.ReadAllBytes(previewPath);
            }

            return saveService.ImportUntrainedSlot(
                destinationSlotIndex,
                string.IsNullOrEmpty(meta.modelName) ? "GalleryMonster" : meta.modelName,
                ModelStatus.CloneOrDefault(meta.status),
                meta.attackMotions,
                glbBytes,
                thumbnailPng,
                overwrite: true);
        }

        private async UniTask<QueryPageResult> QuerySinglePageAsync(
            EUGCQuery queryType,
            ModelGalleryBrowseSortMode sortMode,
            uint page,
            HashSet<ulong> favoritedIds,
            CancellationToken cancellationToken)
        {
            AppId_t appId = GetAppId();
            UGCQueryHandle_t queryHandle = SteamUGC.CreateQueryAllUGCRequest(
                queryType,
                EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items,
                appId,
                appId,
                page);
            SteamUGC.AddRequiredTag(queryHandle, WorkshopTag);
            SteamUGC.SetAllowCachedResponse(queryHandle, 0);
            if (sortMode == ModelGalleryBrowseSortMode.MonthlyRanking)
            {
                SteamUGC.SetRankedByTrendDays(queryHandle, 30u);
            }

            SteamCallOutcome<SteamUGCQueryCompleted_t> completed =
                await AwaitCallResultAsync<SteamUGCQueryCompleted_t>(
                    SteamUGC.SendQueryUGCRequest(queryHandle),
                    DefaultSteamTimeoutMs,
                    cancellationToken);
            if (completed.TimedOut || completed.IoFailure || completed.Result.m_eResult != EResult.k_EResultOK)
            {
                SteamUGC.ReleaseQueryUGCRequest(queryHandle);
                Debug.LogError(
                    $"[SteamModelGalleryService] Query失敗: timedOut={completed.TimedOut} failed={completed.IoFailure} result={completed.Result.m_eResult}");
                if (completed.TimedOut)
                {
                    throw new TimeoutException("SteamUGC query timed out");
                }

                throw new InvalidOperationException("SteamUGC query failed");
            }

            uint returned = completed.Result.m_unNumResultsReturned;
            uint totalMatching = completed.Result.m_unTotalMatchingResults;
            var working = new List<ModelGalleryItemSummary>((int)returned);
            for (uint i = 0; i < returned; i++)
            {
                if (!SteamUGC.GetQueryUGCResult(queryHandle, i, out SteamUGCDetails_t details))
                {
                    continue;
                }

                if (details.m_eResult != EResult.k_EResultOK || details.m_bBanned)
                {
                    continue;
                }

                string itemId = details.m_nPublishedFileId.m_PublishedFileId.ToString();
                if (SteamUGC.GetQueryUGCPreviewURL(queryHandle, i, out string previewUrl, 1024)
                    && !string.IsNullOrEmpty(previewUrl))
                {
                    previewUrlByItemId[itemId] = previewUrl;
                }

                int favoriteCount = 0;
                if (SteamUGC.GetQueryUGCStatistic(
                        queryHandle,
                        i,
                        EItemStatistic.k_EItemStatistic_NumFavorites,
                        out ulong favoriteStat))
                {
                    favoriteCount = favoriteStat > int.MaxValue ? int.MaxValue : (int)favoriteStat;
                }

                working.Add(new ModelGalleryItemSummary
                {
                    itemId = itemId,
                    title = string.IsNullOrEmpty(details.m_rgchTitle) ? itemId : details.m_rgchTitle,
                    authorName = ResolveAuthorName(details.m_ulSteamIDOwner),
                    publishedUnixTime = details.m_rtimeCreated,
                    updatedUnixTime = details.m_rtimeUpdated > 0
                        ? details.m_rtimeUpdated
                        : details.m_rtimeCreated,
                    favoriteCount = favoriteCount,
                    isFavoritedByMe = favoritedIds.Contains(details.m_nPublishedFileId.m_PublishedFileId)
                });
            }

            SteamUGC.ReleaseQueryUGCRequest(queryHandle);
            return new QueryPageResult(working, returned, totalMatching);
        }

        private static bool ShouldStopRandomPaging(uint page, QueryPageResult pageResult)
        {
            if (pageResult.Returned == 0)
            {
                return true;
            }

            if (pageResult.TotalMatching > 0
                && page * (uint)QueryPageSizeHint >= pageResult.TotalMatching)
            {
                return true;
            }

            return pageResult.Returned < QueryPageSizeHint;
        }

        private void ApplySortMode(List<ModelGalleryItemSummary> working, ModelGalleryBrowseSortMode sortMode)
        {
            if (sortMode == ModelGalleryBrowseSortMode.MonthlyRanking)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                var monthItems = new List<ModelGalleryItemSummary>();
                for (int i = 0; i < working.Count; i++)
                {
                    if (IsSameUtcMonth(working[i].publishedUnixTime, now))
                    {
                        monthItems.Add(working[i]);
                    }
                }

                if (monthItems.Count > 0)
                {
                    working.Clear();
                    working.AddRange(monthItems);
                }
            }

            switch (sortMode)
            {
                case ModelGalleryBrowseSortMode.MonthlyRanking:
                case ModelGalleryBrowseSortMode.OverallRanking:
                    working.Sort((a, b) =>
                    {
                        int favoriteCompare = b.favoriteCount.CompareTo(a.favoriteCount);
                        if (favoriteCompare != 0)
                        {
                            return favoriteCompare;
                        }

                        return b.publishedUnixTime.CompareTo(a.publishedUnixTime);
                    });
                    break;
                case ModelGalleryBrowseSortMode.Latest:
                    working.Sort((a, b) =>
                    {
                        long aUpdated = a.updatedUnixTime > 0 ? a.updatedUnixTime : a.publishedUnixTime;
                        long bUpdated = b.updatedUnixTime > 0 ? b.updatedUnixTime : b.publishedUnixTime;
                        int updatedCompare = bUpdated.CompareTo(aUpdated);
                        if (updatedCompare != 0)
                        {
                            return updatedCompare;
                        }

                        return b.publishedUnixTime.CompareTo(a.publishedUnixTime);
                    });
                    break;
                default:
                    Shuffle(working);
                    if (working.Count > RandomDisplayCount)
                    {
                        working.RemoveRange(RandomDisplayCount, working.Count - RandomDisplayCount);
                    }

                    break;
            }
        }

        private async UniTask<bool> EnsureItemInstalledAsync(
            PublishedFileId_t fileId,
            CancellationToken cancellationToken)
        {
            uint state = SteamUGC.GetItemState(fileId);
            bool subscribed = (state & (uint)EItemState.k_EItemStateSubscribed) != 0;
            if (!subscribed)
            {
                SteamCallOutcome<RemoteStorageSubscribePublishedFileResult_t> subscribeOutcome =
                    await AwaitCallResultAsync<RemoteStorageSubscribePublishedFileResult_t>(
                        SteamUGC.SubscribeItem(fileId),
                        DefaultSteamTimeoutMs,
                        cancellationToken);
                if (subscribeOutcome.TimedOut
                    || subscribeOutcome.IoFailure
                    || (subscribeOutcome.Result.m_eResult != EResult.k_EResultOK
                        && subscribeOutcome.Result.m_eResult != EResult.k_EResultDuplicateRequest))
                {
                    Debug.LogError(
                        $"[SteamModelGalleryService] SubscribeItem失敗: timedOut={subscribeOutcome.TimedOut} failed={subscribeOutcome.IoFailure} result={subscribeOutcome.Result.m_eResult}");
                    return false;
                }
            }

            if (IsItemReady(fileId))
            {
                return true;
            }

            // コールバック登録後にDownloadItemを呼ぶ取りこぼしを防ぐ
            using CancellationTokenSource waitCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            UniTask<SteamCallOutcome<DownloadItemResult_t>> downloadWait =
                AwaitCallbackAsync<DownloadItemResult_t>(
                    result => result.m_nPublishedFileId == fileId,
                    DownloadSteamTimeoutMs,
                    waitCts.Token);

            state = SteamUGC.GetItemState(fileId);
            bool downloading = (state & (uint)EItemState.k_EItemStateDownloading) != 0
                || (state & (uint)EItemState.k_EItemStateDownloadPending) != 0;
            if (!downloading)
            {
                if (!SteamUGC.DownloadItem(fileId, true))
                {
                    waitCts.Cancel();
                    await SuppressCanceledAsync(downloadWait);
                    Debug.LogError(
                        $"[SteamModelGalleryService] DownloadItem開始失敗: {fileId.m_PublishedFileId}");
                    return false;
                }
            }

            if (IsItemReady(fileId))
            {
                waitCts.Cancel();
                await SuppressCanceledAsync(downloadWait);
                return true;
            }

            SteamCallOutcome<DownloadItemResult_t> downloadOutcome;
            try
            {
                downloadOutcome = await downloadWait;
            }
            catch (OperationCanceledException)
            {
                if (IsItemReady(fileId))
                {
                    return true;
                }

                throw;
            }

            if (downloadOutcome.TimedOut)
            {
                if (IsItemReady(fileId))
                {
                    return true;
                }

                Debug.LogError(
                    $"[SteamModelGalleryService] DownloadItem失敗: timedOut=True result={downloadOutcome.Result.m_eResult}");
                return false;
            }

            if (downloadOutcome.Result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError(
                    $"[SteamModelGalleryService] DownloadItem失敗: timedOut=False result={downloadOutcome.Result.m_eResult}");
                return false;
            }

            if (!IsItemReady(fileId))
            {
                Debug.LogError(
                    $"[SteamModelGalleryService] ダウンロード後も未インストールです: {fileId.m_PublishedFileId}");
                return false;
            }

            return true;
        }

        private static async UniTask SuppressCanceledAsync(
            UniTask<SteamCallOutcome<DownloadItemResult_t>> task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static bool IsItemReady(PublishedFileId_t fileId)
        {
            uint state = SteamUGC.GetItemState(fileId);
            bool installed = (state & (uint)EItemState.k_EItemStateInstalled) != 0;
            bool needsUpdate = (state & (uint)EItemState.k_EItemStateNeedsUpdate) != 0;
            return installed && !needsUpdate;
        }

        private async UniTask TryDeleteWorkshopItemAsync(
            PublishedFileId_t fileId,
            CancellationToken cancellationToken)
        {
            try
            {
                SteamCallOutcome<DeleteItemResult_t> deleteOutcome =
                    await AwaitCallResultAsync<DeleteItemResult_t>(
                        SteamUGC.DeleteItem(fileId),
                        DefaultSteamTimeoutMs,
                        cancellationToken);
                if (deleteOutcome.TimedOut
                    || deleteOutcome.IoFailure
                    || deleteOutcome.Result.m_eResult != EResult.k_EResultOK)
                {
                    Debug.LogWarning(
                        $"[SteamModelGalleryService] 未完成Workshopアイテム削除失敗: {fileId.m_PublishedFileId}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[SteamModelGalleryService] 未完成Workshopアイテム削除例外: {exception.Message}");
            }
        }

        private async UniTask<string> FetchPreviewUrlAsync(
            string itemId,
            CancellationToken cancellationToken)
        {
            if (!TryParsePublishedFileId(itemId, out PublishedFileId_t fileId))
            {
                return null;
            }

            var ids = new PublishedFileId_t[] { fileId };
            UGCQueryHandle_t queryHandle = SteamUGC.CreateQueryUGCDetailsRequest(ids, 1);
            SteamCallOutcome<SteamUGCQueryCompleted_t> completed =
                await AwaitCallResultAsync<SteamUGCQueryCompleted_t>(
                    SteamUGC.SendQueryUGCRequest(queryHandle),
                    DefaultSteamTimeoutMs,
                    cancellationToken);
            if (completed.TimedOut
                || completed.IoFailure
                || completed.Result.m_eResult != EResult.k_EResultOK
                || completed.Result.m_unNumResultsReturned == 0)
            {
                SteamUGC.ReleaseQueryUGCRequest(queryHandle);
                return null;
            }

            SteamUGC.GetQueryUGCPreviewURL(queryHandle, 0, out string previewUrl, 1024);
            SteamUGC.ReleaseQueryUGCRequest(queryHandle);
            return previewUrl;
        }

        private async UniTask<HashSet<ulong>> LoadFavoritedPublishedFileIdsAsync(
            CancellationToken cancellationToken)
        {
            var favorited = new HashSet<ulong>();
            AppId_t appId = GetAppId();
            AccountID_t accountId = SteamUser.GetSteamID().GetAccountID();
            for (uint page = 1; page <= MaxFavoritePages; page++)
            {
                UGCQueryHandle_t queryHandle = SteamUGC.CreateQueryUserUGCRequest(
                    accountId,
                    EUserUGCList.k_EUserUGCList_Favorited,
                    EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items,
                    EUserUGCListSortOrder.k_EUserUGCListSortOrder_CreationOrderDesc,
                    appId,
                    appId,
                    page);
                SteamCallOutcome<SteamUGCQueryCompleted_t> completed =
                    await AwaitCallResultAsync<SteamUGCQueryCompleted_t>(
                        SteamUGC.SendQueryUGCRequest(queryHandle),
                        DefaultSteamTimeoutMs,
                        cancellationToken);
                if (completed.TimedOut
                    || completed.IoFailure
                    || completed.Result.m_eResult != EResult.k_EResultOK)
                {
                    SteamUGC.ReleaseQueryUGCRequest(queryHandle);
                    break;
                }

                for (uint i = 0; i < completed.Result.m_unNumResultsReturned; i++)
                {
                    if (!SteamUGC.GetQueryUGCResult(queryHandle, i, out SteamUGCDetails_t details))
                    {
                        continue;
                    }

                    favorited.Add(details.m_nPublishedFileId.m_PublishedFileId);
                }

                uint returned = completed.Result.m_unNumResultsReturned;
                uint total = completed.Result.m_unTotalMatchingResults;
                SteamUGC.ReleaseQueryUGCRequest(queryHandle);
                if (returned == 0 || page * QueryPageSizeHint >= total)
                {
                    break;
                }
            }

            return favorited;
        }

        private string PrepareUploadFolder(
            PublishedFileId_t fileId,
            ModelSaveSlot slot,
            string resolvedTitle,
            byte[] glbBytes)
        {
            string folder = Path.Combine(
                Application.temporaryCachePath,
                UploadFolderName,
                fileId.m_PublishedFileId.ToString());
            TryDeleteDirectory(folder);
            Directory.CreateDirectory(folder);

            var meta = new ModelGalleryPackageMeta
            {
                packageVersion = "1",
                modelName = string.IsNullOrEmpty(slot.modelName) ? resolvedTitle : slot.modelName,
                status = ModelStatus.CloneOrDefault(slot.status),
                attackMotions = slot.attackMotions != null
                    ? new List<MotionType>(slot.attackMotions)
                    : new List<MotionType>()
            };

            File.WriteAllText(Path.Combine(folder, MetaFileName), JsonUtility.ToJson(meta, true));
            File.WriteAllBytes(Path.Combine(folder, ModelFileName), glbBytes);

            if (!string.IsNullOrEmpty(slot.thumbnailFileName))
            {
                byte[] previewBytes = ModelSaveStorage.ReadAllBytes(slot.thumbnailFileName);
                if (previewBytes != null && previewBytes.Length > 0)
                {
                    File.WriteAllBytes(Path.Combine(folder, PreviewFileName), previewBytes);
                }
            }

            return folder;
        }

        private static EUGCQuery ResolveQueryType(ModelGalleryBrowseSortMode sortMode)
        {
            switch (sortMode)
            {
                case ModelGalleryBrowseSortMode.MonthlyRanking:
                    return EUGCQuery.k_EUGCQuery_RankedByTrend;
                case ModelGalleryBrowseSortMode.OverallRanking:
                    return EUGCQuery.k_EUGCQuery_RankedByVote;
                case ModelGalleryBrowseSortMode.Latest:
                    return EUGCQuery.k_EUGCQuery_RankedByLastUpdatedDate;
                case ModelGalleryBrowseSortMode.Random:
                    // 並びは無視して全件取得用の安定クエリとして使う
                    return EUGCQuery.k_EUGCQuery_RankedByVote;
                default:
                    return EUGCQuery.k_EUGCQuery_RankedByVote;
            }
        }

        private static int ResolveQueryPageCount(ModelGalleryBrowseSortMode sortMode)
        {
            switch (sortMode)
            {
                case ModelGalleryBrowseSortMode.Random:
                    return MaxRandomQueryPages;
                case ModelGalleryBrowseSortMode.OverallRanking:
                case ModelGalleryBrowseSortMode.Latest:
                    return OverallRankingPages;
                default:
                    return 1;
            }
        }

        private static string ResolveAuthorName(ulong steamIdOwner)
        {
            if (steamIdOwner == 0)
            {
                return "Unknown";
            }

            var steamId = new CSteamID(steamIdOwner);
            SteamFriends.RequestUserInformation(steamId, true);
            string name = SteamFriends.GetFriendPersonaName(steamId);
            return string.IsNullOrEmpty(name) ? steamIdOwner.ToString() : name;
        }

        private static bool IsSameUtcMonth(long unixTime, DateTimeOffset now)
        {
            var published = DateTimeOffset.FromUnixTimeSeconds(unixTime);
            return published.Year == now.Year && published.Month == now.Month;
        }

        private static bool EnsureSteamReady()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError(
                    "[SteamModelGalleryService] Steamが初期化されていませんSteamクライアント起動とAppID設定を確認してください");
                return false;
            }

            if (!SteamUser.BLoggedOn())
            {
                Debug.LogError("[SteamModelGalleryService] Steamにログインしていません");
                return false;
            }

            return true;
        }

        private static AppId_t GetAppId()
        {
            return new AppId_t(SteamAppIds.ClayMonsters);
        }

        private static void OpenWorkshopLegalAgreement()
        {
            SteamFriends.ActivateGameOverlayToWebPage(WorkshopLegalAgreementUrl);
        }

        private static bool TryParsePublishedFileId(string itemId, out PublishedFileId_t fileId)
        {
            fileId = PublishedFileId_t.Invalid;
            if (!ulong.TryParse(itemId, out ulong rawId) || rawId == 0)
            {
                return false;
            }

            fileId = new PublishedFileId_t(rawId);
            return true;
        }

        private void Shuffle(List<ModelGalleryItemSummary> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static void TryDeleteDirectory(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[SteamModelGalleryService] 一時フォルダ削除失敗: {folder} {exception.Message}");
            }
        }

        private static async UniTask<SteamCallOutcome<T>> AwaitCallResultAsync<T>(
            SteamAPICall_t apiCall,
            int timeoutMs,
            CancellationToken cancellationToken)
            where T : struct
        {
            var completion = AutoResetUniTaskCompletionSource<(T result, bool ioFailure)>.Create();
            CallResult<T> callResult = null;
            callResult = CallResult<T>.Create((result, ioFailure) =>
            {
                completion.TrySetResult((result, ioFailure));
            });
            callResult.Set(apiCall);

            using (cancellationToken.Register(() =>
                   {
                       callResult?.Cancel();
                       completion.TrySetCanceled(cancellationToken);
                   }))
            {
                try
                {
                    (bool completedFirst, (T result, bool ioFailure) value) = await UniTask.WhenAny(
                        completion.Task,
                        UniTask.Delay(timeoutMs, cancellationToken: cancellationToken));
                    if (!completedFirst)
                    {
                        callResult?.Cancel();
                        return SteamCallOutcome<T>.Timeout();
                    }

                    return SteamCallOutcome<T>.Completed(value.result, value.ioFailure);
                }
                finally
                {
                    callResult?.Dispose();
                }
            }
        }

        private static async UniTask<SteamCallOutcome<T>> AwaitCallbackAsync<T>(
            Func<T, bool> predicate,
            int timeoutMs,
            CancellationToken cancellationToken)
            where T : struct
        {
            var completion = AutoResetUniTaskCompletionSource<T>.Create();
            Callback<T> callback = null;
            callback = Callback<T>.Create(result =>
            {
                if (predicate != null && !predicate(result))
                {
                    return;
                }

                completion.TrySetResult(result);
            });

            using (cancellationToken.Register(() =>
                   {
                       callback?.Dispose();
                       completion.TrySetCanceled(cancellationToken);
                   }))
            {
                try
                {
                    (bool completedFirst, T value) = await UniTask.WhenAny(
                        completion.Task,
                        UniTask.Delay(timeoutMs, cancellationToken: cancellationToken));
                    if (!completedFirst)
                    {
                        return SteamCallOutcome<T>.Timeout();
                    }

                    return SteamCallOutcome<T>.Completed(value, false);
                }
                finally
                {
                    callback?.Dispose();
                }
            }
        }

        private readonly struct QueryPageResult
        {
            public readonly List<ModelGalleryItemSummary> Items;
            public readonly uint Returned;
            public readonly uint TotalMatching;

            public QueryPageResult(List<ModelGalleryItemSummary> items, uint returned, uint totalMatching)
            {
                Items = items ?? new List<ModelGalleryItemSummary>();
                Returned = returned;
                TotalMatching = totalMatching;
            }
        }

        private readonly struct SteamCallOutcome<T>
            where T : struct
        {
            public readonly T Result;
            public readonly bool IoFailure;
            public readonly bool TimedOut;

            private SteamCallOutcome(T result, bool ioFailure, bool timedOut)
            {
                Result = result;
                IoFailure = ioFailure;
                TimedOut = timedOut;
            }

            public static SteamCallOutcome<T> Completed(T result, bool ioFailure)
            {
                return new SteamCallOutcome<T>(result, ioFailure, false);
            }

            public static SteamCallOutcome<T> Timeout()
            {
                return new SteamCallOutcome<T>(default, true, true);
            }
        }
    }
}
