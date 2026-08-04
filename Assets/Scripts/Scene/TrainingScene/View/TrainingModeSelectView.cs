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
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成方式選択ウィンドウ
    /// じっくり育成か自動育成かを選ばせる
    /// </summary>
    public sealed class TrainingModeSelectView : MonoBehaviour, ITrainingModeSelectView, ILanguageAwareUi
    {
        [Header("Root")]
        [FormerlySerializedAs("windowCanvas")]
        [FormerlySerializedAs("windowGroup")]
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private Image blocker;

        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text modelNameText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Buttons")]
        [SerializeField] private LHButton manualButton;
        [SerializeField] private LHButton autoButton;

        private bool hasChoice;
        private TrainingPlayMode selectedMode = TrainingPlayMode.Manual;
        private bool uiBound;

        /// <inheritdoc/>
        public void Show()
        {
            EnsureUiBound();
            CanvasVisibilityUtility.SetPanelActive(GetWindowRoot(), true);
            ApplyLocalizedLabels();

            if (modelNameText != null)
            {
                modelNameText.text = string.Empty;
                modelNameText.enabled = false;
            }

            if (manualButton != null)
            {
                manualButton.interactable = true;
            }

            if (autoButton != null)
            {
                autoButton.interactable = true;
            }

            SetWindowVisible(true);
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private bool labelOriginalsCaptured;
        private string titleOriginal = "育成方法を選んでください";
        private string descriptionOriginal =
            "じっくり育成は1〜6時間目の行動を自分で選びます\n昼休みは売店放課後は戦闘です\n自動育成は{days}日間を自動で進行します";
        private string manualOriginal = "じっくり育成";
        private string autoOriginal = "自動育成";

        private void ApplyLocalizedLabels()
        {
            CaptureLabelOriginalsIfNeeded();
            if (titleText != null)
            {
                LocalizedFont.SetText(
                    titleText,
                    SceneLocalizedLabel.Resolve(GameTextKeys.TrainingModeSelectTitle, titleOriginal));
            }

            if (descriptionText != null)
            {
                LocalizedFont.SetText(
                    descriptionText,
                    SceneLocalizedLabel.Resolve(
                        GameTextKeys.TrainingModeSelectBody,
                        descriptionOriginal,
                        "days",
                        TrainingSettings.TotalDays));
            }

            LhButtonLabelUtility.SetLabel(
                manualButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.TrainingModeManual, manualOriginal));
            LhButtonLabelUtility.SetLabel(
                autoButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.TrainingModeAuto, autoOriginal));
        }

        private void CaptureLabelOriginalsIfNeeded()
        {
            if (labelOriginalsCaptured)
            {
                return;
            }

            titleOriginal = SceneLocalizedLabel.Capture(titleText, titleOriginal);
            descriptionOriginal = SceneLocalizedLabel.Capture(descriptionText, descriptionOriginal);
            manualOriginal = SceneLocalizedLabel.Capture(manualButton, manualOriginal);
            autoOriginal = SceneLocalizedLabel.Capture(autoButton, autoOriginal);
            labelOriginalsCaptured = true;
        }

        /// <inheritdoc/>
        public void Hide()
        {
            hasChoice = false;
            SetWindowVisible(false);
        }

        /// <inheritdoc/>
        public async UniTask<TrainingPlayMode> WaitChoiceAsync(CancellationToken cancellationToken)
        {
            hasChoice = false;
            await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
            return selectedMode;
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
            if (windowRoot == null)
            {
                windowRoot = gameObject;
            }

            if (manualButton == null || autoButton == null)
            {
                Debug.LogError(
                    "[TrainingModeSelectView] ボタン参照が未配線ですTools/ClayMonsters/Wire Training Scene Referencesを実行してください",
                    this);
            }
        }

        private void BindUi()
        {
            if (GetWindowRoot() == null)
            {
                Debug.LogError(
                    "[TrainingModeSelectView] windowRootが未設定です",
                    this);
            }

            if (manualButton != null)
            {
                manualButton.onClick.RemoveListener(OnManualClicked);
                manualButton.onClick.AddListener(OnManualClicked);
                manualButton.EnsureUiSoundFeedback();
            }

            if (autoButton != null)
            {
                autoButton.onClick.RemoveListener(OnAutoClicked);
                autoButton.onClick.AddListener(OnAutoClicked);
                autoButton.EnsureUiSoundFeedback();
            }
        }

        private void OnManualClicked()
        {
            selectedMode = TrainingPlayMode.Manual;
            hasChoice = true;
        }

        private void OnAutoClicked()
        {
            selectedMode = TrainingPlayMode.Auto;
            hasChoice = true;
        }

        private void SetWindowVisible(bool visible)
        {
            CanvasVisibilityUtility.SetPanelActive(GetWindowRoot(), visible);

            if (blocker != null)
            {
                TitleClayUiVisualUtility.ConfigureInputBlocker(blocker, visible);
            }
        }

        private GameObject GetWindowRoot()
        {
            return windowRoot != null ? windowRoot : gameObject;
        }
    }
}
