using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData;
using SaveData.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UI.ModelGallery.Data;
using UI.ModelGallery.Interface;
using UnityEngine;

namespace UI.ModelGallery.Service
{
    /// <summary>
    /// ローカルフォルダで展示室の投稿閲覧ダウンロードを行う
    /// Steam UGC導入前の動作確認用実装
    /// </summary>
    public sealed class LocalModelGalleryService : IModelGalleryService
    {
        private const string RootFolderName = "ModelGallery";
        private const string IndexFileName = "index.json";
        private const string FavoritesFileName = "local_favorites.json";
        private const string ItemsFolderName = "items";
        private const string LocalAuthorName = "LocalPlayer";
        private const int RandomDisplayCount = 30;

        private readonly IClayModelSaveService saveService;
        private readonly System.Random random = new();

        /// <summary>
        /// 依存を注入する
        /// </summary>
        /// <param name="saveService">モデルセーブサービス</param>
        public LocalModelGalleryService(IClayModelSaveService saveService)
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

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, sourceSlotIndex);
            if (slot == null)
            {
                Debug.LogError($"[LocalModelGalleryService] 未育成スロット{sourceSlotIndex}が空です");
                return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.Failed);
            }

            byte[] glbBytes = ModelSaveStorage.ReadAllBytes(slot.glbFileName);
            if (glbBytes == null || glbBytes.Length == 0)
            {
                Debug.LogError($"[LocalModelGalleryService] glbが読めません: {slot.glbFileName}");
                return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.Failed);
            }

            string voxelFileName = ModelSavePoolSettings.GetVoxelFileName(ModelSavePool.Player, sourceSlotIndex);
            if (!ModelGalleryVoxelPackage.TryReadCompressedFromSlot(voxelFileName, out byte[] voxelCompressedBytes))
            {
                Debug.LogError(
                    $"[LocalModelGalleryService] voxelが読めません: {voxelFileName}"
                    + " ClayEditで保存し直してから投稿してください");
                return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.Failed);
            }

            string itemId = Guid.NewGuid().ToString("N");
            string itemFolder = GetItemFolder(itemId);
            Directory.CreateDirectory(itemFolder);

            string resolvedTitle = string.IsNullOrWhiteSpace(title)
                ? (string.IsNullOrEmpty(slot.modelName) ? $"Slot{sourceSlotIndex}" : slot.modelName)
                : title.Trim();

            var meta = new ModelGalleryPackageMeta
            {
                packageVersion = ModelGalleryPackageFiles.PackageVersionWithVoxel,
                modelName = string.IsNullOrEmpty(slot.modelName) ? resolvedTitle : slot.modelName,
                status = ModelStatus.CloneOrDefault(slot.status),
                attackMotions = slot.attackMotions != null
                    ? new List<MotionType>(slot.attackMotions)
                    : new List<MotionType>()
            };

            byte[] previewBytes = null;
            if (!string.IsNullOrEmpty(slot.thumbnailFileName))
            {
                previewBytes = ModelSaveStorage.ReadAllBytes(slot.thumbnailFileName);
            }

            if (!ModelGalleryPackageWriter.TryWrite(itemFolder, meta, glbBytes, voxelCompressedBytes, previewBytes))
            {
                Debug.LogError($"[LocalModelGalleryService] パッケージ書き込みに失敗しました: {itemId}");
                return ModelGalleryPublishResult.FromStatus(ModelGalleryOperationStatus.Failed);
            }

            ModelGalleryIndex index = LoadIndex();
            index.entries.Add(new ModelGalleryIndexEntry
            {
                itemId = itemId,
                title = resolvedTitle,
                authorName = LocalAuthorName,
                publishedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                favoriteCount = 0
            });
            SaveIndex(index);
            return ModelGalleryPublishResult.Success(itemId);
        }

        /// <inheritdoc />
        public async UniTask<IReadOnlyList<ModelGalleryItemSummary>> QueryAsync(
            ModelGalleryBrowseSortMode sortMode,
            CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);

            ModelGalleryIndex index = LoadIndex();
            ModelGalleryLocalFavorites favorites = LoadFavorites();
            var working = new List<ModelGalleryItemSummary>(index.entries.Count);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            for (int i = 0; i < index.entries.Count; i++)
            {
                ModelGalleryIndexEntry entry = index.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.itemId))
                {
                    continue;
                }

                if (sortMode == ModelGalleryBrowseSortMode.MonthlyRanking
                    && !IsSameUtcMonth(entry.publishedUnixTime, now))
                {
                    continue;
                }

                working.Add(ToSummary(entry, favorites));
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

            return working;
        }

        /// <inheritdoc />
        public async UniTask<Texture2D> LoadPreviewAsync(string itemId, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);

            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            string previewPath = Path.Combine(GetItemFolder(itemId), ModelGalleryPackageFiles.PreviewFileName);
            if (!File.Exists(previewPath))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(previewPath);
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

            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            ModelGalleryIndex index = LoadIndex();
            ModelGalleryIndexEntry entry = FindEntry(index, itemId);
            if (entry == null)
            {
                Debug.LogError($"[LocalModelGalleryService] お気に入り対象が見つかりません: {itemId}");
                return null;
            }

            ModelGalleryLocalFavorites favorites = LoadFavorites();
            bool currentlyFavorited = ContainsFavorite(favorites, itemId);
            if (currentlyFavorited)
            {
                favorites.itemIds.RemoveAll(id => id == itemId);
                entry.favoriteCount = Mathf.Max(0, entry.favoriteCount - 1);
            }
            else
            {
                favorites.itemIds.Add(itemId);
                entry.favoriteCount += 1;
            }

            SaveFavorites(favorites);
            SaveIndex(index);
            return !currentlyFavorited;
        }

        /// <inheritdoc />
        public async UniTask<bool> DownloadAsync(
            string itemId,
            int destinationSlotIndex,
            CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);

            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            string itemFolder = GetItemFolder(itemId);
            string metaPath = Path.Combine(itemFolder, ModelGalleryPackageFiles.MetaFileName);
            string modelPath = Path.Combine(itemFolder, ModelGalleryPackageFiles.ModelFileName);
            if (!File.Exists(metaPath) || !File.Exists(modelPath) || !ModelGalleryVoxelPackage.PackageContainsVoxel(itemFolder))
            {
                Debug.LogError($"[LocalModelGalleryService] パッケージが見つかりません: {itemId}");
                return false;
            }

            ModelGalleryPackageMeta meta = JsonUtility.FromJson<ModelGalleryPackageMeta>(File.ReadAllText(metaPath));
            if (meta == null)
            {
                Debug.LogError($"[LocalModelGalleryService] package.jsonの解析に失敗しました: {itemId}");
                return false;
            }

            byte[] glbBytes = File.ReadAllBytes(modelPath);
            if (glbBytes == null || glbBytes.Length == 0)
            {
                Debug.LogError($"[LocalModelGalleryService] model.glbが空です: {itemId}");
                return false;
            }

            if (!ModelGalleryVoxelPackage.TryReadRawFromPackageFolder(itemFolder, out byte[] voxelBytes))
            {
                Debug.LogError($"[LocalModelGalleryService] model.voxelが空です: {itemId}");
                return false;
            }

            byte[] thumbnailPng = null;
            string previewPath = Path.Combine(itemFolder, ModelGalleryPackageFiles.PreviewFileName);
            if (File.Exists(previewPath))
            {
                thumbnailPng = File.ReadAllBytes(previewPath);
            }

            // 保存確認済みのため使用中スロットも上書きする
            return saveService.ImportUntrainedSlot(
                destinationSlotIndex,
                string.IsNullOrEmpty(meta.modelName) ? "GalleryMonster" : meta.modelName,
                ModelStatus.CloneOrDefault(meta.status),
                meta.attackMotions,
                glbBytes,
                thumbnailPng,
                voxelBytes,
                overwrite: true);
        }

        private static ModelGalleryItemSummary ToSummary(
            ModelGalleryIndexEntry entry,
            ModelGalleryLocalFavorites favorites)
        {
            return new ModelGalleryItemSummary
            {
                itemId = entry.itemId,
                title = entry.title,
                authorName = entry.authorName,
                publishedUnixTime = entry.publishedUnixTime,
                updatedUnixTime = entry.publishedUnixTime,
                favoriteCount = Mathf.Max(0, entry.favoriteCount),
                isFavoritedByMe = ContainsFavorite(favorites, entry.itemId)
            };
        }

        private static bool IsSameUtcMonth(long unixTime, DateTimeOffset now)
        {
            var published = DateTimeOffset.FromUnixTimeSeconds(unixTime);
            return published.Year == now.Year && published.Month == now.Month;
        }

        private void Shuffle(List<ModelGalleryItemSummary> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static ModelGalleryIndexEntry FindEntry(ModelGalleryIndex index, string itemId)
        {
            for (int i = 0; i < index.entries.Count; i++)
            {
                ModelGalleryIndexEntry entry = index.entries[i];
                if (entry != null && entry.itemId == itemId)
                {
                    return entry;
                }
            }

            return null;
        }

        private static bool ContainsFavorite(ModelGalleryLocalFavorites favorites, string itemId)
        {
            if (favorites?.itemIds == null || string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            for (int i = 0; i < favorites.itemIds.Count; i++)
            {
                if (favorites.itemIds[i] == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetRootFolder()
        {
            string root = Path.Combine(Application.persistentDataPath, RootFolderName);
            Directory.CreateDirectory(root);
            return root;
        }

        private static string GetItemFolder(string itemId)
        {
            return Path.Combine(GetRootFolder(), ItemsFolderName, itemId);
        }

        private static string GetIndexPath()
        {
            return Path.Combine(GetRootFolder(), IndexFileName);
        }

        private static string GetFavoritesPath()
        {
            return Path.Combine(GetRootFolder(), FavoritesFileName);
        }

        private static ModelGalleryIndex LoadIndex()
        {
            string path = GetIndexPath();
            if (!File.Exists(path))
            {
                return new ModelGalleryIndex();
            }

            ModelGalleryIndex index = JsonUtility.FromJson<ModelGalleryIndex>(File.ReadAllText(path));
            if (index == null)
            {
                return new ModelGalleryIndex();
            }

            if (index.entries == null)
            {
                index.entries = new List<ModelGalleryIndexEntry>();
            }

            return index;
        }

        private static void SaveIndex(ModelGalleryIndex index)
        {
            File.WriteAllText(GetIndexPath(), JsonUtility.ToJson(index, true));
        }

        private static ModelGalleryLocalFavorites LoadFavorites()
        {
            string path = GetFavoritesPath();
            if (!File.Exists(path))
            {
                return new ModelGalleryLocalFavorites();
            }

            ModelGalleryLocalFavorites favorites =
                JsonUtility.FromJson<ModelGalleryLocalFavorites>(File.ReadAllText(path));
            if (favorites == null)
            {
                return new ModelGalleryLocalFavorites();
            }

            if (favorites.itemIds == null)
            {
                favorites.itemIds = new List<string>();
            }

            return favorites;
        }

        private static void SaveFavorites(ModelGalleryLocalFavorites favorites)
        {
            File.WriteAllText(GetFavoritesPath(), JsonUtility.ToJson(favorites, true));
        }
    }
}
