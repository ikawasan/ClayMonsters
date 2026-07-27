using System;
using Extensions;
using SaveData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// CPU対戦紹介中に敵の強さ段階を選ぶUI
    /// </summary>
    public sealed class BattleEnemyStrengthSelectView : MonoBehaviour
    {
        private static readonly Color SelectedColor = new Color(1f, 0.86f, 0.42f, 1f);
        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 1f);

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

        private Action<EnemyStrengthTier> selectedListener;
        private EnemyStrengthTier currentTier = EnemyStrengthTier.Normal;
        private bool isExpectedVisible;

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
            ValidateRefs();
            selectedListener = onSelected;
            currentTier = initialTier;
            ApplySelectionVisual();
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
        }

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
            if (tier == currentTier)
            {
                return;
            }

            currentTier = tier;
            ApplySelectionVisual();
            selectedListener?.Invoke(tier);
        }

        private void ApplySelectionVisual()
        {
            ApplyButtonVisual(weakButton, currentTier == EnemyStrengthTier.Weak);
            ApplyButtonVisual(normalButton, currentTier == EnemyStrengthTier.Normal);
            ApplyButtonVisual(strongButton, currentTier == EnemyStrengthTier.Strong);
            ApplyButtonVisual(veryStrongButton, currentTier == EnemyStrengthTier.VeryStrong);
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

            Debug.LogError("[BattleEnemyStrengthSelectView] rootCanvasが未配線です", this);
        }

        private void ValidateRefs()
        {
            if (rootCanvas == null)
            {
                Debug.LogError("[BattleEnemyStrengthSelectView] rootCanvasが未配線です", this);
            }

            if (weakButton == null
                || normalButton == null
                || strongButton == null
                || veryStrongButton == null)
            {
                Debug.LogError("[BattleEnemyStrengthSelectView] 強さボタンが未配線です", this);
            }
        }
    }
}
