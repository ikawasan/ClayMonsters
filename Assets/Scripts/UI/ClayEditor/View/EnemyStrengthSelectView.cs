using System;
using Extensions;
using SaveData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 敵の強さ段階を選ぶUI
    /// </summary>
    public class EnemyStrengthSelectView : MonoBehaviour
    {
        private static readonly Color SelectedColor = new Color(1f, 0.86f, 0.42f, 1f);
        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color LockedButtonColor = new Color(0f, 0f, 0f, 1f);

        [Header("表示制御")]
        [SerializeField] private Canvas rootCanvas;

        [Header("ボタン")]
        [SerializeField] private Button weakButton;
        [SerializeField] private Button normalButton;
        [SerializeField] private Button strongButton;
        [SerializeField] private Button veryStrongButton;

        [Header("ラベル")]
        [SerializeField] private TMP_Text weakLabel;
        [SerializeField] private TMP_Text normalLabel;
        [SerializeField] private TMP_Text strongLabel;
        [SerializeField] private TMP_Text veryStrongLabel;

        [Header("ロック表示")]
        [SerializeField] private Image weakLockDimmer;
        [SerializeField] private Image weakLockIcon;
        [SerializeField] private Image normalLockDimmer;
        [SerializeField] private Image normalLockIcon;
        [SerializeField] private Image strongLockDimmer;
        [SerializeField] private Image strongLockIcon;
        [SerializeField] private Image veryStrongLockDimmer;
        [SerializeField] private Image veryStrongLockIcon;

        private Action<EnemyStrengthTier> selectedListener;
        private EnemyStrengthTier currentTier = EnemyStrengthTier.Normal;
        private bool isExpectedVisible;
        private int unlockedTierMask = -1;

        private void Awake()
        {
            ValidateRefs();
            BindButton(weakButton, EnemyStrengthTier.Weak);
            BindButton(normalButton, EnemyStrengthTier.Normal);
            BindButton(strongButton, EnemyStrengthTier.Strong);
            BindButton(veryStrongButton, EnemyStrengthTier.VeryStrong);
            ApplyLabels();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            UnbindButton(weakButton);
            UnbindButton(normalButton);
            UnbindButton(strongButton);
            UnbindButton(veryStrongButton);
            selectedListener = null;
        }

        /// <summary>
        /// 強さ選択UIを表示し選択通知を受け取る
        /// </summary>
        /// <param name="initialTier">初期選択</param>
        /// <param name="onSelected">選択時コールバック</param>
        public void Show(EnemyStrengthTier initialTier, Action<EnemyStrengthTier> onSelected)
        {
            Show(initialTier, onSelected, isTierUnlocked: null);
        }

        /// <summary>
        /// 強さ選択UIを表示し選択通知を受け取る
        /// </summary>
        /// <param name="initialTier">初期選択</param>
        /// <param name="onSelected">選択時コールバック</param>
        /// <param name="isTierUnlocked">開放判定(nullなら全て開放)</param>
        public void Show(
            EnemyStrengthTier initialTier,
            Action<EnemyStrengthTier> onSelected,
            Func<EnemyStrengthTier, bool> isTierUnlocked)
        {
            ValidateRefs();
            selectedListener = onSelected;
            ApplyUnlockMask(isTierUnlocked);
            currentTier = ResolveSelectableTier(initialTier);
            ApplySelectionVisual();
            ApplyButtonInteractable();
            ApplyLockVisuals();
            SetVisible(true);
        }

        /// <summary>
        /// 強さ選択UIを隠す
        /// </summary>
        public void Hide()
        {
            selectedListener = null;
            SetVisible(false);
        }

        /// <summary>
        /// 表示中なら選択中の段階を更新する
        /// </summary>
        /// <param name="tier">強さ段階</param>
        public void SetSelected(EnemyStrengthTier tier)
        {
            currentTier = tier;
            ApplySelectionVisual();
            ApplyLockVisuals();
        }

        /// <summary>
        /// 現在選択中の強さ段階
        /// </summary>
        public EnemyStrengthTier CurrentTier => currentTier;

        /// <summary>
        /// 表示期待状態を保ったままCanvasだけ切り替える
        /// </summary>
        /// <param name="visible">表示するか</param>
        public void SetVisible(bool visible)
        {
            isExpectedVisible = visible;
            ApplyCanvasVisible(visible);
        }

        /// <summary>
        /// 詳細パネルなど背面非表示中に一時的に隠す
        /// </summary>
        /// <param name="visible">表示するか</param>
        public void SetTemporaryVisible(bool visible)
        {
            ApplyCanvasVisible(isExpectedVisible && visible);
        }

        private void BindButton(Button button, EnemyStrengthTier tier)
        {
            if (button == null)
            {
                return;
            }

            button.EnsureUiSoundFeedback();
            button.onClick.AddListener(() => OnClickTier(tier));
        }

        private void UnbindButton(Button button)
        {
            button?.onClick.RemoveAllListeners();
        }

        private void OnClickTier(EnemyStrengthTier tier)
        {
            if (!IsTierUnlocked(tier))
            {
                return;
            }

            if (tier == currentTier)
            {
                return;
            }

            currentTier = tier;
            ApplySelectionVisual();
            ApplyLockVisuals();
            selectedListener?.Invoke(tier);
        }

        private void ApplyUnlockMask(Func<EnemyStrengthTier, bool> isTierUnlocked)
        {
            if (isTierUnlocked == null)
            {
                unlockedTierMask = -1;
                return;
            }

            int mask = 0;
            for (int i = 0; i < NpcBattleProgressRules.TierCount; i++)
            {
                var tier = (EnemyStrengthTier)i;
                if (isTierUnlocked(tier))
                {
                    mask |= 1 << i;
                }
            }

            unlockedTierMask = mask;
        }

        private bool IsTierUnlocked(EnemyStrengthTier tier)
        {
            if (unlockedTierMask < 0)
            {
                return true;
            }

            return (unlockedTierMask & (1 << (int)tier)) != 0;
        }

        private EnemyStrengthTier ResolveSelectableTier(EnemyStrengthTier preferred)
        {
            if (IsTierUnlocked(preferred))
            {
                return preferred;
            }

            for (int i = NpcBattleProgressRules.TierCount - 1; i >= 0; i--)
            {
                var tier = (EnemyStrengthTier)i;
                if (IsTierUnlocked(tier))
                {
                    return tier;
                }
            }

            return EnemyStrengthTier.Weak;
        }

        private void ApplyButtonInteractable()
        {
            SetButtonInteractable(weakButton, IsTierUnlocked(EnemyStrengthTier.Weak));
            SetButtonInteractable(normalButton, IsTierUnlocked(EnemyStrengthTier.Normal));
            SetButtonInteractable(strongButton, IsTierUnlocked(EnemyStrengthTier.Strong));
            SetButtonInteractable(veryStrongButton, IsTierUnlocked(EnemyStrengthTier.VeryStrong));
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void ApplySelectionVisual()
        {
            ApplyButtonVisual(weakButton, currentTier == EnemyStrengthTier.Weak);
            ApplyButtonVisual(normalButton, currentTier == EnemyStrengthTier.Normal);
            ApplyButtonVisual(strongButton, currentTier == EnemyStrengthTier.Strong);
            ApplyButtonVisual(veryStrongButton, currentTier == EnemyStrengthTier.VeryStrong);
        }

        private void ApplyLockVisuals()
        {
            ApplyTierLock(
                weakButton,
                weakLockDimmer,
                weakLockIcon,
                EnemyStrengthTier.Weak);
            ApplyTierLock(
                normalButton,
                normalLockDimmer,
                normalLockIcon,
                EnemyStrengthTier.Normal);
            ApplyTierLock(
                strongButton,
                strongLockDimmer,
                strongLockIcon,
                EnemyStrengthTier.Strong);
            ApplyTierLock(
                veryStrongButton,
                veryStrongLockDimmer,
                veryStrongLockIcon,
                EnemyStrengthTier.VeryStrong);
        }

        private void ApplyTierLock(
            Button button,
            Image dimmer,
            Image icon,
            EnemyStrengthTier tier)
        {
            bool locked = !IsTierUnlocked(tier);
            if (locked && (dimmer == null || icon == null))
            {
                Debug.LogError(
                    "[EnemyStrengthSelectView] 強さロック用Imageが未配線ですPrefab Modeで配置し接続してください",
                    this);
            }

            if (dimmer != null)
            {
                dimmer.enabled = locked;
                if (locked)
                {
                    dimmer.color = new Color(0f, 0f, 0f, 0.82f);
                    dimmer.raycastTarget = false;
                }
            }

            if (icon != null)
            {
                if (locked)
                {
                    if (icon.sprite == null)
                    {
                        Sprite sprite = UiLockIconResources.GetSprite();
                        if (sprite != null)
                        {
                            icon.sprite = sprite;
                        }
                    }

                    icon.color = Color.white;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    icon.enabled = true;
                }
                else
                {
                    icon.enabled = false;
                }
            }

            if (locked && button != null && button.targetGraphic != null)
            {
                button.targetGraphic.color = LockedButtonColor;
            }
        }

        private static void ApplyButtonVisual(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = selected ? SelectedColor : NormalColor;
            colors.highlightedColor = selected ? SelectedColor : NormalColor;
            colors.selectedColor = selected ? SelectedColor : NormalColor;
            button.colors = colors;

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = selected ? SelectedColor : NormalColor;
            }
        }

        private void ApplyLabels()
        {
            SetLabel(weakLabel, EnemyStrengthTier.Weak);
            SetLabel(normalLabel, EnemyStrengthTier.Normal);
            SetLabel(strongLabel, EnemyStrengthTier.Strong);
            SetLabel(veryStrongLabel, EnemyStrengthTier.VeryStrong);
        }

        private static void SetLabel(TMP_Text label, EnemyStrengthTier tier)
        {
            if (label != null)
            {
                label.text = EnemyStrengthStatusCatalog.GetDisplayName(tier);
            }
        }

        private void ApplyCanvasVisible(bool visible)
        {
            if (rootCanvas != null)
            {
                rootCanvas.enabled = visible;
                return;
            }

            Debug.LogError("[EnemyStrengthSelectView] rootCanvasが未配線です", this);
        }

        private void ValidateRefs()
        {
            if (rootCanvas == null)
            {
                Debug.LogError("[EnemyStrengthSelectView] rootCanvasが未配線です", this);
            }

            if (weakButton == null
                || normalButton == null
                || strongButton == null
                || veryStrongButton == null)
            {
                Debug.LogError("[EnemyStrengthSelectView] 強さボタンが未配線です", this);
            }
        }
    }
}
