using Battle;
using Battle.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using SaveData;
using System;
using System.Threading;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// 対戦紹介のVS表示とReady/FightカウントダウンUI
    /// とどめ命中時のFinish演出と部位破壊時のBreak演出も担当する
    /// </summary>
    public sealed class BattleStartOverlayView : MonoBehaviour, IBattleFinishPresentation, IBattlePartBreakPresentation
    {
        private const string FinishLabel = "Finish";
        private const string BreakLabel = "Break!";

        [Header("参照")]
        [SerializeField] private Canvas overlayCanvas;
        [SerializeField] private TMP_Text vsText;
        [SerializeField] private TMP_Text readyText;
        [SerializeField] private TMP_Text fightText;
        [SerializeField] private TMP_Text finishText;
        [SerializeField] private Image finishFlashImage;
        [SerializeField] private Button startButton;
        [SerializeField] private BattleMatchupStatusPanelView statusPanel;
        [SerializeField] private BattleEnemyStrengthSelectView enemyStrengthSelect;

        [Header("VS画面")]
        [SerializeField] private float vsPopSeconds = 0.45f;
        [SerializeField] private float vsIdlePulseAmplitude = 0.05f;
        [SerializeField] private float vsIdlePulseSpeed = 3.2f;

        [Header("Ready/Fight")]
        [SerializeField] private float readyFadeSeconds = 0.3f;
        [SerializeField] private float readyHoldSeconds = 0.55f;
        [SerializeField] private float fightPopSeconds = 0.35f;
        [SerializeField] private float fightHoldSeconds = 0.45f;
        [SerializeField] private float hideSeconds = 0.2f;

        [Header("Finish")]
        [SerializeField] private float finishPopSeconds = 0.45f;
        [SerializeField] private float finishHoldSeconds = 0.9f;
        [SerializeField] private float finishFadeSeconds = 0.35f;
        [SerializeField] private float finishFlashSeconds = 0.18f;

        [Header("Break")]
        [SerializeField] private float breakPopSeconds = 0.35f;
        [SerializeField] private float breakHoldSeconds = 0.45f;
        [SerializeField] private float breakFadeSeconds = 0.25f;
        [SerializeField] private float breakFlashSeconds = 0.14f;

        [Header("Victory")]
        [SerializeField] private float victoryPopSeconds = 0.45f;
        [SerializeField] private float victoryHoldSeconds = 0.35f;
        [SerializeField] private GameObject vsNamePlateRoot;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text enemyNameText;
        [SerializeField] private TMP_Text victoryTitleText;
        [SerializeField] private TMP_Text victoryNameText;

        private IBattleCanvasTransition canvasTransition;
        private bool isVsIdleAnimating;
        private bool isVictoryPresentationActive;
        private bool isFinishPresentationActive;
        private CancellationTokenSource partBreakCts;
        private readonly BattleVictoryConfettiEffect victoryConfetti = new BattleVictoryConfettiEffect();

        [Inject]
        public void Construct(IBattleCanvasTransition canvasTransition)
        {
            this.canvasTransition = canvasTransition;
        }

        private void Awake()
        {
            ValidateSceneUi();
            FixOverlayCanvasLayout();
            ApplyMatchupVisualStyle();
            startButton?.EnsureUiSoundFeedback();
            HideImmediate();
        }

        private void Update()
        {
            if (!isVsIdleAnimating)
            {
                return;
            }

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * vsIdlePulseSpeed) * vsIdlePulseAmplitude;
            if (vsText != null)
            {
                vsText.rectTransform.localScale = Vector3.one * pulse;
            }
        }

        /// <summary>
        /// 育成シーンの放課後戦闘向けにオーバーレイを前面へ出す
        /// </summary>
        public void EnsureReadyForTrainingBattle()
        {
            EnsureMatchupUiReady();
        }

        /// <summary>
        /// 対戦紹介UIのレイアウトと表示順を整える
        /// </summary>
        public void EnsureMatchupUiReady()
        {
            ValidateSceneUi();
            FixOverlayCanvasLayout();
            ApplyMatchupVisualStyle();
        }

        /// <summary>
        /// VS演出に必要な参照が揃っているか
        /// </summary>
        public bool IsVsUiConfigured()
        {
            return overlayCanvas != null
                && vsText != null
                && startButton != null
                && playerNameText != null
                && enemyNameText != null;
        }

        /// <summary>
        /// VSを表示して開始ボタン押下を待つ
        /// </summary>
        public async UniTask ShowVsAndWaitStartAsync(
            string playerName,
            string enemyName,
            BattleUnit player,
            BattleUnit enemy,
            Func<EnemyStrengthTier, BattleUnit> rebuildEnemyWithStrength,
            EnemyStrengthTier initialStrengthTier,
            CancellationToken cancellationToken)
        {
            PrepareVsPresentation(playerName, enemyName, showStartButton: true);
            BindStatusPanel(player, enemy);
            BeginEnemyStrengthSelect(player, enemy, rebuildEnemyWithStrength, initialStrengthTier);

            if (vsText != null)
            {
                vsText.rectTransform.localScale = Vector3.one * 2.2f;
            }

            float elapsed = 0f;
            while (elapsed < vsPopSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / vsPopSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                SetElementAlpha(vsText, eased);
                SetNamePlate(playerNameText, playerName, eased);
                SetNamePlate(enemyNameText, enemyName, eased);

                if (vsText != null)
                {
                    float scale = Mathf.Lerp(2.2f, 1f, eased);
                    vsText.rectTransform.localScale = Vector3.one * scale;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetElementAlpha(vsText, 1f);
            SetNamePlate(playerNameText, playerName, 1f);
            SetNamePlate(enemyNameText, enemyName, 1f);
            EnsureStartButtonReceivesInput();

            isVsIdleAnimating = true;
            try
            {
                await WaitForMatchupStartAsync(cancellationToken);
            }
            finally
            {
                isVsIdleAnimating = false;
                EndEnemyStrengthSelect();
                statusPanel?.SetButtonVisible(false);
                statusPanel?.HideDetail();
            }
        }

        /// <summary>
        /// VSを短く表示して自動的に本番へ進む
        /// </summary>
        public async UniTask ShowVsAndAutoStartAsync(
            string playerName,
            string enemyName,
            CancellationToken cancellationToken)
        {
            PrepareVsPresentation(playerName, enemyName, showStartButton: false);
            statusPanel?.SetButtonVisible(false);
            statusPanel?.HideDetail();
            EndEnemyStrengthSelect();

            if (vsText != null)
            {
                vsText.rectTransform.localScale = Vector3.one * 2.2f;
            }

            float elapsed = 0f;
            while (elapsed < vsPopSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / vsPopSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                SetElementAlpha(vsText, eased);
                SetNamePlate(playerNameText, playerName, eased);
                SetNamePlate(enemyNameText, enemyName, eased);

                if (vsText != null)
                {
                    float scale = Mathf.Lerp(2.2f, 1f, eased);
                    vsText.rectTransform.localScale = Vector3.one * scale;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetElementAlpha(vsText, 1f);
            SetNamePlate(playerNameText, playerName, 1f);
            SetNamePlate(enemyNameText, enemyName, 1f);
            await DelayUnscaledAsync(0.8f, cancellationToken);
            isVsIdleAnimating = false;
        }

        private void PrepareVsPresentation(string playerName, string enemyName, bool showStartButton)
        {
            EnsureMatchupUiReady();
            canvasTransition?.ReleasePresentationInput();
            DisableDecorativeRaycasts();
            ApplyMatchupVisualStyle();
            HidePhaseTexts();
            SetElementAlpha(vsText, 0f);
            SetNamePlate(playerNameText, playerName, 0f);
            SetNamePlate(enemyNameText, enemyName, 0f);
            SetCanvasVisible(true);
            SetStartButtonVisible(showStartButton);
            SetVsNamePlatesVisible(true);
            if (showStartButton)
            {
                EnsureStartButtonReceivesInput();
            }
        }

        private void EnsureStartButtonReceivesInput()
        {
            if (startButton == null)
            {
                return;
            }

            canvasTransition?.ReleasePresentationInput();
            startButton.transform.SetAsLastSibling();
            startButton.interactable = true;
            CanvasVisibilityUtility.SetUiVisible(startButton.gameObject, true);

            Graphic[] graphics = startButton.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                // ボタン本体のImageだけレイキャストを受けラベルは奪わない
                graphics[i].raycastTarget = graphics[i].gameObject == startButton.gameObject;
            }

            if (overlayCanvas != null)
            {
                overlayCanvas.enabled = true;
                GraphicRaycaster raycaster = overlayCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    raycaster.enabled = true;
                }

                if (overlayCanvas.TryGetComponent(out CanvasGroup canvasGroup))
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                    canvasGroup.ignoreParentGroups = true;
                }
            }
        }

        private void DisableDecorativeRaycasts()
        {
            SetGraphicRaycast(vsText, false);
            SetGraphicRaycast(readyText, false);
            SetGraphicRaycast(fightText, false);
            SetGraphicRaycast(finishText, false);
            SetGraphicRaycast(victoryTitleText, false);
            SetGraphicRaycast(victoryNameText, false);
            SetGraphicRaycast(playerNameText, false);
            SetGraphicRaycast(enemyNameText, false);

            if (finishFlashImage != null)
            {
                finishFlashImage.raycastTarget = false;
            }
        }

        private static void SetGraphicRaycast(TMP_Text text, bool enabled)
        {
            if (text != null)
            {
                text.raycastTarget = enabled;
            }
        }

        /// <summary>
        /// VS画面のUIを隠す
        /// </summary>
        public void HideVsUi()
        {
            SetStartButtonVisible(false);
            EndEnemyStrengthSelect();
            statusPanel?.SetButtonVisible(false);
            statusPanel?.HideDetail();
            SetVsNamePlatesVisible(false);
            SetElementAlpha(vsText, 0f);
            SetNamePlate(playerNameText, null, 0f);
            SetNamePlate(enemyNameText, null, 0f);
            isVsIdleAnimating = false;
        }

        private void BeginEnemyStrengthSelect(
            BattleUnit player,
            BattleUnit enemy,
            Func<EnemyStrengthTier, BattleUnit> rebuildEnemyWithStrength,
            EnemyStrengthTier initialStrengthTier)
        {
            if (rebuildEnemyWithStrength == null)
            {
                EndEnemyStrengthSelect();
                return;
            }

            if (enemyStrengthSelect == null)
            {
                Debug.LogError(
                    "[BattleStartOverlayView] enemyStrengthSelectが未配線ですPrefab ModeでBattleEnemyStrengthSelectを配置し接続してください",
                    this);
                return;
            }

            BattleUnit boundPlayer = player;
            BattleUnit boundEnemy = enemy;
            enemyStrengthSelect.Show(
                initialStrengthTier,
                tier =>
                {
                    BattleUnit rebuilt = rebuildEnemyWithStrength(tier);
                    if (rebuilt == null)
                    {
                        return;
                    }

                    boundEnemy = rebuilt;
                    BindStatusPanel(boundPlayer, boundEnemy);
                    enemyStrengthSelect.SetSelected(tier);
                });
        }

        private void EndEnemyStrengthSelect()
        {
            enemyStrengthSelect?.Hide();
        }

        private void BindStatusPanel(BattleUnit player, BattleUnit enemy)
        {
            if (statusPanel == null)
            {
                Debug.LogError(
                    "[BattleStartOverlayView] statusPanelが未配線ですPrefab ModeでBattleMatchupStatusPanelを配置し接続してください",
                    this);
                return;
            }

            statusPanel.Bind(player, enemy);
            statusPanel.SetDetailVisibilityListener(OnStatusDetailVisibilityChanged);
            statusPanel.SetButtonVisible(true);
        }

        private void OnStatusDetailVisibilityChanged(bool isDetailVisible)
        {
            // パラメーター表示中は背面のVS UIを出さない
            if (isDetailVisible)
            {
                SetCanvasVisible(false);
                enemyStrengthSelect?.SetTemporaryVisible(false);
                return;
            }

            if (!isVsIdleAnimating)
            {
                return;
            }

            SetCanvasVisible(true);
            enemyStrengthSelect?.SetTemporaryVisible(true);
            EnsureStartButtonReceivesInput();
        }

        /// <summary>
        /// ReadyからFightまで再生する
        /// </summary>
        public async UniTask PlayReadyFightAsync(CancellationToken cancellationToken)
        {
            ValidateSceneUi();
            HideVsUi();
            SetCanvasVisible(true);
            SetElementAlpha(readyText, 0f);
            SetElementAlpha(fightText, 0f);

            await FadeTextAsync(readyText, 1f, readyFadeSeconds, cancellationToken);
            await DelayUnscaledAsync(readyHoldSeconds, cancellationToken);
            SetElementAlpha(readyText, 0f);

            if (fightText != null)
            {
                fightText.rectTransform.localScale = Vector3.one * 1.8f;
            }

            float elapsed = 0f;
            while (elapsed < fightPopSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fightPopSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                SetElementAlpha(fightText, eased);

                if (fightText != null)
                {
                    float scale = Mathf.Lerp(1.8f, 1f, eased);
                    fightText.rectTransform.localScale = Vector3.one * scale;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetElementAlpha(fightText, 1f);
            await DelayUnscaledAsync(fightHoldSeconds, cancellationToken);
            await HideAsync(hideSeconds, cancellationToken);
        }

        /// <summary>
        /// 勝利ラベルと勝者名を表示する
        /// 戻る選択までWinnerとモンスター名を残す
        /// </summary>
        /// <param name="winnerName">勝者名</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask PlayVictoryPresentationAsync(string winnerName, CancellationToken cancellationToken)
        {
            ValidateSceneUi();
            isVictoryPresentationActive = true;
            HideVsUi();
            HidePhaseTexts();
            SetCanvasVisible(true);
            DisableDecorativeRaycasts();

            if (overlayCanvas != null)
            {
                overlayCanvas.overrideSorting = true;
                overlayCanvas.sortingOrder = 1000;
            }

            TitleClayUiVisualUtility.EnsureTextFontOnly(victoryTitleText);
            TitleClayUiVisualUtility.EnsureTextFontOnly(victoryNameText);
            // オーバーレイ配下に親付けしシーン退場で紙吹雪も破棄する
            victoryConfetti.Play(transform);

            bool isDraw = winnerName == "引き分け" || string.IsNullOrWhiteSpace(winnerName);
            string title = isDraw ? "Draw" : "Winner";
            EnsureVictoryLabelVisible(victoryTitleText, title, 0f);
            if (!isDraw)
            {
                EnsureVictoryLabelVisible(victoryNameText, winnerName, 0f);
            }
            else
            {
                EnsureVictoryLabelVisible(victoryNameText, string.Empty, 0f);
            }

            if (victoryTitleText != null)
            {
                victoryTitleText.rectTransform.localScale = Vector3.one * 2f;
            }

            float elapsed = 0f;
            while (elapsed < victoryPopSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / victoryPopSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                EnsureVictoryLabelVisible(victoryTitleText, title, eased);
                if (!isDraw)
                {
                    EnsureVictoryLabelVisible(victoryNameText, winnerName, eased);
                }

                if (victoryTitleText != null)
                {
                    float scale = Mathf.Lerp(2f, 1f, eased);
                    victoryTitleText.rectTransform.localScale = Vector3.one * scale;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            EnsureVictoryLabelVisible(victoryTitleText, title, 1f);
            if (!isDraw)
            {
                EnsureVictoryLabelVisible(victoryNameText, winnerName, 1f);
            }

            await DelayUnscaledAsync(victoryHoldSeconds, cancellationToken);

            // 戻るボタン待ち中もWinnerとモンスター名を残す
            SetOverlayRaycastEnabled(false);
            SetCanvasVisible(true);
            EnsureVictoryLabelVisible(victoryTitleText, title, 1f);
            if (!isDraw)
            {
                EnsureVictoryLabelVisible(victoryNameText, winnerName, 1f);
            }
        }

        /// <summary>
        /// 勝利演出の表示と紙吹雪を終了する
        /// </summary>
        public void EndVictoryPresentation()
        {
            isVictoryPresentationActive = false;
            victoryConfetti.Dispose();
            HideVictoryLabels();
            SetCanvasVisible(false);
        }

        /// <summary>
        /// シーン退場時に勝利演出と紙吹雪を強制終了する
        /// </summary>
        public void HideForLeave()
        {
            EndVictoryPresentation();
            CancelPartBreakPresentation();
            SetStartButtonVisible(false);
            SetVsNamePlatesVisible(false);
            HidePhaseTexts();
            HideFinishImmediate();
            isVsIdleAnimating = false;
            HideVsUi();
            SetCanvasVisible(false);
        }

        private static void EnsureVictoryLabelVisible(TMP_Text text, string content, float alpha)
        {
            if (text == null)
            {
                return;
            }

            bool hasContent = !string.IsNullOrWhiteSpace(content);
            text.text = hasContent ? content : string.Empty;
            text.alpha = alpha;

            // シーン初期状態で非アクティブな場合があるため表示時に有効化する
            if (hasContent && alpha > 0.001f && !text.gameObject.activeSelf)
            {
                text.gameObject.SetActive(true);
            }
            else if ((!hasContent || alpha <= 0.001f) && text.gameObject.activeSelf)
            {
                text.alpha = 0f;
            }
        }

        /// <inheritdoc/>
        public async UniTask PlayFinishAsync(CancellationToken cancellationToken)
        {
            isFinishPresentationActive = true;
            CancelPartBreakPresentation();
            HidePartBreakImmediate();
            ValidateSceneUi();
            HideVsUi();
            HidePhaseTexts();
            RestoreFinishLabel();

            if (overlayCanvas != null)
            {
                overlayCanvas.sortingOrder = 300;
            }

            await RevealOverlayAsync(cancellationToken);

            if (finishFlashImage != null)
            {
                finishFlashImage.color = new Color(1f, 0.25f, 0.15f, 0f);
            }

            if (finishText != null)
            {
                finishText.rectTransform.localScale = Vector3.one * 2.2f;
                finishText.color = new Color(1f, 0.35f, 0.12f, 0f);
            }

            float flashElapsed = 0f;
            while (flashElapsed < finishFlashSeconds)
            {
                flashElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(flashElapsed / finishFlashSeconds);
                float flashAlpha = t < 0.35f
                    ? Mathf.Lerp(0f, 0.55f, t / 0.35f)
                    : Mathf.Lerp(0.55f, 0f, (t - 0.35f) / 0.65f);

                if (finishFlashImage != null)
                {
                    Color flashColor = finishFlashImage.color;
                    flashColor.a = flashAlpha;
                    finishFlashImage.color = flashColor;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (finishFlashImage != null)
            {
                Color flashColor = finishFlashImage.color;
                flashColor.a = 0f;
                finishFlashImage.color = flashColor;
            }

            float popElapsed = 0f;
            while (popElapsed < finishPopSeconds)
            {
                popElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(popElapsed / finishPopSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                SetElementAlpha(finishText, eased);

                if (finishText != null)
                {
                    float scale = Mathf.Lerp(2.2f, 1f, eased);
                    finishText.rectTransform.localScale = Vector3.one * scale;
                    finishText.color = Color.Lerp(
                        new Color(1f, 0.35f, 0.12f, eased),
                        new Color(1f, 0.82f, 0.2f, eased),
                        eased * 0.35f);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetElementAlpha(finishText, 1f);
            if (finishText != null)
            {
                finishText.color = new Color(1f, 0.82f, 0.2f, 1f);
            }

            float holdElapsed = 0f;
            while (holdElapsed < finishHoldSeconds)
            {
                holdElapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            float fadeElapsed = 0f;
            while (fadeElapsed < finishFadeSeconds)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(fadeElapsed / finishFadeSeconds);
                SetElementAlpha(finishText, 1f - t);

                if (finishFlashImage != null)
                {
                    Color flashColor = finishFlashImage.color;
                    flashColor.a = Mathf.Lerp(0f, 0.25f, t);
                    finishFlashImage.color = flashColor;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            HideFinishImmediate();
        }

        /// <inheritdoc/>
        public async UniTask PlayPartBreakAsync(CancellationToken cancellationToken)
        {
            if (isFinishPresentationActive || isVictoryPresentationActive)
            {
                return;
            }

            CancelPartBreakPresentation();
            partBreakCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationTokenSource localCts = partBreakCts;
            CancellationToken token = localCts.Token;

            try
            {
                if (isFinishPresentationActive || isVictoryPresentationActive)
                {
                    return;
                }

                ValidateSceneUi();
                HideVsUi();
                HidePhaseTexts();

                if (overlayCanvas != null)
                {
                    overlayCanvas.sortingOrder = 300;
                }

                SetCanvasVisible(true);
                SetOverlayRaycastEnabled(false);

                if (finishText != null)
                {
                    finishText.text = BreakLabel;
                    finishText.rectTransform.localScale = Vector3.one * 2.2f;
                    finishText.color = new Color(1f, 0.35f, 0.12f, 0f);
                }

                if (finishFlashImage != null)
                {
                    finishFlashImage.color = new Color(1f, 0.25f, 0.15f, 0f);
                }

                float flashElapsed = 0f;
                while (flashElapsed < breakFlashSeconds)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    if (isFinishPresentationActive)
                    {
                        return;
                    }

                    flashElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(flashElapsed / breakFlashSeconds);
                    float flashAlpha = t < 0.35f
                        ? Mathf.Lerp(0f, 0.4f, t / 0.35f)
                        : Mathf.Lerp(0.4f, 0f, (t - 0.35f) / 0.65f);

                    if (finishFlashImage != null)
                    {
                        Color flashColor = finishFlashImage.color;
                        flashColor.a = flashAlpha;
                        finishFlashImage.color = flashColor;
                    }
                }

                if (isFinishPresentationActive)
                {
                    return;
                }

                if (finishFlashImage != null)
                {
                    Color flashColor = finishFlashImage.color;
                    flashColor.a = 0f;
                    finishFlashImage.color = flashColor;
                }

                float popElapsed = 0f;
                while (popElapsed < breakPopSeconds)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    if (isFinishPresentationActive)
                    {
                        return;
                    }

                    popElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(popElapsed / breakPopSeconds);
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    SetElementAlpha(finishText, eased);

                    if (finishText != null)
                    {
                        float scale = Mathf.Lerp(2.2f, 1f, eased);
                        finishText.rectTransform.localScale = Vector3.one * scale;
                        finishText.color = Color.Lerp(
                            new Color(1f, 0.35f, 0.12f, eased),
                            new Color(1f, 0.82f, 0.2f, eased),
                            eased * 0.35f);
                    }
                }

                if (isFinishPresentationActive)
                {
                    return;
                }

                SetElementAlpha(finishText, 1f);
                if (finishText != null)
                {
                    finishText.color = new Color(1f, 0.82f, 0.2f, 1f);
                }

                float holdElapsed = 0f;
                while (holdElapsed < breakHoldSeconds)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    if (isFinishPresentationActive)
                    {
                        return;
                    }

                    holdElapsed += Time.unscaledDeltaTime;
                }

                float fadeElapsed = 0f;
                while (fadeElapsed < breakFadeSeconds)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    if (isFinishPresentationActive)
                    {
                        return;
                    }

                    fadeElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(fadeElapsed / breakFadeSeconds);
                    SetElementAlpha(finishText, 1f - t);
                }

                if (!isFinishPresentationActive)
                {
                    HidePartBreakImmediate();
                }
            }
            catch (OperationCanceledException)
            {
                if (!isFinishPresentationActive)
                {
                    RestoreFinishLabel();
                    SetElementAlpha(finishText, 0f);
                    if (finishFlashImage != null)
                    {
                        Color flashColor = finishFlashImage.color;
                        flashColor.a = 0f;
                        finishFlashImage.color = flashColor;
                    }
                }
            }
            finally
            {
                if (ReferenceEquals(partBreakCts, localCts))
                {
                    partBreakCts.Dispose();
                    partBreakCts = null;
                }
            }
        }

        /// <inheritdoc/>
        public void HideFinishImmediate()
        {
            isFinishPresentationActive = false;
            RestoreFinishLabel();
            SetElementAlpha(finishText, 0f);

            if (finishFlashImage != null)
            {
                Color flashColor = finishFlashImage.color;
                flashColor.a = 0f;
                finishFlashImage.color = flashColor;
            }

            if (overlayCanvas != null)
            {
                overlayCanvas.enabled = false;
            }
        }

        /// <inheritdoc/>
        public void HidePartBreakImmediate()
        {
            RestoreFinishLabel();
            SetElementAlpha(finishText, 0f);

            if (finishFlashImage != null)
            {
                Color flashColor = finishFlashImage.color;
                flashColor.a = 0f;
                finishFlashImage.color = flashColor;
            }

            if (isFinishPresentationActive)
            {
                return;
            }

            if (overlayCanvas != null)
            {
                overlayCanvas.enabled = false;
            }
        }

        private void CancelPartBreakPresentation()
        {
            if (partBreakCts == null)
            {
                return;
            }

            partBreakCts.Cancel();
            partBreakCts.Dispose();
            partBreakCts = null;
        }

        private void RestoreFinishLabel()
        {
            if (finishText != null)
            {
                finishText.text = FinishLabel;
            }
        }

        /// <summary>
        /// オーバーレイを隠す
        /// </summary>
        public async UniTask HideAsync(float fadeSeconds, CancellationToken cancellationToken)
        {
            if (fadeSeconds > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }

            HideImmediate();
        }

        /// <summary>
        /// オーバーレイを即座に隠す
        /// </summary>
        public void HideImmediate()
        {
            if (isVictoryPresentationActive)
            {
                // 勝利表示中はWinnerとモンスター名を維持する
                CancelPartBreakPresentation();
                SetStartButtonVisible(false);
                EndEnemyStrengthSelect();
                statusPanel?.SetButtonVisible(false);
                statusPanel?.HideDetail();
                SetVsNamePlatesVisible(false);
                HidePhaseTexts();
                HideFinishImmediate();
                isVsIdleAnimating = false;
                return;
            }

            isVictoryPresentationActive = false;
            victoryConfetti.Dispose();
            CancelPartBreakPresentation();
            SetStartButtonVisible(false);
            EndEnemyStrengthSelect();
            statusPanel?.SetButtonVisible(false);
            statusPanel?.HideDetail();
            SetVsNamePlatesVisible(false);
            HidePhaseTexts();
            HideVictoryLabels();
            HideFinishImmediate();
            isVsIdleAnimating = false;
            SetCanvasVisible(false);
        }

        private void OnDisable()
        {
            victoryConfetti.Dispose();
        }

        private void OnDestroy()
        {
            CancelPartBreakPresentation();
            victoryConfetti.Dispose();
        }

        private void ApplyMatchupVisualStyle()
        {
            TitleClayUiVisualUtility.ApplyMatchupVsText(vsText);
            TitleClayUiVisualUtility.ApplyMatchupReadyText(readyText);
            TitleClayUiVisualUtility.ApplyMatchupFightText(fightText);
            TitleClayUiVisualUtility.ApplyMatchupNameLabel(playerNameText, true);
            TitleClayUiVisualUtility.ApplyMatchupNameLabel(enemyNameText, false);
            TitleClayUiVisualUtility.EnsureTextFontOnly(victoryTitleText);
            TitleClayUiVisualUtility.EnsureTextFontOnly(victoryNameText);
        }

        private async UniTask WaitForMatchupStartAsync(CancellationToken cancellationToken)
        {
            EnsureStartButtonReceivesInput();

            if (startButton == null)
            {
                await DelayUnscaledAsync(1f, cancellationToken);
                return;
            }

            await UniTask.WhenAny(
                WaitForStartButtonAsync(cancellationToken),
                WaitForAdvanceKeyAsync(cancellationToken));
        }

        private static async UniTask WaitForAdvanceKeyAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Keyboard keyboard = Keyboard.current;
                if (keyboard != null
                    && (keyboard.enterKey.wasPressedThisFrame
                        || keyboard.numpadEnterKey.wasPressedThisFrame
                        || keyboard.spaceKey.wasPressedThisFrame))
                {
                    return;
                }

                Gamepad gamepad = Gamepad.current;
                if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private async UniTask WaitForStartButtonAsync(CancellationToken cancellationToken)
        {
            if (startButton == null)
            {
                await DelayUnscaledAsync(1f, cancellationToken);
                return;
            }

            bool pressed = false;
            void OnClick() => pressed = true;
            startButton.onClick.AddListener(OnClick);

            try
            {
                await UniTask.WaitUntil(() => pressed, cancellationToken: cancellationToken);
            }
            finally
            {
                startButton.onClick.RemoveListener(OnClick);
            }
        }

        private void HidePhaseTexts()
        {
            SetElementAlpha(vsText, 0f);
            SetElementAlpha(readyText, 0f);
            SetElementAlpha(fightText, 0f);
            SetElementAlpha(finishText, 0f);
            if (!isVictoryPresentationActive)
            {
                HideVictoryLabels();
            }
        }

        private void HideVictoryLabels()
        {
            SetElementAlpha(victoryTitleText, 0f);
            if (victoryTitleText != null)
            {
                victoryTitleText.gameObject.SetActive(false);
            }

            SetElementAlpha(victoryNameText, 0f);
            if (victoryNameText != null)
            {
                victoryNameText.gameObject.SetActive(false);
            }
        }

        private void SetStartButtonVisible(bool isVisible)
        {
            if (startButton == null)
            {
                return;
            }

            startButton.interactable = isVisible;
            CanvasVisibilityUtility.SetUiVisible(startButton.gameObject, isVisible);
        }

        private async UniTask RevealOverlayAsync(CancellationToken cancellationToken)
        {
            if (canvasTransition != null)
            {
                await canvasTransition.FadeOutAsync(cancellationToken);
            }

            SetCanvasVisible(true);

            if (canvasTransition != null)
            {
                await canvasTransition.FadeInAsync(cancellationToken);
            }
        }

        private void SetCanvasVisible(bool isVisible)
        {
            if (isVisible)
            {
                FixOverlayCanvasLayout();
            }

            if (overlayCanvas != null)
            {
                overlayCanvas.enabled = isVisible;
            }

            SetOverlayRaycastEnabled(isVisible);
        }

        private void SetOverlayRaycastEnabled(bool enabled)
        {
            if (overlayCanvas == null)
            {
                return;
            }

            GraphicRaycaster raycaster = overlayCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = enabled;
            }
        }

        private static async UniTask FadeTextAsync(TMP_Text text, float targetAlpha, float duration, CancellationToken cancellationToken)
        {
            if (text == null)
            {
                return;
            }

            float start = text.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                text.alpha = Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            text.alpha = targetAlpha;
        }

        private static void SetElementAlpha(TMP_Text text, float alpha)
        {
            if (text != null)
            {
                text.alpha = alpha;
            }
        }

        private static void SetNamePlate(TMP_Text text, string displayName, float alpha)
        {
            if (text == null)
            {
                return;
            }

            text.text = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
            text.alpha = alpha;
            text.gameObject.SetActive(alpha > 0.001f && !string.IsNullOrWhiteSpace(displayName));
        }

        private void SetVsNamePlatesVisible(bool isVisible)
        {
            if (vsNamePlateRoot != null)
            {
                vsNamePlateRoot.SetActive(isVisible);
            }
        }

        private void FixOverlayCanvasLayout()
        {
            if (overlayCanvas == null)
            {
                return;
            }

            RectTransform canvasRect = overlayCanvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            FixZeroScaleAncestors(canvasRect);

            if (canvasRect.localScale.sqrMagnitude < 0.001f)
            {
                canvasRect.localScale = Vector3.one;
            }

            overlayCanvas.overrideSorting = true;
            if (overlayCanvas.sortingOrder < 1000)
            {
                overlayCanvas.sortingOrder = 1000;
            }

            if (overlayCanvas.TryGetComponent(out CanvasGroup canvasGroup))
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = overlayCanvas.enabled;
            }
        }

        private static void FixZeroScaleAncestors(RectTransform canvasRect)
        {
            if (canvasRect == null)
            {
                return;
            }

            Transform current = canvasRect;
            while (current != null)
            {
                if (current is RectTransform rect && rect.localScale.sqrMagnitude < 0.001f)
                {
                    rect.localScale = Vector3.one;
                }

                current = current.parent;
            }
        }

        private void ValidateSceneUi()
        {
            if (overlayCanvas == null
                || vsText == null
                || readyText == null
                || fightText == null
                || finishText == null
                || finishFlashImage == null
                || startButton == null
                || playerNameText == null
                || enemyNameText == null
                || victoryTitleText == null
                || victoryNameText == null)
            {
                Debug.LogError(
                    "[BattleStartOverlayView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",
                    this);
            }
        }

        private static async UniTask DelayUnscaledAsync(float seconds, CancellationToken cancellationToken)
        {
            if (seconds <= 0f)
            {
                return;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
}
