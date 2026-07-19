using System.Collections.Generic;
using ClayEditor.Rigging;
using Extensions;
using SaveData;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 保存確認画面で名前付きサムネイルと縦並びステータス・技スロットを表示する
    /// レイアウトはプレハブ配置を使い実行時はデータ反映のみ行う
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class ModelSaveConfirmView : MonoBehaviour
    {
        [SerializeField] private GameObject legacyMessageRoot;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private ModelSaveSlotRowElementRefs rowElementRefs;

        private Texture2D runtimeThumbnailTexture;
        private Sprite runtimeThumbnailSprite;
        private bool validated;

        /// <summary>
        /// 新規保存前のプレビューを表示する
        /// </summary>
        public void ShowPreview(
            string modelName,
            ModelStatus status,
            IReadOnlyList<MotionType> attackMotions,
            byte[] thumbnailPng)
        {
            ValidateSerializedReferences();
            SetLegacyMessageVisible(false);
            rowElementRefs?.BindConfirmPreview(modelName, status, attackMotions);
            ApplyThumbnail(thumbnailPng);
        }

        /// <summary>
        /// 保存済みスロット内容を表示する
        /// </summary>
        public void ShowSlot(ModelSaveSlot slot, byte[] thumbnailPng, int slotIndex)
        {
            _ = slotIndex;
            ValidateSerializedReferences();
            SetLegacyMessageVisible(false);
            rowElementRefs?.BindConfirmFromSlot(slot);
            ApplyThumbnail(thumbnailPng);
        }

        /// <summary>
        /// 表示をクリアする
        /// </summary>
        public void Clear()
        {
            ClearThumbnail();
            rowElementRefs?.BindConfirmEmpty();
            SetLegacyMessageVisible(true);
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
