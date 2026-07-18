using Battle;
using ClayEditor.Rigging;
using Extensions;
using TMPro;
using UI.Battle.View;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 育成確認画面とセーブスロット向け技スロット表示
    /// シーン配置済みUIへ攻撃名・部位アイコン・ダメージ・コスト・範囲を反映する
    /// </summary>
    public sealed class TrainingAttackSlotView : MonoBehaviour
    {
        [Header("Texts")]
        [Tooltip("攻撃名を表示するテキスト")]
        [SerializeField] private TMP_Text attackNameText;
        [Tooltip("ダメージ値を表示するテキスト(ダメージ:)")]
        [SerializeField] private TMP_Text damageText;
        [Tooltip("コスト値を表示するテキスト(コスト:)")]
        [SerializeField] private TMP_Text costText;
        [Tooltip("範囲ラベルテキスト(範囲:)")]
        [SerializeField] private TMP_Text rangeLabelText;

        [Header("Part Icons")]
        [Tooltip("使用部位アイコン")]
        [SerializeField] private Image requiredPartIcon;
        [Tooltip("破壊部位アイコン")]
        [SerializeField] private Image targetPartIcon;

        [Header("Range")]
        [Tooltip("範囲ゲージ(範囲:の右隣に置く)")]
        [SerializeField] private MoveRangeSegmentBarView rangeBar;

        /// <summary>
        /// 技スロット表示を更新する
        /// </summary>
        /// <param name="slotNumber">スロット番号</param>
        /// <param name="attack">攻撃</param>
        public void Apply(int slotNumber, MotionType attack, bool preserveLayoutSpace = false)
        {
            if (preserveLayoutSpace)
            {
                ApplyForConfirmPrefab(slotNumber, attack);
                return;
            }

            _ = slotNumber;
            SetSlotRootVisible(true, preserveLayoutSpace);

            if (attackNameText != null)
            {
                attackNameText.text = MotionPartRequirement.GetDisplayName(attack);
            }

            if (damageText != null)
            {
                damageText.text = ModelSaveSummaryFormatter.FormatTrainingAttackDamageText(attack);
            }

            if (costText != null)
            {
                costText.text = ModelSaveSummaryFormatter.FormatTrainingAttackCostText(attack);
            }

            if (rangeLabelText != null)
            {
                rangeLabelText.text = ModelSaveSummaryFormatter.TrainingAttackRangeLabel;
            }

            if (requiredPartIcon != null)
            {
                MoveTargetPartIconUtility.ApplyRequiredPartIcon(requiredPartIcon, attack, preserveLayoutSpace);
            }

            if (targetPartIcon != null)
            {
                MoveTargetPartIconUtility.ApplyTargetPartIcon(targetPartIcon, attack, preserveLayoutSpace);
            }

            ApplyRange(attack);
            DisableContentRaycasts();

            if (preserveLayoutSpace)
            {
                RefreshPartInfoRowLayout();
            }
        }

        /// <summary>
        /// 表示内容をクリアする
        /// </summary>
        public void Clear(bool preserveLayoutSpace = false)
        {
            if (preserveLayoutSpace)
            {
                ClearForConfirmPrefab();
                return;
            }

            if (attackNameText != null)
            {
                attackNameText.text = string.Empty;
            }

            if (damageText != null)
            {
                damageText.text = string.Empty;
            }

            if (costText != null)
            {
                costText.text = string.Empty;
            }

            if (rangeLabelText != null)
            {
                rangeLabelText.text = ModelSaveSummaryFormatter.TrainingAttackRangeLabel;
            }

            if (requiredPartIcon != null)
            {
                requiredPartIcon.sprite = null;
                if (preserveLayoutSpace)
                {
                    requiredPartIcon.enabled = false;
                }
            }

            if (targetPartIcon != null)
            {
                targetPartIcon.sprite = null;
                if (preserveLayoutSpace)
                {
                    targetPartIcon.enabled = false;
                }
            }

            if (rangeBar != null)
            {
                rangeBar.Apply(
                    0f,
                    0f,
                    MoveRangeSegmentBarView.DefaultMaxDistance,
                    usable: false);
            }

            SetSlotRootVisible(false, preserveLayoutSpace);
        }

        /// <summary>
        /// 確認画面プレハブ向けに技スロット表示を更新する
        /// HierarchyとRectTransformは変更しない
        /// </summary>
        /// <param name="slotNumber">スロット番号</param>
        /// <param name="attack">攻撃</param>
        public void ApplyForConfirmPrefab(int slotNumber, MotionType attack)
        {
            _ = slotNumber;

            if (attackNameText != null)
            {
                attackNameText.text = MotionPartRequirement.GetDisplayName(attack);
            }

            if (damageText != null)
            {
                damageText.text = ModelSaveSummaryFormatter.FormatTrainingAttackDamageText(attack);
            }

            if (costText != null)
            {
                costText.text = ModelSaveSummaryFormatter.FormatTrainingAttackCostText(attack);
            }

            if (rangeLabelText != null)
            {
                rangeLabelText.text = ModelSaveSummaryFormatter.TrainingAttackRangeLabel;
            }

            if (requiredPartIcon != null)
            {
                MoveTargetPartIconUtility.ApplyRequiredPartIcon(requiredPartIcon, attack, preserveGameObjectActive: true);
            }

            if (targetPartIcon != null)
            {
                MoveTargetPartIconUtility.ApplyTargetPartIcon(targetPartIcon, attack, preserveGameObjectActive: true);
            }

            ApplyRangeForConfirmPrefab(attack);
            RefreshPartInfoRowLayout();
        }

        /// <summary>
        /// 確認画面プレハブ向けに表示内容をクリアする
        /// HierarchyとRectTransformは変更しない
        /// </summary>
        public void ClearForConfirmPrefab()
        {
            if (attackNameText != null)
            {
                attackNameText.text = string.Empty;
            }

            if (damageText != null)
            {
                damageText.text = string.Empty;
            }

            if (costText != null)
            {
                costText.text = string.Empty;
            }

            if (rangeLabelText != null)
            {
                rangeLabelText.text = ModelSaveSummaryFormatter.TrainingAttackRangeLabel;
            }

            if (requiredPartIcon != null)
            {
                ClearPartIconForConfirmPrefab(requiredPartIcon);
            }

            if (targetPartIcon != null)
            {
                ClearPartIconForConfirmPrefab(targetPartIcon);
            }

            if (rangeBar != null)
            {
                rangeBar.Apply(
                    0f,
                    0f,
                    MoveRangeSegmentBarView.DefaultMaxDistance,
                    usable: false,
                    preserveSegmentHierarchy: true);
            }
        }

        private void ClearPartIconForConfirmPrefab(Image iconImage)
        {
            Image resolvedIcon = ResolvePartIconImage(iconImage);
            if (resolvedIcon == null)
            {
                return;
            }

            resolvedIcon.sprite = null;
            resolvedIcon.enabled = false;
            SetPartIconFrameEnabled(resolvedIcon, false);
        }

        private static Image ResolvePartIconImage(Image iconImage)
        {
            if (iconImage == null)
            {
                return null;
            }

            if (iconImage.gameObject.name == "Icon")
            {
                return iconImage;
            }

            Transform iconTransform = iconImage.transform.Find("Icon");
            if (iconTransform != null && iconTransform.TryGetComponent(out Image childIcon))
            {
                return childIcon;
            }

            return iconImage;
        }

        private static void SetPartIconFrameEnabled(Image iconImage, bool enabled)
        {
            if (iconImage == null)
            {
                return;
            }

            Transform frameTransform = iconImage.transform.parent;
            if (frameTransform == null || !frameTransform.TryGetComponent(out Image frameImage))
            {
                return;
            }

            if (frameImage == iconImage)
            {
                return;
            }

            frameImage.enabled = enabled;
        }

        private void RefreshPartInfoRowLayout()
        {
            Transform partInfoRow = transform.Find("PartInfoRow");
            if (partInfoRow == null)
            {
                return;
            }

            TMP_Text[] labels = partInfoRow.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                label.ForceMeshUpdate();
                if (label.TryGetComponent(out LayoutElement layoutElement))
                {
                    layoutElement.preferredWidth = label.preferredWidth;
                }
            }

            Canvas.ForceUpdateCanvases();
        }

        private void ApplyRangeForConfirmPrefab(MotionType attack)
        {
            if (rangeBar == null)
            {
                return;
            }

            Vector2 range = MotionPartRequirement.GetRange(attack);
            rangeBar.Apply(
                range.x,
                range.y,
                MoveRangeSegmentBarView.DefaultMaxDistance,
                usable: true,
                preserveSegmentHierarchy: true);
        }

        private void SetSlotRootVisible(bool visible, bool preserveLayoutSpace)
        {
            if (preserveLayoutSpace)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                SetSlotContentVisible(visible);
                return;
            }

            gameObject.SetActive(visible);
        }

        private void SetSlotContentVisible(bool visible)
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(canvas, visible);
                return;
            }

            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = false;
                return;
            }

            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].enabled = visible;
            }
        }

        private void ApplyRange(MotionType attack)
        {
            if (rangeBar == null)
            {
                return;
            }

            Vector2 range = MotionPartRequirement.GetRange(attack);
            rangeBar.Apply(
                range.x,
                range.y,
                MoveRangeSegmentBarView.DefaultMaxDistance,
                usable: true);
        }

        private void DisableContentRaycasts()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }
}
