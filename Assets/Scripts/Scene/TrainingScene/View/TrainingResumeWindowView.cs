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
                || restartButton == null)
            {
                Debug.LogError(
                    "[TrainingResumeWindowView] SerializeFieldが未配線ですTools/ClayMonsters/Wire Training Scene Referencesを実行してください",
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
