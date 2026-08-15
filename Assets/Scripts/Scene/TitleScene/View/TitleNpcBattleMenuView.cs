using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using Scene.TitleScene.Interface;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Scene.TitleScene.View
{
    /// <summary>
    /// CPU戦のモード選択とトーナメント難易度選択UI
    /// Canvas.enabledで表示切替する
    /// </summary>
    public sealed class TitleNpcBattleMenuView : MonoBehaviour, ITitleNpcBattleMenuView, ILanguageAwareUi
    {
        private const int MenuCanvasSortingOrder = 450;

        [Header("モード選択")]
        [SerializeField] private Canvas modeSelectCanvas;
        [SerializeField] private LHButton tournamentButton;
        [SerializeField] private LHButton freeBattleButton;
        [SerializeField] private LHButton modeBackButton;

        [Header("難易度選択")]
        [SerializeField] private Canvas difficultySelectCanvas;
        [SerializeField] private LHButton easyButton;
        [SerializeField] private LHButton normalButton;
        [SerializeField] private LHButton hardButton;
        [SerializeField] private LHButton veryHardButton;
        [SerializeField] private LHButton difficultyBackButton;

        private string tournamentOriginal = "トーナメント";
        private string freeBattleOriginal = "フリー対戦";
        private string modeBackOriginal = "戻る";
        private string easyOriginal = "イージー";
        private string normalOriginal = "ノーマル";
        private string hardOriginal = "ハード";
        private string veryHardOriginal = "ベリーハード";
        private string difficultyBackOriginal = "戻る";
        private bool labelOriginalsCaptured;

        /// <inheritdoc/>
        public bool IsConfigured =>
            modeSelectCanvas != null
            && difficultySelectCanvas != null
            && tournamentButton != null
            && freeBattleButton != null
            && modeBackButton != null
            && easyButton != null
            && normalButton != null
            && hardButton != null
            && veryHardButton != null
            && difficultyBackButton != null;

        private void Awake()
        {
            ValidateSceneUi();
            CaptureLabelOriginalsIfNeeded();
            ApplyLocalizedLabels();
            Hide();
        }

        /// <inheritdoc/>
        public void ShowModeSelect()
        {
            if (!IsConfigured)
            {
                Debug.LogError(
                    "[TitleNpcBattleMenuView] 必須参照が未配線のため表示できません",
                    this);
                return;
            }

            CaptureLabelOriginalsIfNeeded();
            ApplyLocalizedLabels();
            CanvasVisibilityUtility.SetCanvasEnabled(
                difficultySelectCanvas,
                false);
            CanvasVisibilityUtility.SetCanvasEnabled(
                modeSelectCanvas,
                true,
                MenuCanvasSortingOrder);
        }

        /// <inheritdoc/>
        public void ShowDifficultySelect()
        {
            if (!IsConfigured)
            {
                Debug.LogError(
                    "[TitleNpcBattleMenuView] 必須参照が未配線のため表示できません",
                    this);
                return;
            }

            CaptureLabelOriginalsIfNeeded();
            ApplyLocalizedLabels();
            CanvasVisibilityUtility.SetCanvasEnabled(
                modeSelectCanvas,
                false);
            CanvasVisibilityUtility.SetCanvasEnabled(
                difficultySelectCanvas,
                true,
                MenuCanvasSortingOrder);
        }

        /// <inheritdoc/>
        public void Hide()
        {
            CanvasVisibilityUtility.SetCanvasEnabled(modeSelectCanvas, false);
            CanvasVisibilityUtility.SetCanvasEnabled(difficultySelectCanvas, false);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeTournamentButtonClick(UnityAction action)
        {
            return SubscribeButton(tournamentButton, action);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeFreeBattleButtonClick(UnityAction action)
        {
            return SubscribeButton(freeBattleButton, action);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeModeBackButtonClick(UnityAction action)
        {
            return SubscribeButton(modeBackButton, action);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeEasyButtonClick(UnityAction action)
        {
            return SubscribeButton(easyButton, action);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeNormalButtonClick(UnityAction action)
        {
            return SubscribeButton(normalButton, action);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeHardButtonClick(UnityAction action)
        {
            return SubscribeButton(hardButton, action);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeVeryHardButtonClick(UnityAction action)
        {
            return SubscribeButton(veryHardButton, action);
        }

        /// <inheritdoc/>
        public IDisposable SubscribeDifficultyBackButtonClick(UnityAction action)
        {
            return SubscribeButton(difficultyBackButton, action);
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private void ApplyLocalizedLabels()
        {
            CaptureLabelOriginalsIfNeeded();
            LhButtonLabelUtility.SetLabel(
                tournamentButton,
                SceneLocalizedLabel.Resolve(
                    GameTextKeys.TitleNpcModeTournament,
                    tournamentOriginal));
            LhButtonLabelUtility.SetLabel(
                freeBattleButton,
                SceneLocalizedLabel.Resolve(
                    GameTextKeys.TitleNpcModeFreeBattle,
                    freeBattleOriginal));
            LhButtonLabelUtility.SetLabel(
                modeBackButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonReturn, modeBackOriginal));
            LhButtonLabelUtility.SetLabel(
                easyButton,
                SceneLocalizedLabel.Resolve(
                    GameTextKeys.TitleNpcTournamentEasy,
                    easyOriginal));
            LhButtonLabelUtility.SetLabel(
                normalButton,
                SceneLocalizedLabel.Resolve(
                    GameTextKeys.TitleNpcTournamentNormal,
                    normalOriginal));
            LhButtonLabelUtility.SetLabel(
                hardButton,
                SceneLocalizedLabel.Resolve(
                    GameTextKeys.TitleNpcTournamentHard,
                    hardOriginal));
            LhButtonLabelUtility.SetLabel(
                veryHardButton,
                SceneLocalizedLabel.Resolve(
                    GameTextKeys.TitleNpcTournamentVeryHard,
                    veryHardOriginal));
            LhButtonLabelUtility.SetLabel(
                difficultyBackButton,
                SceneLocalizedLabel.Resolve(
                    GameTextKeys.CommonReturn,
                    difficultyBackOriginal));
        }

        private void CaptureLabelOriginalsIfNeeded()
        {
            if (labelOriginalsCaptured)
            {
                return;
            }

            tournamentOriginal = SceneLocalizedLabel.Capture(tournamentButton, tournamentOriginal);
            freeBattleOriginal = SceneLocalizedLabel.Capture(freeBattleButton, freeBattleOriginal);
            modeBackOriginal = SceneLocalizedLabel.Capture(modeBackButton, modeBackOriginal);
            easyOriginal = SceneLocalizedLabel.Capture(easyButton, easyOriginal);
            normalOriginal = SceneLocalizedLabel.Capture(normalButton, normalOriginal);
            hardOriginal = SceneLocalizedLabel.Capture(hardButton, hardOriginal);
            veryHardOriginal = SceneLocalizedLabel.Capture(veryHardButton, veryHardOriginal);
            difficultyBackOriginal = SceneLocalizedLabel.Capture(
                difficultyBackButton,
                difficultyBackOriginal);
            labelOriginalsCaptured = true;
        }

        private void ValidateSceneUi()
        {
            if (!IsConfigured)
            {
                Debug.LogError(
                    "[TitleNpcBattleMenuView] シーン上のUI参照が未設定です"
                    + "ModeSelectCanvasとDifficultySelectCanvasを配置しInspectorで接続してください",
                    this);
            }
        }

        private static IDisposable SubscribeButton(LHButton button, UnityAction action)
        {
            if (button == null)
            {
                return EmptyDisposable.Instance;
            }

            return button.SubscribeOnClick(action);
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();

            public void Dispose()
            {
            }
        }
    }
}
