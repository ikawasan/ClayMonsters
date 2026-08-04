using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using Scene.TrainingScene.Domain;
using Scene.TrainingScene.Interface;
using System.Threading;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 強敵急襲のアラート演出と戦う逃げる選択ウィンドウ
    /// </summary>
    public sealed class TrainingAmbushView : MonoBehaviour, ITrainingAmbushView, ILanguageAwareUi
    {
        private const int AlertSortingOrder = 920;
        private const int ChoiceSortingOrder = 930;

        [Header("Alert")]
        [SerializeField] private Canvas alertCanvas;
        [SerializeField] private Image alertFlashImage;
        [SerializeField] private TMP_Text alertTitleText;

        [Header("Choice")]
        [SerializeField] private Canvas choiceCanvas;
        [SerializeField] private Image choiceBlocker;
        [SerializeField] private TMP_Text choiceTitleText;
        [SerializeField] private TMP_Text choiceMessageText;
        [SerializeField] private LHButton fightButton;
        [SerializeField] private LHButton fleeButton;

        [Header("AlertTiming")]
        [SerializeField] private float alertPopSeconds = 0.4f;
        [SerializeField] private float alertHoldSeconds = 0.75f;
        [SerializeField] private float alertHideSeconds = 0.2f;

        private bool hasChoice;
        private TrainingAmbushChoice selectedChoice = TrainingAmbushChoice.Flee;
        private bool uiBound;
        private string cachedEnemyName = string.Empty;
        private bool choiceVisible;

        private void Awake()
        {
            EnsureUiBound();
            Hide();
        }

        /// <inheritdoc/>
        public async UniTask PlayAlertAsync(CancellationToken cancellationToken)
        {
            EnsureUiBound();
            if (alertCanvas == null || alertTitleText == null)
            {
                return;
            }

            SetChoiceVisible(false);
            SetAlertVisible(true);

            if (alertFlashImage != null)
            {
                SetImageAlpha(alertFlashImage, 0.75f);
            }

            alertTitleText.rectTransform.localScale = Vector3.one * 2.2f;
            SetTextAlpha(alertTitleText, 0f);

            float elapsed = 0f;
            while (elapsed < alertPopSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / alertPopSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                SetTextAlpha(alertTitleText, eased);
                alertTitleText.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, eased);
                if (alertFlashImage != null)
                {
                    SetImageAlpha(alertFlashImage, Mathf.Lerp(0.75f, 0.35f, eased));
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetTextAlpha(alertTitleText, 1f);
            alertTitleText.rectTransform.localScale = Vector3.one;
            await DelayUnscaledAsync(alertHoldSeconds, cancellationToken);

            elapsed = 0f;
            while (elapsed < alertHideSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / alertHideSeconds);
                SetTextAlpha(alertTitleText, 1f - t);
                if (alertFlashImage != null)
                {
                    SetImageAlpha(alertFlashImage, Mathf.Lerp(0.35f, 0f, t));
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetAlertVisible(false);
        }

        /// <inheritdoc/>
        public void ShowChoice(string enemyName)
        {
            EnsureUiBound();
            hasChoice = false;
            selectedChoice = TrainingAmbushChoice.Flee;
            cachedEnemyName = enemyName ?? string.Empty;
            choiceVisible = true;
            ApplyChoiceCopy();

            if (fightButton != null)
            {
                fightButton.interactable = true;
            }

            if (fleeButton != null)
            {
                fleeButton.interactable = true;
            }

            SetAlertVisible(false);
            SetChoiceVisible(true);
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            if (alertTitleText != null)
            {
                alertTitleText.text = LocalizedText.Get(GameTextKeys.TrainingAmbushTitleBang);
            }

            // 非表示中でもボタン文言は現言語に揃える
            ApplyChoiceCopy();
        }

        private void ApplyChoiceCopy()
        {
            if (choiceTitleText != null)
            {
                choiceTitleText.text = LocalizedText.Get(GameTextKeys.TrainingAmbushTitle);
            }

            if (choiceMessageText != null)
            {
                string name = string.IsNullOrEmpty(cachedEnemyName)
                    ? LocalizedText.Get(GameTextKeys.TrainingAmbushEnemyDefault)
                    : cachedEnemyName;
                choiceMessageText.text = LocalizedText.Get(GameTextKeys.TrainingAmbushMessage, "name", name);
            }

            LhButtonLabelUtility.SetLabel(
                fightButton,
                LocalizedText.GetOrFallback(GameTextKeys.TrainingAmbushFight, "戦う"));
            LhButtonLabelUtility.SetLabel(
                fleeButton,
                LocalizedText.GetOrFallback(GameTextKeys.TrainingAmbushFlee, "逃げる"));
        }

        /// <inheritdoc/>
        public async UniTask<TrainingAmbushChoice> WaitChoiceAsync(CancellationToken cancellationToken)
        {
            hasChoice = false;
            await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
            return selectedChoice;
        }

        /// <inheritdoc/>
        public void Hide()
        {
            hasChoice = false;
            choiceVisible = false;
            SetAlertVisible(false);
            SetChoiceVisible(false);
        }

        private void EnsureUiBound()
        {
            if (uiBound)
            {
                return;
            }

            uiBound = true;
            ValidateSerializedReferences();
            BindUi();
        }

        private void ValidateSerializedReferences()
        {
            if (alertCanvas == null
                || alertTitleText == null
                || choiceCanvas == null
                || choiceTitleText == null
                || choiceMessageText == null
                || fightButton == null
                || fleeButton == null)
            {
                Debug.LogError(
                    "[TrainingAmbushView] 必須SerializeFieldが未配線ですHierarchyで接続してください",
                    this);
            }
        }

        private void BindUi()
        {
            if (fightButton != null)
            {
                fightButton.onClick.RemoveListener(OnFightClicked);
                fightButton.onClick.AddListener(OnFightClicked);
                fightButton.EnsureUiSoundFeedback();
            }

            if (fleeButton != null)
            {
                fleeButton.onClick.RemoveListener(OnFleeClicked);
                fleeButton.onClick.AddListener(OnFleeClicked);
                fleeButton.EnsureUiSoundFeedback();
            }

            if (alertTitleText != null)
            {
                alertTitleText.text = LocalizedText.Get(GameTextKeys.TrainingAmbushTitleBang);
            }
        }

        private void OnFightClicked()
        {
            selectedChoice = TrainingAmbushChoice.Fight;
            hasChoice = true;
        }

        private void OnFleeClicked()
        {
            selectedChoice = TrainingAmbushChoice.Flee;
            hasChoice = true;
        }

        private void SetAlertVisible(bool visible)
        {
            if (alertCanvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(alertCanvas, visible, AlertSortingOrder);
            }
        }

        private void SetChoiceVisible(bool visible)
        {
            if (choiceCanvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(choiceCanvas, visible, ChoiceSortingOrder);
            }

            if (choiceBlocker != null)
            {
                TitleClayUiVisualUtility.ConfigureInputBlocker(choiceBlocker, visible);
            }
        }

        private static void SetTextAlpha(TMP_Text text, float alpha)
        {
            if (text == null)
            {
                return;
            }

            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static async UniTask DelayUnscaledAsync(float seconds, CancellationToken cancellationToken)
        {
            if (seconds <= 0f)
            {
                return;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
}
