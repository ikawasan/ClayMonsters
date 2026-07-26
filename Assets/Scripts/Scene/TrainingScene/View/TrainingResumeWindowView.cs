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
        [Tooltip("曜日・時間割・所持金")]
        [FormerlySerializedAs("progressText")]
        [SerializeField] private TMP_Text scheduleMoneyText;
        [Tooltip("やる気ラベル")]
        [FormerlySerializedAs("motivationStaminaText")]
        [SerializeField] private TMP_Text motivationText;
        [Tooltip("体力ラベル")]
        [SerializeField] private TMP_Text staminaText;
        [Tooltip("ステータス")]
        [SerializeField] private TMP_Text statsText;
        [Tooltip("技構成パネル")]
        [SerializeField] private TrainingResumeAttacksContentView attacksPanel;

        [Header("Motivation")]
        [Tooltip("やる気アイコン")]
        [SerializeField] private Image motivationIcon;

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
        private Sprite[] motivationFrames;
        private int motivationFrameIndex;
        private float motivationFrameTimer;
        private const float MotivationFrameSeconds = 0.12f;

        private void Awake()
        {
            EnsureUiBound();
        }

        private void Update()
        {
            TickMotivationIconAnimation();
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

            if (scheduleMoneyText != null)
            {
                scheduleMoneyText.text = presentation.ScheduleMoneyLabel;
                scheduleMoneyText.enabled = !string.IsNullOrEmpty(presentation.ScheduleMoneyLabel);
            }

            if (motivationText != null)
            {
                motivationText.text = presentation.MotivationLabel;
                motivationText.enabled = !string.IsNullOrEmpty(presentation.MotivationLabel);
            }

            if (staminaText != null)
            {
                staminaText.text = presentation.StaminaLabel;
                staminaText.enabled = !string.IsNullOrEmpty(presentation.StaminaLabel);
            }

            if (statsText != null)
            {
                statsText.text = presentation.StatsText;
            }

            if (attacksPanel != null)
            {
                attacksPanel.ShowForResumeWindow(presentation.Attacks);
            }

            ApplyMotivationIcon(presentation.Motivation);
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
            ClearMotivationIcon();
            SetWindowVisible(false);
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

        /// <inheritdoc/>
        public async UniTask<bool> WaitChoiceAsync(CancellationToken cancellationToken)
        {
            hasChoice = false;
            continueSelected = false;
            await UniTask.WaitUntil(() => hasChoice, cancellationToken: cancellationToken);
            bool resume = continueSelected;
            Hide();
            return resume;
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

            if (thumbnailImage == null
                || attacksPanel == null
                || continueButton == null
                || restartButton == null
                || scheduleMoneyText == null
                || motivationText == null
                || staminaText == null
                || motivationIcon == null)
            {
                Debug.LogError(
                    "[TrainingResumeWindowView] SerializeFieldが未配線ですHierarchy/Inspectorで手動接続してください",
                    this);
            }
        }

        private void BindUi()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
                continueButton.onClick.AddListener(OnContinueClicked);
                continueButton.EnsureUiSoundFeedback();
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
                restartButton.onClick.AddListener(OnRestartClicked);
                restartButton.EnsureUiSoundFeedback();
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

        private void ApplyMotivationIcon(TrainingMotivation motivation)
        {
            if (motivationIcon == null)
            {
                return;
            }

            motivationFrames = TrainingMotivationCatalog.ResolveIconFrames(motivation);
            motivationFrameIndex = 0;
            motivationFrameTimer = 0f;
            Sprite first = motivationFrames != null && motivationFrames.Length > 0
                ? motivationFrames[0]
                : null;
            motivationIcon.sprite = first;
            bool hasFrames = first != null;
            motivationIcon.enabled = hasFrames;
            motivationIcon.gameObject.SetActive(true);
            if (!hasFrames)
            {
                Debug.LogError(
                    "[TrainingResumeWindowView] やる気アイコンの読み込みに失敗しました",
                    this);
            }
        }

        private void ClearMotivationIcon()
        {
            motivationFrames = null;
            motivationFrameIndex = 0;
            motivationFrameTimer = 0f;
            if (motivationIcon == null)
            {
                return;
            }

            motivationIcon.sprite = null;
            motivationIcon.enabled = false;
        }

        private void TickMotivationIconAnimation()
        {
            if (motivationIcon == null
                || !motivationIcon.isActiveAndEnabled
                || motivationFrames == null
                || motivationFrames.Length <= 1)
            {
                return;
            }

            motivationFrameTimer += Time.unscaledDeltaTime;
            if (motivationFrameTimer < MotivationFrameSeconds)
            {
                return;
            }

            motivationFrameTimer = 0f;
            motivationFrameIndex = (motivationFrameIndex + 1) % motivationFrames.Length;
            Sprite frame = motivationFrames[motivationFrameIndex];
            if (frame != null)
            {
                motivationIcon.sprite = frame;
            }
        }

        private void ApplyThumbnail(byte[] thumbnailPng)
        {
            ClearThumbnail();

            if (thumbnailImage == null)
            {
                return;
            }

            if (!RuntimeThumbnailUtility.TryCreate(
                    thumbnailPng,
                    out runtimeThumbnailTexture,
                    out runtimeThumbnailSprite))
            {
                thumbnailImage.enabled = false;
                return;
            }

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

            RuntimeThumbnailUtility.Destroy(ref runtimeThumbnailSprite, ref runtimeThumbnailTexture);
        }

        private void OnDestroy()
        {
            ClearThumbnail();
        }
    }
}
