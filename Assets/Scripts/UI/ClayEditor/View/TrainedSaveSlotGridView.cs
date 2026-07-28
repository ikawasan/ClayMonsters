using Extensions;
using SaveData;
using SaveData.Interface;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 育成済みセーブスロットを5列×10行で並べるグリッド一覧
    /// </summary>
    public sealed class TrainedSaveSlotGridView : MonoBehaviour
    {
        public const int ColumnCount = ModelSavePoolSettings.TrainedGridColumnCount;
        public const int RowCount = ModelSavePoolSettings.TrainedGridRowCount;
        public const int SlotCount = ModelSavePoolSettings.TrainedSlotCount;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private TrainedSaveSlotCellView[] cells =
            new TrainedSaveSlotCellView[SlotCount];

        private readonly List<UnityEngine.Object> runtimeThumbnailObjects = new List<UnityEngine.Object>();
        private Action<int> onSlotSelected;
        private Action<int> onSlotPointerEnter;
        private Action onSlotPointerExit;
        private bool isClickBound;

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
            EnsureCells();
            ClearRuntimeThumbnails();
            if (saveService == null)
            {
                Debug.LogError(
                    "[TrainedSaveSlotGridView] saveServiceがnullです",
                    this);
                return;
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
                    cell.BindEmpty(i, emptySlotLabel, false);
                    continue;
                }

                ModelSaveSlot slot = saveService.GetSlot(pool, i);
                bool used = slot != null
                    && slot.isUsed
                    && !string.IsNullOrEmpty(slot.glbFileName);
                if (!used)
                {
                    cell.BindEmpty(i, emptySlotLabel, allowEmptySlotSelection);
                    continue;
                }

                Sprite thumbnail = LoadThumbnailSprite(saveService, pool, i);
                bool interactable = isSlotUnlocked == null || isSlotUnlocked(i);
                cell.BindUsed(i, slot, thumbnail, interactable);
            }
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
            return sprite;
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
