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
    public sealed class ModelSaveSlotDeletePromptView : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private LHButton confirmButton;
        [SerializeField] private LHButton cancelButton;

        private Action pendingConfirmAction;

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
            if (messageText != null)
            {
                string safeName = string.IsNullOrEmpty(modelName)
                    ? LocalizedText.GetOrFallback(GameTextKeys.SaveUnnamedModel, "名称未設定")
                    : modelName;
                messageText.text = LocalizedText.Get(GameTextKeys.TrainingDeleteConfirm, new System.Collections.Generic.Dictionary<string, object> { { "slot", slotIndex + 1 }, { "name", safeName } });
            }

            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }

        /// <summary>
        /// 削除確認ダイアログを閉じる
        /// </summary>
        public void Hide()
        {
            pendingConfirmAction = null;
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
