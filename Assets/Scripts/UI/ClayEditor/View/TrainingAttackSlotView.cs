using Battle;
using ClayEditor.Rigging;
using Extensions;
using Localization;
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
    public sealed class TrainingAttackSlotView : MonoBehaviour, ILanguageAwareUi
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

        private LocalizedBakedTextApplier partLabelApplier;
        private bool hasContent;
        private int cachedSlotNumber;
        private MotionType cachedAttack;
        private bool cachedPreserveLayoutSpace;

        private void Awake()
        {
            // プレハブ焼き込みの日本語範囲ラベルを現言語へ直す
            ApplyLocalizedChromeOnly();
        }

        /// <summary>
        /// 技スロット表示を更新する
        /// </summary>
        /// <param name="slotNumber">スロット番号</param>
        /// <param name="attack">攻撃</param>
        public void Apply(int slotNumber, MotionType attack, bool preserveLayoutSpace = false)
        {
            hasContent = true;
            cachedSlotNumber = slotNumber;
            cachedAttack = attack;
            cachedPreserveLayoutSpace = preserveLayoutSpace;

            if (preserveLayoutSpace)
            {
                ApplyForConfirmPrefab(slotNumber, attack);
                return;
            }

            _ = slotNumber;
            SetSlotRootVisible(true, preserveLayoutSpace);
            ApplyLocalizedChromeOnly();

            if (attackNameText != null)
            {
                LocalizedFont.SetText(
                    attackNameText,
                    MotionPartRequirement.FormatDisplayNameWithStrengthRank(attack));
            }

            if (damageText != null)
            {
                LocalizedFont.SetText(
                    damageText,
                    ModelSaveSummaryFormatter.FormatTrainingAttackDamageText(attack));
            }

            if (costText != null)
            {
                LocalizedFont.SetText(
                    costText,
                    ModelSaveSummaryFormatter.FormatTrainingAttackCostText(attack));
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
        }

        /// <summary>
        /// 表示内容をクリアする
        /// </summary>
        public void Clear(bool preserveLayoutSpace = false)
        {
            hasContent = false;
            if (preserveLayoutSpace)
            {
                ClearForConfirmPrefab();
                return;
            }

            ApplyLocalizedChromeOnly();

            if (attackNameText != null)
            {
                LocalizedFont.SetText(attackNameText, string.Empty);
            }

            if (damageText != null)
            {
                LocalizedFont.SetText(damageText, string.Empty);
            }

            if (costText != null)
            {
                LocalizedFont.SetText(costText, string.Empty);
            }

            if (requiredPartIcon != null)
            {
                ClearPartIcon(requiredPartIcon);
            }

            if (targetPartIcon != null)
            {
                ClearPartIcon(targetPartIcon);
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
            hasContent = true;
            cachedSlotNumber = slotNumber;
            cachedAttack = attack;
            cachedPreserveLayoutSpace = true;
            _ = slotNumber;
            ApplyLocalizedChromeOnly();

            if (attackNameText != null)
            {
                LocalizedFont.SetText(
                    attackNameText,
                    MotionPartRequirement.FormatDisplayNameWithStrengthRank(attack));
            }

            if (damageText != null)
            {
                LocalizedFont.SetText(
                    damageText,
                    ModelSaveSummaryFormatter.FormatTrainingAttackDamageText(attack));
            }

            if (costText != null)
            {
                LocalizedFont.SetText(
                    costText,
                    ModelSaveSummaryFormatter.FormatTrainingAttackCostText(attack));
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
        }

        /// <summary>
        /// 確認画面プレハブ向けに表示内容をクリアする
        /// HierarchyとRectTransformは変更しない
        /// </summary>
        public void ClearForConfirmPrefab()
        {
            hasContent = false;
            ApplyLocalizedChromeOnly();

            if (attackNameText != null)
            {
                LocalizedFont.SetText(attackNameText, string.Empty);
            }

            if (damageText != null)
            {
                LocalizedFont.SetText(damageText, string.Empty);
            }

            if (costText != null)
            {
                LocalizedFont.SetText(costText, string.Empty);
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


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            if (hasContent)
            {
                Apply(cachedSlotNumber, cachedAttack, cachedPreserveLayoutSpace);
                return;
            }

            ApplyLocalizedChromeOnly();
            if (attackNameText != null)
            {
                LocalizedFont.SetText(attackNameText, string.Empty);
            }
        }

        private void ApplyLocalizedChromeOnly()
        {
            ApplyPartChromeLabels();
            if (rangeLabelText != null)
            {
                LocalizedFixedChromeLabel.ApplyFixedRectLabel(
                    rangeLabelText,
                    GameTextKeys.SaveSummaryRangeLabel,
                    "範囲:");
            }
        }

        private void ApplyPartChromeLabels()
        {
            if (partLabelApplier == null)
            {
                partLabelApplier = new LocalizedBakedTextApplier();
                partLabelApplier.Register(GameTextKeys.BattleUsePartLabel, "使用部位:");
                partLabelApplier.Register(GameTextKeys.BattleBreakPartLabel, "破壊部位:");
                partLabelApplier.Capture(transform);
            }

            partLabelApplier.Apply();
        }

        private void ClearPartIcon(Image iconImage)
        {
            Image resolvedIcon = ResolvePartIconImage(iconImage);
            if (resolvedIcon == null)
            {
                return;
            }

            resolvedIcon.sprite = null;
            resolvedIcon.enabled = false;
            resolvedIcon.color = new Color(1f, 1f, 1f, 0f);
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
            DisablePartIconFrame(resolvedIcon);
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

        private static void DisablePartIconFrame(Image iconImage)
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

            frameImage.enabled = false;
            frameImage.sprite = null;
            frameImage.color = new Color(1f, 1f, 1f, 0f);
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
                preserveSegmentHierarchy: false);
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
