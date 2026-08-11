using Extensions;
using LighthouseExtends.TextTable;
using LighthouseExtends.UIComponent.Button;
using Localization;
using R3;
using Scene.TitleScene.Interface;
using System;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Scene.TitleScene.View
{
    /// <summary>
    /// タイトル画面のメニューとロゴを表示する
    /// UIはTitleシーンのCanvas上に配置する
    /// </summary>
    public class TitleView : MonoBehaviour, ITitleView, ILanguageAwareUi
    {
        private const string PointsTooltipFallback =
            "戦闘でたまるポイント。\nスキルツリーや展示室で使用可能";

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
        [Tooltip("ポイント表示のホバー判定領域(未設定ならpointsText親のGraphic)")]
        [SerializeField] private Graphic pointsHoverTarget;
        [Tooltip("ポイント説明ウィンドウのCanvas表示はenabledで切替")]
        [SerializeField] private Canvas pointsTooltipCanvas;
        [Tooltip("ポイント説明本文")]
        [SerializeField] private TMP_Text pointsTooltipText;
        [Tooltip("ポイント説明パネル背景任意")]
        [SerializeField] private Image pointsTooltipPanelImage;

        private IDisposable languageSubscription;
        private int cachedPoints;
        private string pointsTooltipOriginal = PointsTooltipFallback;

        // シーン配置時の日本語原文(起動時にボタンから採取)
        private string clayEditLabelOriginal = "モンスターエディット";
        private string battleNpcLabelOriginal = "CPU戦";
        private string battlePvpLabelOriginal = "対人戦";
        private string trainingLabelOriginal = "育成";
        private string skillTreeLabelOriginal = "スキルツリー";
        private string modelGalleryLabelOriginal = "展示室";
        private string optionLabelOriginal = "オプション";
        private string quitGameLabelOriginal = "ゲームをやめる";

        private void Awake()
        {
            ValidateSceneUi();
            CaptureSceneMenuLabelOriginals();
            CapturePointsTooltipOriginal();
            ApplyMenuLabels();
            ApplyPointsTooltipVisual();
            BindPointsTooltipHover();
            SetPointsTooltipVisible(false);
            ApplyPointsTooltipText();
            SubscribeLanguageChange();
        }

        private void OnDestroy()
        {
            languageSubscription?.Dispose();
            languageSubscription = null;
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyMenuLabels();
            SetPoints(cachedPoints);
            ApplyPointsTooltipText();
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
                ApplyPointsTooltipText();
            });
        }

        /// <summary>
        /// シーンに配置された日本語ラベルを原文として保持する
        /// </summary>
        private void CaptureSceneMenuLabelOriginals()
        {
            CaptureIfPresent(clayEditButton, ref clayEditLabelOriginal);
            CaptureIfPresent(battleNpcButton, ref battleNpcLabelOriginal);
            CaptureIfPresent(battlePvpButton, ref battlePvpLabelOriginal);
            CaptureIfPresent(trainingButton, ref trainingLabelOriginal);
            CaptureIfPresent(skillTreeButton, ref skillTreeLabelOriginal);
            CaptureIfPresent(modelGalleryButton, ref modelGalleryLabelOriginal);
            CaptureIfPresent(optionButton, ref optionLabelOriginal);
            CaptureIfPresent(quitGameButton, ref quitGameLabelOriginal);
        }

        private void CapturePointsTooltipOriginal()
        {
            pointsTooltipOriginal = SceneLocalizedLabel.Capture(
                pointsTooltipText,
                PointsTooltipFallback);
        }

        private static void CaptureIfPresent(LHButton button, ref string original)
        {
            string text = ReadButtonLabel(button);
            if (!string.IsNullOrEmpty(text))
            {
                original = text;
            }
        }

        private static string ReadButtonLabel(LHButton button)
        {
            if (button == null)
            {
                return string.Empty;
            }

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null || string.IsNullOrEmpty(text.text))
            {
                return string.Empty;
            }

            return text.text.Trim();
        }

        private void ApplyMenuLabels()
        {
            // シーン配置原文を日本語フォールバックにする
            SetButtonLabel(
                clayEditButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleClayEdit, clayEditLabelOriginal));
            SetButtonLabel(
                battleNpcButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleBattleNpc, battleNpcLabelOriginal));
            SetButtonLabel(
                battlePvpButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleBattlePvp, battlePvpLabelOriginal));
            SetButtonLabel(
                trainingButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleTraining, trainingLabelOriginal));
            SetButtonLabel(
                skillTreeButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleSkillTree, skillTreeLabelOriginal));
            SetButtonLabel(
                modelGalleryButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleModelGallery, modelGalleryLabelOriginal));
            SetButtonLabel(
                optionButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleOption, optionLabelOriginal));
            SetButtonLabel(
                quitGameButton,
                LocalizedText.GetOrFallback(GameTextKeys.TitleQuit, quitGameLabelOriginal));
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

        private void ApplyPointsTooltipVisual()
        {
            if (pointsTooltipPanelImage != null)
            {
                TitleClayUiVisualUtility.ApplyPanel(pointsTooltipPanelImage);
            }

            if (pointsTooltipText != null)
            {
                TitleClayUiVisualUtility.EnsureTextFontOnly(pointsTooltipText);
                pointsTooltipText.raycastTarget = false;
            }
        }

        private void ApplyPointsTooltipText()
        {
            if (pointsTooltipText == null)
            {
                return;
            }

            LocalizedFont.SetText(
                pointsTooltipText,
                LocalizedText.GetOrFallback(
                    GameTextKeys.TitlePointsTooltip,
                    pointsTooltipOriginal));
        }

        private void BindPointsTooltipHover()
        {
            Graphic hoverTarget = ResolvePointsHoverTarget();
            if (hoverTarget == null)
            {
                return;
            }

            hoverTarget.raycastTarget = true;

            EventTrigger trigger = hoverTarget.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = hoverTarget.gameObject.AddComponent<EventTrigger>();
            }

            AddHoverEntry(trigger, EventTriggerType.PointerEnter, () => SetPointsTooltipVisible(true));
            AddHoverEntry(trigger, EventTriggerType.PointerExit, () => SetPointsTooltipVisible(false));
        }

        private Graphic ResolvePointsHoverTarget()
        {
            if (pointsHoverTarget != null)
            {
                return pointsHoverTarget;
            }

            if (pointsText == null || pointsText.transform.parent == null)
            {
                return null;
            }

            return pointsText.transform.parent.GetComponent<Graphic>();
        }

        private void SetPointsTooltipVisible(bool visible)
        {
            if (pointsTooltipCanvas == null)
            {
                return;
            }

            CanvasVisibilityUtility.SetCanvasEnabled(pointsTooltipCanvas, visible);
        }

        private static void AddHoverEntry(
            EventTrigger trigger,
            EventTriggerType type,
            Action action)
        {
            if (trigger == null || action == null)
            {
                return;
            }

            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action.Invoke());
            trigger.triggers.Add(entry);
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

            if (pointsTooltipCanvas == null || pointsTooltipText == null)
            {
                Debug.LogError(
                    "[TitleView] ポイント説明Tooltipが未配線です。Canvasと本文TMPをInspectorで接続してください",
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

            cachedPoints = Mathf.Max(0, points);
            LocalizedFont.SetText(
                pointsText,
                LocalizedText.GetOrFallback(
                    GameTextKeys.TitlePoints,
                    "{points} ポイント",
                    "points",
                    cachedPoints));
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
