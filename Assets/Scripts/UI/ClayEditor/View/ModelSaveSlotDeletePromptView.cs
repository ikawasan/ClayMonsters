using Localization;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using System;
using TMPro;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// セーブスロット削除の最終確認ダイアログ
    /// </summary>
    public sealed class ModelSaveSlotDeletePromptView : MonoBehaviour, ILanguageAwareUi
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton confirmButton;
        [SerializeField] private LHButton cancelButton;

        private Action pendingConfirmAction;
        private string cachedModelName = string.Empty;
        private int cachedSlotIndex;
        private bool isShowing;
        private LocalizedBakedTextApplier bakedLabelApplier;

        private void Awake()
        {
            if (confirmButton != null)
            {
                confirmButton.SubscribeOnClick(OnConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.SubscribeOnClick(Hide);
            }

            ApplyButtonLabels();
            Hide();
        }

        /// <summary>
        /// 削除確認ダイアログを表示する
        /// </summary>
        /// <param name="modelName">削除対象モデル名</param>
        /// <param name="slotIndex">削除対象スロット番号</param>
        /// <param name="onConfirmed">削除確定時コールバック</param>
        public void Show(string modelName, int slotIndex, Action onConfirmed)
        {
            pendingConfirmAction = onConfirmed;
            cachedModelName = modelName ?? string.Empty;
            cachedSlotIndex = slotIndex;
            isShowing = true;
            ApplyCopy();

            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            // 非表示中もボタン原文が残らないよう常に文言を差替える
            ApplyButtonLabels();
            if (isShowing)
            {
                ApplyMessage();
            }
        }

        private void ApplyCopy()
        {
            ApplyMessage();
            ApplyButtonLabels();
        }

        private void ApplyMessage()
        {
            if (messageText == null)
            {
                return;
            }

            string safeName = string.IsNullOrEmpty(cachedModelName)
                ? LocalizedText.GetOrFallback(GameTextKeys.SaveUnnamedModel, "名称未設定")
                : cachedModelName;
            messageText.text = LocalizedText.Get(
                GameTextKeys.TrainingDeleteConfirm,
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "slot", cachedSlotIndex + 1 },
                    { "name", safeName },
                });
        }

        private void ApplyButtonLabels()
        {
            EnsureBakedLabels();
            bakedLabelApplier?.Apply();
            LhButtonLabelUtility.SetLabel(
                confirmButton,
                LocalizedText.GetOrFallback(GameTextKeys.ClayEditDeleteConfirm, "削除"));
            LhButtonLabelUtility.SetLabel(
                cancelButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonCancel, "キャンセル"));
        }

        private void EnsureBakedLabels()
        {
            if (bakedLabelApplier != null)
            {
                return;
            }

            Transform root = canvas != null ? canvas.transform : transform;
            bakedLabelApplier = new LocalizedBakedTextApplier();
            bakedLabelApplier.Register(GameTextKeys.ClayEditDeleteConfirm, "削除");
            bakedLabelApplier.Register(GameTextKeys.ClayEditDeleteConfirm, "削除する");
            bakedLabelApplier.Register(GameTextKeys.CommonCancel, "キャンセル");
            bakedLabelApplier.Register(GameTextKeys.CommonCancel, "取消");
            bakedLabelApplier.Capture(root);
        }

        /// <summary>
        /// 削除確認ダイアログを閉じる
        /// </summary>
        public void Hide()
        {
            pendingConfirmAction = null;
            isShowing = false;
            if (canvas != null)
            {
                canvas.enabled = false;
            }
        }

        private void OnConfirmClicked()
        {
            Action action = pendingConfirmAction;
            Hide();
            action?.Invoke();
        }
    }
}
