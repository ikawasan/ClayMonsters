using ClayEditor.Rigging;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.TrainingScene.Domain;
using System;
using System.Collections.Generic;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.UI;

using Localization;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 攻撃入れ替えUI
    /// 習得技と入れ替え候補をシーン配置済みスロットへ反映する
    /// </summary>
    public sealed class TrainingAttackSwapChoicesView : MonoBehaviour
    {
        public const int SwapSlotCount = 4;

        [SerializeField] private TMP_Text learnedHeaderText;
        [SerializeField] private TrainingAttackSlotView learnedAttackSlot;
        [SerializeField] private TMP_Text choicesHeaderText;
        [SerializeField] private TrainingAttackSwapSlotView[] swapSlots = new TrainingAttackSwapSlotView[SwapSlotCount];
        [SerializeField] private LHButton skipButton;

        /// <summary>
        /// 入れ替え候補を表示する
        /// </summary>
        /// <param name="learnedAttack">習得する攻撃</param>
        /// <param name="currentAttacks">現在の攻撃一覧</param>
        /// <param name="onSelected">選択時コールバック スロット0〜3 入れ替えないは-1</param>
        public void Show(
            MotionType learnedAttack,
            IReadOnlyList<MotionType> currentAttacks,
            Action<int> onSelected)
        {
            EnsureSwapSlotsResolved();

            if (learnedHeaderText != null)
            {
                learnedHeaderText.gameObject.SetActive(true);
                learnedHeaderText.text = LocalizedText.Get(GameTextKeys.TrainingLearnedHeader);
            }

            if (learnedAttackSlot != null)
            {
                learnedAttackSlot.Apply(0, learnedAttack);
            }

            if (choicesHeaderText != null)
            {
                choicesHeaderText.gameObject.SetActive(true);
                choicesHeaderText.text = LocalizedText.Get(GameTextKeys.TrainingSwapHeader);
            }

            int slotCount = currentAttacks != null
                ? Mathf.Min(currentAttacks.Count, TrainingSettings.AttackSlotCount, SwapSlotCount)
                : 0;

            for (int i = 0; i < SwapSlotCount; i++)
            {
                TrainingAttackSwapSlotView slotView = swapSlots != null && i < swapSlots.Length
                    ? swapSlots[i]
                    : null;
                if (slotView == null)
                {
                    continue;
                }

                if (i < slotCount)
                {
                    slotView.Apply(i + 1, currentAttacks[i]);
                    BindSelectButton(slotView.SelectButton, i, onSelected);
                }
                else
                {
                    slotView.Hide();
                }
            }

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.EnsureUiSoundFeedback();
                DisableChildRaycasts(skipButton);
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() => onSelected?.Invoke(-1));
            }
        }

        /// <summary>
        /// 表示内容をクリアする
        /// </summary>
        public void Clear()
        {
            EnsureSwapSlotsResolved();

            if (learnedHeaderText != null)
            {
                learnedHeaderText.gameObject.SetActive(false);
            }

            if (learnedAttackSlot != null)
            {
                learnedAttackSlot.Clear();
            }

            if (choicesHeaderText != null)
            {
                choicesHeaderText.gameObject.SetActive(false);
            }

            if (swapSlots != null)
            {
                for (int i = 0; i < swapSlots.Length; i++)
                {
                    swapSlots[i]?.Hide();
                }
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.gameObject.SetActive(false);
            }
        }

        private void EnsureSwapSlotsResolved()
        {
            if (swapSlots != null && swapSlots.Length >= SwapSlotCount)
            {
                bool hasMissing = false;
                for (int i = 0; i < SwapSlotCount; i++)
                {
                    if (swapSlots[i] == null)
                    {
                        hasMissing = true;
                        break;
                    }
                }

                if (!hasMissing)
                {
                    return;
                }
            }

            TrainingAttackSwapSlotView[] found =
                GetComponentsInChildren<TrainingAttackSwapSlotView>(true);
            if (found.Length == 0)
            {
                return;
            }

            System.Array.Sort(
                found,
                (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

            if (swapSlots == null || swapSlots.Length != SwapSlotCount)
            {
                swapSlots = new TrainingAttackSwapSlotView[SwapSlotCount];
            }

            for (int i = 0; i < SwapSlotCount && i < found.Length; i++)
            {
                if (swapSlots[i] == null)
                {
                    swapSlots[i] = found[i];
                }
            }
        }

        private static void BindSelectButton(LHButton button, int slotIndex, Action<int> onSelected)
        {
            if (button == null)
            {
                return;
            }

            button.EnsureUiSoundFeedback();
            DisableChildRaycasts(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelected?.Invoke(slotIndex));
        }

        private static void DisableChildRaycasts(LHButton button)
        {
            if (button == null)
            {
                return;
            }

            Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic.gameObject == button.gameObject)
                {
                    continue;
                }

                graphic.raycastTarget = false;
            }
        }
    }
}
