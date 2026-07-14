using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
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
    /// 育成再開確認用の大型ウィンドウ
    /// 途中データのステータスと技構成を一覧表示する
    /// </summary>
    public sealed class TrainingResumeWindowView : MonoBehaviour, ITrainingResumeWindowView
    {
        [Header("Root")]
        [Tooltip("ウィンドウ全体のルート")]
        [FormerlySerializedAs("windowGroup")]
        [SerializeField] private GameObject windowRoot;
        [Tooltip("背面の入力ブロック用オーバーレイ")]
        [SerializeField] private Image blocker;

        [Header("Thumbnail")]
        [Tooltip("モデルサムネイル")]
        [SerializeField] private Image thumbnailImage;

        [Header("Texts")]
        [Tooltip("ウィンドウタイトル")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("モデル名")]
        [SerializeField] private TMP_Text modelNameText;
        [Tooltip("曜日・時間・ターン")]
        [SerializeField] private TMP_Text progressText;
        [Tooltip("体力")]
        [SerializeField] private TMP_Text staminaText;
        [Tooltip("ステータス")]
        [SerializeField] private TMP_Text statsText;
        [Tooltip("技構成パネル")]
        [SerializeField] private TrainingResumeAttacksContentView attacksPanel;

        [Header("Buttons")]
        [Tooltip("最初から育成ボタン")]
        [SerializeField] private LHButton restartButton;
        [Tooltip("続きから育成ボタン")]
        [SerializeField] private LHButton continueButton;

        private bool hasChoice;
        private bool continueSelected;
        private bool uiBound;
        private Texture2D runtimeThumbnailTexture;
        private Sprite runtimeThumbnailSprite;

        private void Awake()
        {
            EnsureUiBound();
        }

        /// <inheritdoc/>
        public void Show(TrainingResumeProgressPresentation presentation)
        {
            EnsureUiBound();

            if (titleText != null)
            {
                titleText.text = "育成途中のデータがあります";
            }

            if (modelNameText != null)
            {
                bool hasModelName = !string.IsNullOrEmpty(presentation.ModelName);
                modelNameText.text = hasModelName ? presentation.ModelName : string.Empty;
                modelNameText.enabled = hasModelName;
            }

            if (progressText != null)
            {
                progressText.text = presentation.DayPeriodTurnLabel;
            }

            if (staminaText != null)
            {
                staminaText.text = presentation.StaminaLabel;
            }

            if (statsText != null)
            {
                statsText.text = presentation.StatsText;
            }

            if (attacksPanel != null)
            {
                attacksPanel.ShowForResumeWindow(presentation.Attacks);
            }

            ApplyThumbnail(presentation.ThumbnailPng);

            if (continueButton != null)
            {
                continueButton.interactable = true;
            }

            if (restartButton != null)
            {
                restartButton.interactable = true;
            }

            SetWindowVisible(true);
        }

        /// <inheritdoc/>
        public void Hide()
        {
            hasChoice = false;
            continueSelected = false;
            attacksPanel?.Clear(preserveLayoutSpace: true);
            ClearThumbnail();
            SetWindowVisible(false);
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

        /// <inheritdoc/>
        public async UniTask<bool> WaitChoiceAsync(CancellationToken cancellationToken)
        {
            hasChoice = false;
            continueSelected = false;
            await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
            Hide();
            return continueSelected;
        }

        private void EnsureUiBound()
        {
            if (uiBound)
            {
                return;
            }

            uiBound = true;
            BindUi();
        }

        private void BindUi()
        {
            EnsureSerializedReferences();

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
                continueButton.onClick.AddListener(OnContinueClicked);
                continueButton.EnsureUiSoundFeedback();
                SetButtonLabel(continueButton, "続きから育成");
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
                restartButton.onClick.AddListener(OnRestartClicked);
                restartButton.EnsureUiSoundFeedback();
                SetButtonLabel(restartButton, "最初から育成");
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

        private void OnContinueClicked()
        {
            continueSelected = true;
            hasChoice = true;
        }

        private void OnRestartClicked()
        {
            continueSelected = false;
            hasChoice = true;
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

            if (titleText == null)
            {
                titleText = TrainingUiReferenceUtility.FindWindowPanelChild(root, "TitleText")
                    ?.GetComponent<TMP_Text>();
            }

            if (modelNameText == null)
            {
                modelNameText = TrainingUiReferenceUtility.FindWindowPanelChild(root, "ModelNameText")
                    ?.GetComponent<TMP_Text>();
            }

            if (progressText == null)
            {
                progressText = TrainingUiReferenceUtility.FindWindowPanelChild(root, "ProgressText")
                    ?.GetComponent<TMP_Text>();
            }

            if (staminaText == null)
            {
                staminaText = TrainingUiReferenceUtility.FindWindowPanelChild(root, "StaminaText")
                    ?.GetComponent<TMP_Text>();
            }

            if (statsText == null)
            {
                statsText = TrainingUiReferenceUtility.FindWindowPanelChild(root, "StatsText")
                    ?.GetComponent<TMP_Text>();
            }

            if (continueButton == null)
            {
                continueButton = TrainingUiReferenceUtility.FindButton(root, "ResumeContinueButton");
            }

            if (restartButton == null)
            {
                restartButton = TrainingUiReferenceUtility.FindButton(root, "ResumeRestartButton");
            }

            EnsureThumbnailReference();
            EnsureAttacksPanelReference();
        }

        private void EnsureThumbnailReference()
        {
            if (thumbnailImage != null)
            {
                return;
            }

            Transform thumbnailTransform =
                TrainingUiReferenceUtility.FindWindowPanelChild(transform, "ThumbnailImage");
            if (thumbnailTransform == null)
            {
                Transform frame = TrainingUiReferenceUtility.FindWindowPanelChild(transform, "ThumbnailFrame");
                thumbnailTransform = frame != null
                    ? TrainingUiReferenceUtility.FindDeepChild(frame, "ThumbnailImage")
                    : null;
            }

            thumbnailImage = thumbnailTransform?.GetComponent<Image>();

            if (thumbnailImage == null)
            {
                Debug.LogError(
                    "[TrainingResumeWindowView] ThumbnailImageが見つかりません。TrainingResumeWindow配下を確認してください",
                    this);
            }
        }

        private void EnsureAttacksPanelReference()
        {
            if (attacksPanel != null)
            {
                return;
            }

            Transform attacksTransform = TrainingUiReferenceUtility.FindWindowPanelChild(transform, "AttacksPanel");
            attacksPanel = attacksTransform?.GetComponent<TrainingResumeAttacksContentView>();
            if (attacksPanel == null)
            {
                Debug.LogError(
                    "[TrainingResumeWindowView] AttacksPanelが見つかりません。TrainingResumeWindow配下を確認してください",
                    this);
            }
        }

        private void ApplyThumbnail(byte[] thumbnailPng)
        {
            EnsureThumbnailReference();
            ClearThumbnail();

            if (thumbnailImage == null)
            {
                return;
            }

            if (thumbnailPng == null || thumbnailPng.Length == 0)
            {
                thumbnailImage.enabled = false;
                return;
            }

            runtimeThumbnailTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!runtimeThumbnailTexture.LoadImage(thumbnailPng))
            {
                ClearThumbnail();
                thumbnailImage.enabled = false;
                return;
            }

            runtimeThumbnailSprite = Sprite.Create(
                runtimeThumbnailTexture,
                new Rect(0f, 0f, runtimeThumbnailTexture.width, runtimeThumbnailTexture.height),
                new Vector2(0.5f, 0.5f));
            TitleClayUiVisualUtility.ApplySlotThumbnailImage(thumbnailImage, runtimeThumbnailSprite);
            thumbnailImage.enabled = true;
        }

        private void ClearThumbnail()
        {
            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = null;
                thumbnailImage.enabled = false;
            }

            if (runtimeThumbnailSprite != null)
            {
                Destroy(runtimeThumbnailSprite);
                runtimeThumbnailSprite = null;
            }

            if (runtimeThumbnailTexture != null)
            {
                Destroy(runtimeThumbnailTexture);
                runtimeThumbnailTexture = null;
            }
        }

        private void OnDestroy()
        {
            ClearThumbnail();
        }
    }
}
