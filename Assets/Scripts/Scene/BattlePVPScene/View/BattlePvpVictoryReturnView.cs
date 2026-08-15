using Battle;
using Battle.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattlePVPScene.View
{
    /// <summary>
    /// 通信対戦勝利後のタイトル戻りと再戦ボタンUI
    /// </summary>
    public sealed class BattlePvpVictoryReturnView
        : MonoBehaviour,
            IBattleDualVictoryReturnView,
            IBattleVictoryReturnPresentationView,
            ILanguageAwareUi
    {
        private const int VisibleSortingOrder = 1100;

        [Tooltip("ONのときフォールバックUIを実行時生成しない")]
        [SerializeField] private bool useSceneCanvasLayout = true;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private LHButton titleReturnButton;
        [SerializeField] private LHButton rematchButton;

        private int cachedSortingOrder = 320;
        private bool hasCachedSortingOrder;
        private BattleVictoryReturnPresentation presentation = BattleVictoryReturnPresentation.TitleAndRematch;
        private bool rematchVisualCaptured;
        private readonly List<Graphic> rematchGraphics = new List<Graphic>();
        private readonly List<float> rematchGraphicAlphas = new List<float>();
        private readonly List<TMP_Text> rematchTexts = new List<TMP_Text>();
        private readonly List<float> rematchTextAlphas = new List<float>();

        private void Awake()
        {
            ValidateSceneLayout();
            rematchButton?.EnsureUiSoundFeedback();
            titleReturnButton?.EnsureUiSoundFeedback();
            CacheSortingOrderIfNeeded();
            CaptureRematchVisualsIfNeeded();
            ApplyLocalizedLabels();

            // GOを落とさずCanvasのみオフ(初回表示でAwake再入して消えるのを防ぐ)
            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, false);
        }

        /// <inheritdoc/>
        public void SetPresentation(BattleVictoryReturnPresentation presentation)
        {
            this.presentation = presentation;
            ApplyLocalizedLabels();
            ApplyRematchVisualState(rootCanvas != null && rootCanvas.enabled);
        }

        /// <inheritdoc/>
        public void SetDualButtonsVisible(bool visible)
        {
            if (visible)
            {
                ApplyLocalizedLabels();
            }

            if (rootCanvas != null)
            {
                CacheSortingOrderIfNeeded();
                if (visible)
                {
                    rootCanvas.overrideSorting = true;
                    rootCanvas.sortingOrder = VisibleSortingOrder;
                }
                else
                {
                    rootCanvas.sortingOrder = cachedSortingOrder;
                }
            }

            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, visible);
            EnsureButtonReady(titleReturnButton, visible);
            bool showRematch = visible && presentation != BattleVictoryReturnPresentation.TitleOnly;
            EnsureButtonReady(rematchButton, showRematch);
            ApplyRematchVisualState(showRematch);
        }


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private bool labelOriginalsCaptured;
        private string titleReturnOriginal = "タイトルへ戻る";
        private string rematchOriginal = "再戦";

        private void ApplyLocalizedLabels()
        {
            CaptureLabelOriginalsIfNeeded();
            ResolveLabels(out string titleLabel, out string rematchLabel);
            LhButtonLabelUtility.SetLabel(titleReturnButton, titleLabel);
            LhButtonLabelUtility.SetLabel(rematchButton, rematchLabel);
        }

        private void ResolveLabels(out string titleLabel, out string rematchLabel)
        {
            switch (presentation)
            {
                case BattleVictoryReturnPresentation.ContinueAndAbort:
                    titleLabel = SceneLocalizedLabel.Resolve(
                        GameTextKeys.NpcTournamentAbort,
                        "中断");
                    rematchLabel = SceneLocalizedLabel.Resolve(
                        GameTextKeys.NpcTournamentContinue,
                        "続ける");
                    return;
                case BattleVictoryReturnPresentation.TitleOnly:
                    titleLabel = SceneLocalizedLabel.Resolve(
                        GameTextKeys.TrainingHudBackToTitle,
                        titleReturnOriginal);
                    rematchLabel = SceneLocalizedLabel.Resolve(
                        GameTextKeys.BattleRematch,
                        rematchOriginal);
                    return;
                default:
                    titleLabel = SceneLocalizedLabel.Resolve(
                        GameTextKeys.TrainingHudBackToTitle,
                        titleReturnOriginal);
                    rematchLabel = SceneLocalizedLabel.Resolve(
                        GameTextKeys.BattleRematch,
                        rematchOriginal);
                    return;
            }
        }

        private void CaptureLabelOriginalsIfNeeded()
        {
            if (labelOriginalsCaptured)
            {
                return;
            }

            titleReturnOriginal = SceneLocalizedLabel.Capture(titleReturnButton, titleReturnOriginal);
            rematchOriginal = SceneLocalizedLabel.Capture(rematchButton, rematchOriginal);
            labelOriginalsCaptured = true;
        }

        private void CacheSortingOrderIfNeeded()
        {
            if (hasCachedSortingOrder || rootCanvas == null)
            {
                return;
            }

            // 表示用に一時的に上げる前の値だけを覚える
            if (rootCanvas.sortingOrder != VisibleSortingOrder)
            {
                cachedSortingOrder = rootCanvas.sortingOrder;
            }

            hasCachedSortingOrder = true;
        }

        /// <inheritdoc/>
        public async UniTask<BattleVictoryReturnChoice> WaitVictoryReturnChoiceAsync(CancellationToken cancellationToken)
        {
            if (titleReturnButton == null && rematchButton == null)
            {
                Debug.LogError(
                    "[BattlePvpVictoryReturnView] 再戦/タイトル戻りボタン参照がありません Titleへ強制します",
                    this);
                return BattleVictoryReturnChoice.Title;
            }

            BattleVictoryReturnChoice choice = BattleVictoryReturnChoice.Title;
            bool decided = false;
            bool allowRematch = presentation != BattleVictoryReturnPresentation.TitleOnly;

            void OnTitleClick()
            {
                choice = BattleVictoryReturnChoice.Title;
                decided = true;
            }

            void OnRematchClick()
            {
                choice = BattleVictoryReturnChoice.Rematch;
                decided = true;
            }

            if (titleReturnButton != null)
            {
                titleReturnButton.onClick.AddListener(OnTitleClick);
            }

            if (allowRematch && rematchButton != null)
            {
                rematchButton.onClick.AddListener(OnRematchClick);
            }

            try
            {
                await UniTask.WaitUntil(() => decided, cancellationToken: cancellationToken);
                return choice;
            }
            finally
            {
                if (titleReturnButton != null)
                {
                    titleReturnButton.onClick.RemoveListener(OnTitleClick);
                }

                if (rematchButton != null)
                {
                    rematchButton.onClick.RemoveListener(OnRematchClick);
                }
            }
        }

        private void CaptureRematchVisualsIfNeeded()
        {
            if (rematchVisualCaptured || rematchButton == null)
            {
                return;
            }

            rematchGraphics.Clear();
            rematchGraphicAlphas.Clear();
            rematchTexts.Clear();
            rematchTextAlphas.Clear();

            Graphic[] graphics = rematchButton.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                rematchGraphics.Add(graphic);
                rematchGraphicAlphas.Add(graphic.color.a);
            }

            TMP_Text[] texts = rematchButton.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                rematchTexts.Add(text);
                rematchTextAlphas.Add(text.alpha);
            }

            rematchVisualCaptured = true;
        }

        private void ApplyRematchVisualState(bool visible)
        {
            CaptureRematchVisualsIfNeeded();
            for (int i = 0; i < rematchGraphics.Count; i++)
            {
                Graphic graphic = rematchGraphics[i];
                if (graphic == null)
                {
                    continue;
                }

                Color color = graphic.color;
                color.a = visible ? rematchGraphicAlphas[i] : 0f;
                graphic.color = color;
                graphic.raycastTarget = visible;
            }

            for (int i = 0; i < rematchTexts.Count; i++)
            {
                TMP_Text text = rematchTexts[i];
                if (text == null)
                {
                    continue;
                }

                text.alpha = visible ? rematchTextAlphas[i] : 0f;
                text.raycastTarget = visible;
            }
        }

        private static void EnsureButtonReady(LHButton button, bool visible)
        {
            if (button == null)
            {
                return;
            }

            // 親Canvas配下は常時有効にし表示はCanvas.enabledで制御する
            if (!button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(true);
            }

            button.interactable = visible;
        }

        private void ValidateSceneLayout()
        {
            if (!useSceneCanvasLayout)
            {
                return;
            }

            if (rootCanvas == null || titleReturnButton == null || rematchButton == null)
            {
                Debug.LogError(
                    "[BattlePvpVictoryReturnView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }
        }
    }
}
