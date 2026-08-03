using System.Collections.Generic;
using ClayEditor.Rigging;
using Extensions;
using Localization;
using SaveData;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 保存確認画面で名前付きサムネイルと縦並びステータス・技スロットを表示する
    /// レイアウトはプレハブ配置を使い実行時はデータ反映のみ行う
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class ModelSaveConfirmView : MonoBehaviour, ILanguageAwareUi
    {
        private enum ShowMode
        {
            None,
            Preview,
            Slot,
            Empty
        }

        [SerializeField] private GameObject legacyMessageRoot;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private ModelSaveSlotRowElementRefs rowElementRefs;

        private Texture2D runtimeThumbnailTexture;
        private Sprite runtimeThumbnailSprite;
        private bool validated;
        private ShowMode showMode = ShowMode.None;
        private string cachedModelName = string.Empty;
        private ModelStatus cachedStatus;
        private IReadOnlyList<MotionType> cachedAttacks;
        private ModelSaveSlot cachedSlot;
        private ModelStatus cachedStatusOverride;
        private IReadOnlyList<MotionType> cachedAttackOverride;
        private byte[] cachedThumbnailPng;
        private int cachedSlotIndex;

        /// <summary>
        /// 新規保存前のプレビューを表示する
        /// </summary>
        public void ShowPreview(
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions,
            byte[] thumbnailPng)
        {
            showMode = ShowMode.Preview;
            cachedModelName = modelName ?? string.Empty;
            cachedStatus = status;
            cachedAttacks = attackMotions;
            cachedThumbnailPng = thumbnailPng;
            cachedSlot = null;
            cachedStatusOverride = null;
            cachedAttackOverride = null;

            ValidateSerializedReferences();
            SetLegacyMessageVisible(false);
            rowElementRefs?.BindConfirmPreview(cachedModelName, cachedStatus, cachedAttacks);
            ApplyThumbnail(cachedThumbnailPng);
        }

        /// <summary>
        /// 保存済みスロット内容を表示する
        /// </summary>
        public void ShowSlot(ModelSaveSlot slot, byte[] thumbnailPng, int slotIndex)
        {
            ShowSlot(slot, thumbnailPng, slotIndex, statusOverride: null);
        }

        /// <summary>
        /// 保存済みスロット内容を指定ステータスで表示する
        /// </summary>
        public void ShowSlot(
            ModelSaveSlot slot,
            byte[] thumbnailPng,
            int slotIndex,
            ModelStatus statusOverride)
        {
            ShowSlot(slot, thumbnailPng, slotIndex, statusOverride, attackOverride: null);
        }

        /// <summary>
        /// 保存済みスロット内容を指定ステータスと攻撃で表示する
        /// </summary>
        public void ShowSlot(
            ModelSaveSlot slot,
            byte[] thumbnailPng,
            int slotIndex,
            ModelStatus statusOverride,
            IReadOnlyList<MotionType> attackOverride)
        {
            cachedSlotIndex = slotIndex;
            cachedThumbnailPng = thumbnailPng;
            cachedSlot = slot;
            cachedStatusOverride = statusOverride;
            cachedAttackOverride = attackOverride;

            ValidateSerializedReferences();
            SetLegacyMessageVisible(false);
            if (slot == null || string.IsNullOrEmpty(slot.modelName))
            {
                showMode = ShowMode.Empty;
                rowElementRefs?.BindConfirmEmpty();
                ApplyThumbnail(null);
                return;
            }

            showMode = ShowMode.Slot;
            if (statusOverride != null || attackOverride != null)
            {
                ModelStatus status = statusOverride ?? slot.status;
                IReadOnlyList<MotionType> attacks = attackOverride ?? slot.attackMotions;
                rowElementRefs?.BindConfirmPreview(slot.modelName, status, attacks);
            }
            else
            {
                rowElementRefs?.BindConfirmFromSlot(slot);
            }

            ApplyThumbnail(thumbnailPng);
        }

        /// <summary>
        /// 表示をクリアする
        /// </summary>
        public void Clear()
        {
            showMode = ShowMode.None;
            ClearThumbnail();
            rowElementRefs?.BindConfirmEmpty();
            SetLegacyMessageVisible(true);
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            switch (showMode)
            {
                case ShowMode.Preview:
                    ShowPreview(cachedModelName, cachedStatus, cachedAttacks, cachedThumbnailPng);
                    break;
                case ShowMode.Slot:
                    ShowSlot(
                        cachedSlot,
                        cachedThumbnailPng,
                        cachedSlotIndex,
                        cachedStatusOverride,
                        cachedAttackOverride);
                    break;
                case ShowMode.Empty:
                    rowElementRefs?.BindConfirmEmpty();
                    break;
            }
        }

        private void OnDestroy()
        {
            ClearThumbnail();
        }

        private void ValidateSerializedReferences()
        {
            if (validated)
            {
                return;
            }

            validated = true;
            if (rowElementRefs == null)
            {
                Debug.LogError(
                    "[ModelSaveConfirmView] rowElementRefsが未配線ですEditorのWireでConfirmSlotRowを接続してください",
                    this);
            }
        }

        private void ApplyThumbnail(byte[] thumbnailPng)
        {
            ClearThumbnail();

            if (rowElementRefs == null)
            {
                return;
            }

            if (!RuntimeThumbnailUtility.TryCreate(
                    thumbnailPng,
                    out runtimeThumbnailTexture,
                    out runtimeThumbnailSprite))
            {
                rowElementRefs.ApplyConfirmThumbnail(null);
                return;
            }

            rowElementRefs.ApplyConfirmThumbnail(runtimeThumbnailSprite);
        }

        private void ClearThumbnail()
        {
            rowElementRefs?.ApplyConfirmThumbnail(null);
            RuntimeThumbnailUtility.Destroy(ref runtimeThumbnailSprite, ref runtimeThumbnailTexture);
        }

        private void SetLegacyMessageVisible(bool visible)
        {
            if (legacyMessageRoot != null)
            {
                legacyMessageRoot.SetActive(visible);
            }
        }
    }
}
