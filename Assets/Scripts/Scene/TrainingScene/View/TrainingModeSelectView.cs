using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.TrainingScene.Domain;
using Scene.TrainingScene.Interface;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成方式選択ウィンドウ
    /// じっくり育成か自動育成かを選ばせる
    /// </summary>
    public sealed class TrainingModeSelectView : MonoBehaviour, ITrainingModeSelectView
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
        public void Show(string modelName)
        {
            EnsureUiBound();
            CanvasVisibilityUtility.SetPanelActive(GetWindowRoot(), true);

            if (titleText != null)
            {
                titleText.text = "育成方法を選んでください";
            }

            if (modelNameText != null)
            {
                bool hasModelName = !string.IsNullOrEmpty(modelName);
                modelNameText.text = hasModelName ? modelName : string.Empty;
                modelNameText.enabled = hasModelName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = "じっくり育成は行き先を自分で選びます\n自動育成は5日間を自動で進行します";
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
            Hide();
            return selectedMode;
        }

        private void EnsureUiBound()
        {
            if (uiBound)
            {
                return;
            }

            uiBound = true;
            EnsureSerializedReferences();
            BindUi();
        }

        private void EnsureSerializedReferences()
        {
            Transform root = transform;

            if (windowRoot == null)
            {
                windowRoot = gameObject;
            }

            if (blocker == null)
            {
                blocker = TrainingUiReferenceUtility.FindImage(root, "Blocker");
            }

            Transform panel = root.Find("WindowPanel");
            Transform textRoot = panel != null ? panel : root;

            if (titleText == null)
            {
                titleText = TrainingUiReferenceUtility.FindText(textRoot, "TitleText");
            }

            if (modelNameText == null)
            {
                modelNameText = TrainingUiReferenceUtility.FindText(textRoot, "ModelNameText");
            }

            if (descriptionText == null)
            {
                descriptionText = TrainingUiReferenceUtility.FindText(textRoot, "DescriptionText");
            }

            if (manualButton == null)
            {
                manualButton = TrainingUiReferenceUtility.FindButton(root, "ManualTrainingButton");
            }

            if (autoButton == null)
            {
                autoButton = TrainingUiReferenceUtility.FindButton(root, "AutoTrainingButton");
            }
        }

        private void BindUi()
        {
            if (windowRoot == null)
            {
                windowRoot = gameObject;
            }

            if (GetWindowRoot() == null)
            {
                Debug.LogError(
                    "[TrainingModeSelectView] windowRootが見つかりません。TrainingModeSelectWindowが配置されているか確認してください",
                    this);
            }

            if (manualButton != null)
            {
                manualButton.onClick.RemoveListener(OnManualClicked);
                manualButton.onClick.AddListener(OnManualClicked);
                manualButton.EnsureUiSoundFeedback();
                SetButtonLabel(manualButton, "じっくり育成");
            }

            if (autoButton != null)
            {
                autoButton.onClick.RemoveListener(OnAutoClicked);
                autoButton.onClick.AddListener(OnAutoClicked);
                autoButton.EnsureUiSoundFeedback();
                SetButtonLabel(autoButton, "自動育成");
            }
        }

        private static void SetButtonLabel(LHButton button, string label)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
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
                blocker.raycastTarget = visible;
            }
        }

        private GameObject GetWindowRoot()
        {
            return windowRoot != null ? windowRoot : gameObject;
        }
    }
}
