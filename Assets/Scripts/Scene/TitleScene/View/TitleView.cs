using R3;
using Extensions;
using LighthouseExtends.TextTable;
using LighthouseExtends.UIComponent.Button;
using Localization;
using Scene.TitleScene.Interface;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Scene.TitleScene.View
{
    /// <summary>
    /// タイトル画面のメニューとロゴを表示する
    /// UIはTitleシーンのCanvas上に配置する
    /// </summary>
    public class TitleView : MonoBehaviour, ITitleView
    {
        [SerializeField] private LHButton clayEditButton;
        [SerializeField] private LHButton battleNpcButton;
        [SerializeField] private LHButton battlePvpButton;
        [SerializeField] private LHButton trainingButton;
        [SerializeField] private LHButton skillTreeButton;
        [SerializeField] private LHButton modelGalleryButton;
        [SerializeField] private LHButton optionButton;
        [SerializeField] private LHButton quitGameButton;
        [SerializeField] private Image titleLogoImage;
        [SerializeField] private TMP_Text pointsText;

        private IDisposable languageSubscription;

        private void Awake()
        {
            ValidateSceneUi();
            ApplyMenuLabels();
            SubscribeLanguageChange();
        }

        private void OnDestroy()
        {
            languageSubscription?.Dispose();
            languageSubscription = null;
        }

        private void SubscribeLanguageChange()
        {
            ITextTableService service = TextTableService.Instance;
            if (service == null)
            {
                return;
            }

            languageSubscription?.Dispose();
            languageSubscription = service.CurrentLanguage.Subscribe(_ =>
            {
                ApplyMenuLabels();
            });
        }

        private void ApplyMenuLabels()
        {
            // Titleシーン配置の日本語原文をフォールバックにする
            SetButtonLabel(
                clayEditButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleClayEdit, "モンスターエディット"));
            SetButtonLabel(
                battleNpcButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleBattleNpc, "CPU戦"));
            SetButtonLabel(
                battlePvpButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleBattlePvp, "対人戦"));
            SetButtonLabel(
                trainingButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleTraining, "育成"));
            SetButtonLabel(
                skillTreeButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleSkillTree, "スキルツリー"));
            SetButtonLabel(
                modelGalleryButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleModelGallery, "展示室"));
            SetButtonLabel(
                optionButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleOption, "オプション"));
            SetButtonLabel(
                quitGameButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleQuit, "ゲームをやめる"));
        }

        private static void SetButtonLabel(LHButton button, string label)
        {
            if (button == null)
            {
                return;
            }

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                return;
            }

            LocalizedFont.SetText(text, label);
        }

        private void ValidateSceneUi()
        {
            if (titleLogoImage == null || battlePvpButton == null)
            {
                Debug.LogError(
                    "[TitleView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }

            if (pointsText == null)
            {
                Debug.LogError(
                    "[TitleView] pointsTextが未配線です。Titleシーン右上にTMPを配置しInspectorで接続してください",
                    this);
            }

            if (skillTreeButton == null)
            {
                Debug.LogError(
                    "[TitleView] skillTreeButtonが未配線です",
                    this);
            }

            if (modelGalleryButton == null)
            {
                Debug.LogError(
                    "[TitleView] modelGalleryButtonが未配線です。ModelGalleryButton配下のLHButtonを接続してください",
                    this);
            }
        }

        public IDisposable SubscribeClayEditButtonClick(UnityAction action) => clayEditButton.SubscribeOnClick(action);

        public IDisposable SubscribeBattleNpcButtonClick(UnityAction action) => battleNpcButton.SubscribeOnClick(action);

        public IDisposable SubscribeBattlePvpButtonClick(UnityAction action) => battlePvpButton.SubscribeOnClick(action);

        public IDisposable SubscribeTrainingButtonClick(UnityAction action)
        {
            if (trainingButton == null)
            {
                return new EmptyDisposable();
            }

            return trainingButton.SubscribeOnClick(action);
        }

        /// <inheritdoc />
        public IDisposable SubscribeSkillTreeButtonClick(UnityAction action)
        {
            if (skillTreeButton == null)
            {
                return new EmptyDisposable();
            }

            return skillTreeButton.SubscribeOnClick(action);
        }

        /// <inheritdoc />
        public IDisposable SubscribeModelGalleryButtonClick(UnityAction action)
        {
            if (modelGalleryButton == null)
            {
                return new EmptyDisposable();
            }

            return modelGalleryButton.SubscribeOnClick(action);
        }

        public IDisposable SubscribeOptionButtonClick(UnityAction action) => optionButton.SubscribeOnClick(action);

        public IDisposable SubscribeQuitGameButtonClick(UnityAction action) => quitGameButton.SubscribeOnClick(action);

        /// <inheritdoc />
        public void SetPoints(int points)
        {
            if (pointsText == null)
            {
                return;
            }

            LocalizedFont.SetText(
                pointsText,
                LocalizedText.Get(
                    GameTextKeys.TitlePoints,
                    "points",
                    Mathf.Max(0, points)));
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
