using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
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
    public sealed class TrainingAutoResultView : MonoBehaviour, ITrainingAutoResultView, ILanguageAwareUi
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
        [SerializeField] private TMP_Text backToTitleButtonLabel;

        private bool uiBound;
        private Texture2D runtimeThumbnailTexture;
        private Sprite runtimeThumbnailSprite;
        private bool isShowing;
        private TrainingAutoResultPresentation cachedPresentation;

        /// <inheritdoc/>
        public void Show(TrainingAutoResultPresentation presentation)
        {
            EnsureUiBound();
            isShowing = true;
            cachedPresentation = presentation;

            // 初回表示で子のAwake(EnsureSprites)が走る前に範囲色を書くと上書きされる
            SetWindowVisible(true);
            ApplyPresentationCopy(presentation);
            ApplyThumbnail(presentation.ThumbnailPng);
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            if (!isShowing)
            {
                LhButtonLabelUtility.SetLabel(
                    backToTitleButtonLabel,
                    LocalizedText.GetOrFallback(GameTextKeys.TrainingChooseSave, "保存先を選ぶ"));
                return;
            }

            ApplyPresentationCopy(cachedPresentation);
        }

        private void ApplyPresentationCopy(TrainingAutoResultPresentation presentation)
        {
            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(presentation.TitleText)
                    ? LocalizedText.Get(GameTextKeys.TrainingComplete)
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
                attacksPanel.ShowForConfirmPrefab(presentation.Attacks);
            }

            if (saveResultText != null)
            {
                bool hasMessage = !string.IsNullOrEmpty(presentation.SaveResultMessage);
                saveResultText.text = hasMessage ? presentation.SaveResultMessage : string.Empty;
                saveResultText.enabled = hasMessage;
            }

            if (backToTitleButton != null)
            {
                backToTitleButton.interactable = true;
                string buttonLabel = string.IsNullOrEmpty(presentation.ContinueButtonLabel)
                    ? LocalizedText.GetOrFallback(GameTextKeys.TrainingChooseSave, "保存先を選ぶ")
                    : presentation.ContinueButtonLabel;
                LhButtonLabelUtility.SetLabel(backToTitleButtonLabel, buttonLabel);
            }
        }

        /// <inheritdoc/>
        public void Hide()
        {
            isShowing = false;
            attacksPanel?.ClearForConfirmPrefab();
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
                    "[TrainingAutoResultView] BackToTitleButtonが未配線です",
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
                || backToTitleButton == null
                || backToTitleButtonLabel == null)
            {
                Debug.LogError(
                    "[TrainingAutoResultView] SerializeFieldが未配線ですTools/ClayMonsters/Wire Training Scene Referencesを実行してください",
                    this);
            }
        }

        private void BindUi()
        {
            if (backToTitleButton != null)
            {
                backToTitleButton.EnsureUiSoundFeedback();
                LhButtonLabelUtility.SetLabel(
                    backToTitleButtonLabel,
                    LocalizedText.GetOrFallback(GameTextKeys.TrainingChooseSave, "保存先を選ぶ"));
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
                thumbnailImage.gameObject.SetActive(false);
                return;
            }

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

            RuntimeThumbnailUtility.Destroy(ref runtimeThumbnailSprite, ref runtimeThumbnailTexture);
        }

        private void SetWindowVisible(bool visible)
        {
            CanvasVisibilityUtility.SetPanelActive(windowRoot != null ? windowRoot : gameObject, visible);

            if (blocker != null)
            {
                TitleClayUiVisualUtility.ConfigureInputBlocker(blocker, visible);
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
