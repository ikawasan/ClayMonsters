using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.TrainingScene.Domain;
using Scene.TrainingScene.Interface;
using System;
using System.Threading;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成完了リザルト画面
    /// 最終ステータスと技構成を表示する
    /// </summary>
    public sealed class TrainingAutoResultView : MonoBehaviour, ITrainingAutoResultView
    {
        [Header("Root")]
        [FormerlySerializedAs("windowCanvas")]
        [FormerlySerializedAs("windowGroup")]
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private Image blocker;

        [Header("Thumbnail")]
        [Tooltip("モデルサムネイル")]
        [SerializeField] private Image thumbnailImage;

        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text modelNameText;
        [SerializeField] private TMP_Text statsText;
        [Header("Attacks")]
        [SerializeField] private TrainingResumeAttacksContentView attacksPanel;
        [SerializeField] private TMP_Text saveResultText;

        [Header("Buttons")]
        [SerializeField] private LHButton backToTitleButton;

        private bool uiBound;
        private Texture2D runtimeThumbnailTexture;
        private Sprite runtimeThumbnailSprite;

        /// <inheritdoc/>
        public void Show(TrainingAutoResultPresentation presentation)
        {
            EnsureUiBound();

            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(presentation.TitleText)
                    ? "育成完了"
                    : presentation.TitleText;
            }

            if (modelNameText != null)
            {
                bool hasModelName = !string.IsNullOrEmpty(presentation.ModelName);
                modelNameText.text = hasModelName ? presentation.ModelName : string.Empty;
                modelNameText.enabled = hasModelName;
            }

            if (statsText != null)
            {
                statsText.text = presentation.StatsText;
            }

            if (attacksPanel != null)
            {
                attacksPanel.Show(presentation.Attacks);
            }

            if (saveResultText != null)
            {
                bool hasMessage = !string.IsNullOrEmpty(presentation.SaveResultMessage);
                saveResultText.text = hasMessage ? presentation.SaveResultMessage : string.Empty;
                saveResultText.enabled = hasMessage;
            }

            ApplyThumbnail(presentation.ThumbnailPng);

            if (backToTitleButton != null)
            {
                backToTitleButton.interactable = true;
                string buttonLabel = string.IsNullOrEmpty(presentation.ContinueButtonLabel)
                    ? "保存先を選ぶ"
                    : presentation.ContinueButtonLabel;
                SetButtonLabel(backToTitleButton, buttonLabel);
            }

            SetWindowVisible(true);
        }

        /// <inheritdoc/>
        public void Hide()
        {
            attacksPanel?.Clear();
            ClearThumbnail();
            SetWindowVisible(false);
        }

        /// <inheritdoc/>
        public UniTask WaitContinueAsync(CancellationToken cancellationToken)
        {
            return WaitBackToTitleAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async UniTask WaitBackToTitleAsync(CancellationToken cancellationToken)
        {
            if (backToTitleButton == null)
            {
                Debug.LogError(
                    "[TrainingAutoResultView] BackToTitleButtonが見つかりません。TrainingAutoResultWindow配下を確認してください",
                    this);
                return;
            }

            bool pressed = false;
            using (SubscribeBackToTitleClick(() => pressed = true))
            {
                await UniTask.WaitUntil(() => pressed, cancellationToken: cancellationToken);
            }

            Hide();
        }

        /// <inheritdoc/>
        public IDisposable SubscribeBackToTitleClick(UnityAction action)
        {
            if (backToTitleButton == null)
            {
                return new EmptyDisposable();
            }

            return backToTitleButton.SubscribeOnClick(action);
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

            EnsureThumbnailReference();
            EnsureAttacksPanelReference();

            if (backToTitleButton != null)
            {
                backToTitleButton.EnsureUiSoundFeedback();
                SetButtonLabel(backToTitleButton, "保存先を選ぶ");
            }
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

            if (statsText == null)
            {
                statsText = TrainingUiReferenceUtility.FindWindowPanelChild(root, "StatsText")
                    ?.GetComponent<TMP_Text>();
            }

            if (saveResultText == null)
            {
                saveResultText = TrainingUiReferenceUtility.FindWindowPanelChild(root, "SaveResultText")
                    ?.GetComponent<TMP_Text>();
            }

            if (backToTitleButton == null)
            {
                backToTitleButton = TrainingUiReferenceUtility.FindButton(root, "BackToTitleButton");
            }
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
                    "[TrainingAutoResultView] ThumbnailImageが見つかりません。TrainingAutoResultWindow配下を確認してください",
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
                    "[TrainingAutoResultView] AttacksPanelが見つかりません。TrainingAutoResultWindow配下を確認してください",
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
                thumbnailImage.gameObject.SetActive(false);
                return;
            }

            runtimeThumbnailTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!runtimeThumbnailTexture.LoadImage(thumbnailPng))
            {
                ClearThumbnail();
                thumbnailImage.gameObject.SetActive(false);
                return;
            }

            runtimeThumbnailSprite = Sprite.Create(
                runtimeThumbnailTexture,
                new Rect(0f, 0f, runtimeThumbnailTexture.width, runtimeThumbnailTexture.height),
                new Vector2(0.5f, 0.5f));
            TitleClayUiVisualUtility.ApplySlotThumbnailImage(thumbnailImage, runtimeThumbnailSprite);
            thumbnailImage.gameObject.SetActive(true);
        }

        private void ClearThumbnail()
        {
            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = null;
                thumbnailImage.gameObject.SetActive(false);
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

        private static void SetButtonLabel(LHButton button, string label)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }

        private void SetWindowVisible(bool visible)
        {
            CanvasVisibilityUtility.SetPanelActive(windowRoot != null ? windowRoot : gameObject, visible);

            if (blocker != null)
            {
                blocker.raycastTarget = visible;
            }
        }

        private void OnDestroy()
        {
            ClearThumbnail();
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
