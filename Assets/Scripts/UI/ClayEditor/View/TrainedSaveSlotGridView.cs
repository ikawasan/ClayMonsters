using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Extensions;
using Localization;
using SaveData;
using SaveData.Interface;
using TMPro;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 育成済みセーブスロットを5列×10行で並べるグリッド一覧
    /// サムネイルとTMP字形はキャッシュし明転前に完了させる
    /// </summary>
    public sealed class TrainedSaveSlotGridView : MonoBehaviour
    {
        public const int ColumnCount = ModelSavePoolSettings.TrainedGridColumnCount;
        public const int RowCount = ModelSavePoolSettings.TrainedGridRowCount;
        public const int SlotCount = ModelSavePoolSettings.TrainedSlotCount;

        private const int MeshYieldInterval = 16;
        private const int ThumbnailDecodeBatchSize = 8;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private TrainedSaveSlotCellView[] cells =
            new TrainedSaveSlotCellView[SlotCount];

        private readonly List<UnityEngine.Object> runtimeThumbnailObjects = new List<UnityEngine.Object>();
        private readonly Dictionary<ThumbnailCacheKey, CachedThumbnail> thumbnailCache =
            new Dictionary<ThumbnailCacheKey, CachedThumbnail>();
        private readonly StringBuilder displayedCharactersBuilder = new StringBuilder(512);

        private Action<int> onSlotSelected;
        private Action<int> onSlotPointerEnter;
        private Action onSlotPointerExit;
        private bool isClickBound;
        private ModelSavePool cachedPool;
        private bool hasCachedPool;

        /// <summary>
        /// クリック購読を初期化する
        /// </summary>
        /// <param name="slotSelectedHandler">選択時コールバック</param>
        public void Initialize(Action<int> slotSelectedHandler)
        {
            onSlotSelected = slotSelectedHandler;
            EnsureCells();
            if (isClickBound)
            {
                ApplyHoverHandlers();
                return;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                TrainedSaveSlotCellView cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                cell.Initialize(OnCellSelected);
            }

            isClickBound = true;
            ApplyHoverHandlers();
        }

        /// <summary>
        /// ホバー購読を設定する
        /// </summary>
        /// <param name="enterHandler">進入時コールバック</param>
        /// <param name="exitHandler">退出時コールバック</param>
        public void SetHoverHandlers(Action<int> enterHandler, Action exitHandler)
        {
            onSlotPointerEnter = enterHandler;
            onSlotPointerExit = exitHandler;
            ApplyHoverHandlers();
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
            Refresh(
                saveService,
                ModelSavePool.TrainedPlayer,
                emptySlotLabel,
                allowEmptySlotSelection);
        }

        /// <summary>
        /// 指定プールのスロット一覧を更新する
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="pool">対象プール</param>
        /// <param name="emptySlotLabel">空スロット文言</param>
        /// <param name="allowEmptySlotSelection">空スロット選択を許可するか</param>
        public void Refresh(
            IClayModelSaveService saveService,
            ModelSavePool pool,
            string emptySlotLabel,
            bool allowEmptySlotSelection)
        {
            Refresh(
                saveService,
                pool,
                emptySlotLabel,
                allowEmptySlotSelection,
                isSlotUnlocked: null);
        }

        /// <summary>
        /// 指定プールのスロット一覧を更新する
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="pool">対象プール</param>
        /// <param name="emptySlotLabel">空スロット文言</param>
        /// <param name="allowEmptySlotSelection">空スロット選択を許可するか</param>
        /// <param name="isSlotUnlocked">使用中スロットの選択可否(nullなら常に可)</param>
        public void Refresh(
            IClayModelSaveService saveService,
            ModelSavePool pool,
            string emptySlotLabel,
            bool allowEmptySlotSelection,
            Func<int, bool> isSlotUnlocked)
        {
            BindAllSlots(
                saveService,
                pool,
                emptySlotLabel,
                allowEmptySlotSelection,
                isSlotUnlocked);
        }

        /// <summary>
        /// 明転前にサムネイルとTMP字形とメッシュを完了する
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="pool">対象プール</param>
        /// <param name="emptySlotLabel">空スロット文言</param>
        /// <param name="allowEmptySlotSelection">空スロット選択を許可するか</param>
        /// <param name="isSlotUnlocked">使用中スロットの選択可否(nullなら常に可)</param>
        /// <param name="cancellationToken">中断トークン</param>
        public async UniTask PrepareContentsAsync(
            IClayModelSaveService saveService,
            ModelSavePool pool,
            string emptySlotLabel,
            bool allowEmptySlotSelection,
            Func<int, bool> isSlotUnlocked,
            CancellationToken cancellationToken)
        {
            EnsureCells();
            if (saveService == null)
            {
                Debug.LogError(
                    "[TrainedSaveSlotGridView] saveServiceがnullです",
                    this);
                return;
            }

            if (!hasCachedPool || cachedPool != pool)
            {
                // プール切替でも(pool,slot)キーのキャッシュは残し再デコードを避ける
                cachedPool = pool;
                hasCachedPool = true;
            }

            ClayModelSaveData data = saveService.Load(pool);
            await EnsureThumbnailsCachedAsync(
                saveService,
                data,
                pool,
                isSlotUnlocked,
                cancellationToken);

            displayedCharactersBuilder.Clear();
            if (!string.IsNullOrEmpty(emptySlotLabel))
            {
                displayedCharactersBuilder.Append(emptySlotLabel);
            }

            int poolSlotCount = ModelSavePoolSettings.GetSlotCount(pool);
            for (int i = 0; i < SlotCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TrainedSaveSlotCellView cell = i < cells.Length ? cells[i] : null;
                if (cell == null)
                {
                    continue;
                }

                if (i >= poolSlotCount)
                {
                    if (cell.gameObject.activeSelf)
                    {
                        cell.gameObject.SetActive(false);
                    }

                    continue;
                }

                if (!cell.gameObject.activeSelf)
                {
                    cell.gameObject.SetActive(true);
                }

                ModelSaveSlot slot = ResolveSlotFromData(data, i);
                bool used = slot != null
                    && slot.isUsed
                    && !string.IsNullOrEmpty(slot.glbFileName);
                if (!used)
                {
                    cell.BindEmpty(i, emptySlotLabel, allowEmptySlotSelection);
                    AppendCellDisplayText(cell);
                    continue;
                }

                bool interactable = isSlotUnlocked == null || isSlotUnlocked(i);
                Sprite thumbnail = null;
                if (interactable)
                {
                    TryGetCachedThumbnailSprite(pool, i, out thumbnail);
                }

                string displayName = ResolveDisplayName(pool, i, slot);
                if (!string.IsNullOrEmpty(displayName) && interactable)
                {
                    displayedCharactersBuilder.Append(displayName);
                }

                cell.BindUsed(i, displayName, thumbnail, interactable);
                AppendCellDisplayText(cell);
            }

            cancellationToken.ThrowIfCancellationRequested();
            LocalizedFont.WarmupCharacters(displayedCharactersBuilder.ToString());

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            Canvas.ForceUpdateCanvases();
            await ApplyNameMeshesAsync(cancellationToken);
        }

        /// <summary>
        /// 表示せずサムネイルだけ先読みしてキャッシュする
        /// </summary>
        /// <param name="saveService">セーブサービス</param>
        /// <param name="pool">対象プール</param>
        /// <param name="isSlotUnlocked">使用中スロットの選択可否(nullなら常に可)</param>
        /// <param name="cancellationToken">中断トークン</param>
        public async UniTask PrefetchThumbnailsAsync(
            IClayModelSaveService saveService,
            ModelSavePool pool,
            Func<int, bool> isSlotUnlocked,
            CancellationToken cancellationToken)
        {
            if (saveService == null)
            {
                return;
            }

            ClayModelSaveData data = saveService.Load(pool);
            await EnsureThumbnailsCachedAsync(
                saveService,
                data,
                pool,
                isSlotUnlocked,
                cancellationToken);
        }

        private async UniTask EnsureThumbnailsCachedAsync(
            IClayModelSaveService saveService,
            ClayModelSaveData data,
            ModelSavePool pool,
            Func<int, bool> isSlotUnlocked,
            CancellationToken cancellationToken)
        {
            if (data?.slots == null)
            {
                return;
            }

            int poolSlotCount = Mathf.Min(
                ModelSavePoolSettings.GetSlotCount(pool),
                data.slots.Count);
            var pending = new List<PendingThumbnailLoad>(poolSlotCount);
            for (int i = 0; i < poolSlotCount; i++)
            {
                ModelSaveSlot slot = ResolveSlotFromData(data, i);
                if (slot == null
                    || !slot.isUsed
                    || string.IsNullOrEmpty(slot.glbFileName)
                    || string.IsNullOrEmpty(slot.thumbnailFileName))
                {
                    continue;
                }

                if (isSlotUnlocked != null && !isSlotUnlocked(i))
                {
                    continue;
                }

                if (TryGetCachedThumbnail(saveService, pool, i, out _))
                {
                    continue;
                }

                pending.Add(new PendingThumbnailLoad(i, slot.thumbnailFileName));
            }

            if (pending.Count == 0)
            {
                return;
            }

            // persistentDataPath等はメインスレッド限定のため先にキャッシュする
            ModelSaveStorage.EnsureUnityPathsCached();

            var loadedBytes = new byte[pending.Count][];
            var revisions = new long[pending.Count];
            await UniTask.RunOnThreadPool(
                () =>
                {
                    Parallel.For(
                        0,
                        pending.Count,
                        index =>
                        {
                            PendingThumbnailLoad item = pending[index];
                            revisions[index] = ResolveThumbnailRevisionFromFileName(item.FileName);
                            loadedBytes[index] = ModelSaveStorage.ReadAllBytes(item.FileName);
                        });
                },
                cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            int decoded = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PendingThumbnailLoad item = pending[i];
                byte[] bytes = loadedBytes[i];
                if (bytes == null || bytes.Length == 0)
                {
                    continue;
                }

                if (TryGetCachedThumbnail(saveService, pool, item.SlotIndex, out _))
                {
                    continue;
                }

                CacheThumbnailFromPngBytes(pool, item.SlotIndex, revisions[i], bytes);
                decoded++;
                if (decoded % ThumbnailDecodeBatchSize == 0)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
        }

        private void CacheThumbnailFromPngBytes(
            ModelSavePool pool,
            int slotIndex,
            long revision,
            byte[] pngBytes)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!texture.LoadImage(pngBytes))
            {
                Destroy(texture);
                return;
            }

            EvictThumbnailCacheEntry(pool, slotIndex);
            runtimeThumbnailObjects.Add(texture);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            runtimeThumbnailObjects.Add(sprite);
            thumbnailCache[new ThumbnailCacheKey(pool, slotIndex)] =
                new CachedThumbnail(revision, sprite, texture);
        }

        private static ModelSaveSlot ResolveSlotFromData(ClayModelSaveData data, int slotIndex)
        {
            if (data?.slots == null
                || slotIndex < 0
                || slotIndex >= data.slots.Count)
            {
                return null;
            }

            ModelSaveSlot slot = data.slots[slotIndex];
            if (slot == null || !slot.isUsed)
            {
                return null;
            }

            return slot;
        }

        /// <summary>
        /// グリッドを表示する
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            if (gridContent != null)
            {
                gridContent.gameObject.SetActive(true);
            }

            // 自身のCanvasがある場合のみenabledを切替える
            // 親Canvasは触らない
            Canvas ownCanvas = ResolveOwnCanvas();
            if (ownCanvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(ownCanvas, true);
            }
        }

        /// <summary>
        /// グリッドを隠す
        /// </summary>
        public void Hide()
        {
            if (gridContent != null)
            {
                gridContent.gameObject.SetActive(false);
            }

            Canvas ownCanvas = ResolveOwnCanvas();
            if (ownCanvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(ownCanvas, false);
                return;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// シーン退場時に整理する
        /// </summary>
        public void HideForLeave()
        {
            Hide();
            ClearRuntimeThumbnails();
            hasCachedPool = false;
        }

        private void BindAllSlots(
            IClayModelSaveService saveService,
            ModelSavePool pool,
            string emptySlotLabel,
            bool allowEmptySlotSelection,
            Func<int, bool> isSlotUnlocked)
        {
            EnsureCells();
            if (saveService == null)
            {
                Debug.LogError(
                    "[TrainedSaveSlotGridView] saveServiceがnullです",
                    this);
                return;
            }

            if (!hasCachedPool || cachedPool != pool)
            {
                // プール切替でも(pool,slot)キーのキャッシュは残し再デコードを避ける
                cachedPool = pool;
                hasCachedPool = true;
            }

            ClayModelSaveData data = saveService.Load(pool);
            displayedCharactersBuilder.Clear();
            if (!string.IsNullOrEmpty(emptySlotLabel))
            {
                displayedCharactersBuilder.Append(emptySlotLabel);
            }

            int poolSlotCount = ModelSavePoolSettings.GetSlotCount(pool);
            for (int i = 0; i < SlotCount; i++)
            {
                TrainedSaveSlotCellView cell = i < cells.Length ? cells[i] : null;
                if (cell == null)
                {
                    continue;
                }

                if (i >= poolSlotCount)
                {
                    if (cell.gameObject.activeSelf)
                    {
                        cell.gameObject.SetActive(false);
                    }

                    continue;
                }

                if (!cell.gameObject.activeSelf)
                {
                    cell.gameObject.SetActive(true);
                }

                ModelSaveSlot slot = ResolveSlotFromData(data, i);
                bool used = slot != null
                    && slot.isUsed
                    && !string.IsNullOrEmpty(slot.glbFileName);
                if (!used)
                {
                    cell.BindEmpty(i, emptySlotLabel, allowEmptySlotSelection);
                    AppendCellDisplayText(cell);
                    continue;
                }

                bool interactable = isSlotUnlocked == null || isSlotUnlocked(i);
                Sprite thumbnail = interactable
                    ? LoadThumbnailSprite(saveService, pool, i)
                    : null;

                string displayName = ResolveDisplayName(pool, i, slot);
                if (!string.IsNullOrEmpty(displayName) && interactable)
                {
                    displayedCharactersBuilder.Append(displayName);
                }

                cell.BindUsed(i, displayName, thumbnail, interactable);
                AppendCellDisplayText(cell);
            }

            LocalizedFont.WarmupCharacters(displayedCharactersBuilder.ToString());
        }

        private async UniTask ApplyNameMeshesAsync(CancellationToken cancellationToken)
        {
            EnsureCells();
            int updated = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TrainedSaveSlotCellView cell = cells[i];
                if (cell == null || !cell.gameObject.activeInHierarchy)
                {
                    continue;
                }

                TMP_Text nameText = cell.NameText;
                if (nameText == null || !nameText.enabled || string.IsNullOrEmpty(nameText.text))
                {
                    continue;
                }

                LocalizedFont.Apply(nameText);
                if (nameText.isActiveAndEnabled)
                {
                    nameText.ForceMeshUpdate(true);
                }

                updated++;
                if (updated % MeshYieldInterval == 0)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
        }

        private void AppendCellDisplayText(TrainedSaveSlotCellView cell)
        {
            TMP_Text nameText = cell != null ? cell.NameText : null;
            if (nameText == null || string.IsNullOrEmpty(nameText.text))
            {
                return;
            }

            displayedCharactersBuilder.Append(nameText.text);
        }

        private static string ResolveDisplayName(
            ModelSavePool pool,
            int slotIndex,
            ModelSaveSlot slot)
        {
            string modelName = slot != null ? slot.modelName : string.Empty;
            if (pool == ModelSavePool.Enemy)
            {
                return EnemyDisplayName.Resolve(slotIndex, modelName);
            }

            return modelName ?? string.Empty;
        }

        private void ApplyHoverHandlers()
        {
            EnsureCells();
            for (int i = 0; i < cells.Length; i++)
            {
                TrainedSaveSlotCellView cell = cells[i];
                if (cell == null)
                {
                    continue;
                }

                cell.SetHoverHandlers(onSlotPointerEnter, onSlotPointerExit);
            }
        }

        private void OnCellSelected(int slotIndex)
        {
            onSlotSelected?.Invoke(slotIndex);
        }

        private Sprite LoadThumbnailSprite(
            IClayModelSaveService saveService,
            ModelSavePool pool,
            int slotIndex)
        {
            if (TryGetCachedThumbnail(saveService, pool, slotIndex, out Sprite cached))
            {
                return cached;
            }

            long revision = ResolveThumbnailRevision(saveService, pool, slotIndex);
            EvictThumbnailCacheEntry(pool, slotIndex);

            Texture2D texture = saveService.LoadThumbnail(pool, slotIndex);
            if (texture == null)
            {
                return null;
            }

            runtimeThumbnailObjects.Add(texture);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            runtimeThumbnailObjects.Add(sprite);
            thumbnailCache[new ThumbnailCacheKey(pool, slotIndex)] = new CachedThumbnail(revision, sprite, texture);
            return sprite;
        }

        private bool TryGetCachedThumbnail(
            IClayModelSaveService saveService,
            ModelSavePool pool,
            int slotIndex,
            out Sprite sprite)
        {
            sprite = null;
            var key = new ThumbnailCacheKey(pool, slotIndex);
            if (!thumbnailCache.TryGetValue(key, out CachedThumbnail cached)
                || cached.Sprite == null)
            {
                return false;
            }

            long revision = ResolveThumbnailRevision(saveService, pool, slotIndex);
            if (cached.Revision != revision)
            {
                return false;
            }

            sprite = cached.Sprite;
            return true;
        }

        private bool TryGetCachedThumbnailSprite(
            ModelSavePool pool,
            int slotIndex,
            out Sprite sprite)
        {
            sprite = null;
            var key = new ThumbnailCacheKey(pool, slotIndex);
            if (!thumbnailCache.TryGetValue(key, out CachedThumbnail cached)
                || cached.Sprite == null)
            {
                return false;
            }

            sprite = cached.Sprite;
            return true;
        }

        private void EvictThumbnailCacheEntry(ModelSavePool pool, int slotIndex)
        {
            var key = new ThumbnailCacheKey(pool, slotIndex);
            if (!thumbnailCache.TryGetValue(key, out CachedThumbnail cached))
            {
                return;
            }

            thumbnailCache.Remove(key);
            DestroyCachedThumbnail(cached);
        }

        private void DestroyCachedThumbnail(CachedThumbnail cached)
        {
            if (cached.Sprite != null)
            {
                runtimeThumbnailObjects.Remove(cached.Sprite);
                Destroy(cached.Sprite);
            }

            if (cached.Texture != null)
            {
                runtimeThumbnailObjects.Remove(cached.Texture);
                Destroy(cached.Texture);
            }
        }

        private static long ResolveThumbnailRevision(
            IClayModelSaveService saveService,
            ModelSavePool pool,
            int slotIndex)
        {
            if (saveService == null)
            {
                return 0L;
            }

            ModelSaveSlot slot = saveService.GetSlot(pool, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.thumbnailFileName))
            {
                return 0L;
            }

            return ResolveThumbnailRevisionFromFileName(slot.thumbnailFileName);
        }

        private static long ResolveThumbnailRevisionFromFileName(string thumbnailFileName)
        {
            if (string.IsNullOrEmpty(thumbnailFileName))
            {
                return 0L;
            }

            // 書込直後も分かるよう永続領域の実体ファイルを優先する
            string writableRaw = ModelSaveStorage.GetWritablePath(thumbnailFileName);
            string writableCompressed = writableRaw + ".gz";
            string path = null;
            if (File.Exists(writableCompressed))
            {
                path = writableCompressed;
            }
            else if (File.Exists(writableRaw))
            {
                path = writableRaw;
            }
            else
            {
                path = ModelSaveStorage.ResolveReadPath(thumbnailFileName);
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return 0L;
            }

            try
            {
                var info = new FileInfo(path);
                return info.Length ^ info.LastWriteTimeUtc.Ticks;
            }
            catch (IOException)
            {
                return 0L;
            }
            catch (UnauthorizedAccessException)
            {
                return 0L;
            }
        }

        private readonly struct PendingThumbnailLoad
        {
            public PendingThumbnailLoad(int slotIndex, string fileName)
            {
                SlotIndex = slotIndex;
                FileName = fileName;
            }

            public int SlotIndex { get; }
            public string FileName { get; }
        }

        private void EnsureCells()
        {
            if (cells != null && cells.Length == SlotCount)
            {
                bool hasAny = false;
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] != null)
                    {
                        hasAny = true;
                        break;
                    }
                }

                if (hasAny)
                {
                    return;
                }
            }

            TrainedSaveSlotCellView[] found =
                GetComponentsInChildren<TrainedSaveSlotCellView>(true);
            if (found == null || found.Length == 0)
            {
                Debug.LogError(
                    "[TrainedSaveSlotGridView] cellsが未配線ですHierarchyで5×10のセルを接続してください",
                    this);
                cells = new TrainedSaveSlotCellView[SlotCount];
                return;
            }

            cells = new TrainedSaveSlotCellView[SlotCount];
            int count = Mathf.Min(found.Length, SlotCount);
            for (int i = 0; i < count; i++)
            {
                cells[i] = found[i];
            }

            if (found.Length < SlotCount)
            {
                Debug.LogError(
                    $"[TrainedSaveSlotGridView] セル数が不足しています need={SlotCount} found={found.Length}",
                    this);
            }
        }

        private Canvas ResolveOwnCanvas()
        {
            if (rootCanvas != null && rootCanvas.transform == transform)
            {
                return rootCanvas;
            }

            rootCanvas = GetComponent<Canvas>();
            return rootCanvas;
        }

        private void ClearRuntimeThumbnails()
        {
            thumbnailCache.Clear();
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
            hasCachedPool = false;
        }

        private readonly struct ThumbnailCacheKey : IEquatable<ThumbnailCacheKey>
        {
            public ThumbnailCacheKey(ModelSavePool pool, int slotIndex)
            {
                Pool = pool;
                SlotIndex = slotIndex;
            }

            public ModelSavePool Pool { get; }
            public int SlotIndex { get; }

            /// <inheritdoc/>
            public bool Equals(ThumbnailCacheKey other)
            {
                return Pool == other.Pool && SlotIndex == other.SlotIndex;
            }

            /// <inheritdoc/>
            public override bool Equals(object obj)
            {
                return obj is ThumbnailCacheKey other && Equals(other);
            }

            /// <inheritdoc/>
            public override int GetHashCode()
            {
                return ((int)Pool * 397) ^ SlotIndex;
            }
        }

        private readonly struct CachedThumbnail
        {
            public CachedThumbnail(long revision, Sprite sprite, Texture2D texture)
            {
                Revision = revision;
                Sprite = sprite;
                Texture = texture;
            }

            public long Revision { get; }
            public Sprite Sprite { get; }
            public Texture2D Texture { get; }
        }
    }
}
