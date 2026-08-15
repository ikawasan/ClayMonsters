using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// トーナメントブラケットUIの実装
    /// Canvas.enabledで表示切替する
    /// </summary>
    public sealed class NpcTournamentBracketView : MonoBehaviour, INpcTournamentBracketView, ILanguageAwareUi
    {
        private const float RiseDurationSeconds = 0.85f;
        private const float ShakeDurationSeconds = 0.4f;
        private const float ShakeAmplitude = 10f;
        private const float DarkenDurationSeconds = 0.85f;
        private const float ChampionGlowDurationSeconds = 0.95f;
        private const float MeetSideOffset = 28f;
        private const int DisplaySortingOrder = 800;
        private const int ConfirmSortingOrder = 820;

        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private NpcTournamentPanZoom panZoom;
        [SerializeField] private RectTransform content;
        [SerializeField] private NpcTournamentFighterSlotView[] leafSlots = new NpcTournamentFighterSlotView[8];
        [SerializeField] private NpcTournamentFighterSlotView[] quarterSlots = new NpcTournamentFighterSlotView[4];
        [SerializeField] private NpcTournamentFighterSlotView[] semiSlots = new NpcTournamentFighterSlotView[2];
        [SerializeField] private NpcTournamentFighterSlotView finalSlot;
        [SerializeField] private Image travelerA;
        [SerializeField] private Image travelerB;
        [SerializeField] private NpcTournamentBracketLines bracketLines;
        [SerializeField] private NpcTournamentLineBinder[] legacyLines;
        [SerializeField] private LHButton titleReturnButton;
        [SerializeField] private Canvas confirmCanvas;
        [SerializeField] private TMP_Text confirmMessageText;
        [SerializeField] private LHButton confirmYesButton;
        [SerializeField] private LHButton confirmNoButton;
        [SerializeField] private RectTransform championCrown;
        [SerializeField] private float championCrownRaisedY = 268f;

        private bool waitingAdvanceClick;
        private bool advanceClickReceived;
        private bool abortToTitleRequested;
        private bool confirmVisible;
        private bool championRewardMode;
        private bool championRewardAcknowledged;
        private int championRewardPoints;
        private bool travelersElevated;
        private int elevatedLeftLeaf = -1;
        private int elevatedRightLeaf = -1;
        private int elevatedRound = -1;
        private int elevatedMatchIndex = -1;
        private Vector2 elevatedLeftMeet;
        private Vector2 elevatedRightMeet;
        private int pendingMatchLeftLeaf = -1;
        private int pendingMatchRightLeaf = -1;
        private bool suppressFinalSlotVisual;
        private Vector2 championRiseFrom;
        private Vector2 championRiseTo;
        private int championRiseWinnerLeaf = -1;
        private Vector2 championCrownRestPosition;
        private bool championCrownRestCaptured;
        private Vector2 confirmYesDualPosition;
        private bool confirmYesDualPositionCaptured;
        private IDisposable titleReturnSubscription;
        private IDisposable confirmYesSubscription;
        private IDisposable confirmNoSubscription;
        private IDisposable languageSubscription;
        private string titleReturnOriginal = "タイトルへ戻る";
        private string confirmYesOriginal = "はい";
        private string confirmNoOriginal = "いいえ";
        private bool labelOriginalsCaptured;

        /// <inheritdoc/>
        public bool IsConfigured =>
            rootCanvas != null
            && leafSlots != null
            && leafSlots.Length == NpcTournamentBracket.FighterCount
            && quarterSlots != null
            && quarterSlots.Length == NpcTournamentBracket.QuarterMatchCount
            && semiSlots != null
            && semiSlots.Length == NpcTournamentBracket.SemiMatchCount
            && finalSlot != null
            && travelerA != null
            && travelerB != null
            && titleReturnButton != null
            && confirmCanvas != null
            && confirmMessageText != null
            && confirmYesButton != null
            && confirmNoButton != null;

        private void Awake()
        {
            if (!IsConfigured)
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 必須参照が未配線ですHierarchyで接続してください"
                    + $" canvas={(rootCanvas != null)}"
                    + $" leaf={(leafSlots != null ? leafSlots.Length : 0)}"
                    + $" quarter={(quarterSlots != null ? quarterSlots.Length : 0)}"
                    + $" semi={(semiSlots != null ? semiSlots.Length : 0)}"
                    + $" final={(finalSlot != null)}"
                    + $" travelerA={(travelerA != null)}"
                    + $" travelerB={(travelerB != null)}"
                    + $" titleReturn={(titleReturnButton != null)}"
                    + $" confirmCanvas={(confirmCanvas != null)}"
                    + $" confirmMessage={(confirmMessageText != null)}"
                    + $" confirmYes={(confirmYesButton != null)}"
                    + $" confirmNo={(confirmNoButton != null)}",
                    this);
            }

            BindButtons();
            languageSubscription = LanguageAwareUi.RegisterAware(this);
            CaptureLabelOriginalsIfNeeded();
            ApplyLocalizedLabels();
            EnsureBracketLines();
            CaptureChampionCrownRestPosition();
            ResetChampionCrownToRest();
            ClearAdvanceSlotVisuals();
            HideConfirm();
            HideTravelers();
            HideImmediate();
        }

        private void OnDestroy()
        {
            titleReturnSubscription?.Dispose();
            confirmYesSubscription?.Dispose();
            confirmNoSubscription?.Dispose();
            languageSubscription?.Dispose();
        }

        private void Update()
        {
            if (!waitingAdvanceClick || advanceClickReceived || abortToTitleRequested || confirmVisible)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasReleasedThisFrame)
            {
                return;
            }

            if (IsPointerOverUiButton())
            {
                return;
            }

            if (panZoom != null && panZoom.ConsumePanForClickBlock())
            {
                return;
            }

            advanceClickReceived = true;
        }

        /// <inheritdoc/>
        public void Show(NpcTournamentBracket bracket)
        {
            if (!IsConfigured)
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 未配線のため表示できません",
                    this);
                return;
            }

            PrepareBracketContent(bracket);
            ResetChampionCrownToRest();
            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, true, DisplaySortingOrder);
            Canvas.ForceUpdateCanvases();
            Debug.Log("[NpcTournamentBracketView] ブラケットを表示しました");
        }

        /// <inheritdoc/>
        public void FocusPlayerEntry(NpcTournamentBracket bracket)
        {
            if (panZoom == null)
            {
                return;
            }

            RectTransform target = ResolvePlayerFocusRect(bracket);
            if (target == null)
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 入場フォーカス対象のサムネがありません",
                    this);
                return;
            }

            Canvas.ForceUpdateCanvases();
            panZoom.FocusOn(target);
        }

        private RectTransform ResolvePlayerFocusRect(NpcTournamentBracket bracket)
        {
            int playerLeaf = bracket != null ? bracket.PlayerLeafIndex : 0;
            if (bracket != null)
            {
                int round = bracket.CurrentRound;
                if (round >= 2)
                {
                    for (int i = 0; i < NpcTournamentBracket.SemiMatchCount; i++)
                    {
                        if (bracket.GetSemiWinner(i) == playerLeaf)
                        {
                            RectTransform semi = GetSemiRect(i);
                            if (semi != null)
                            {
                                return semi;
                            }
                        }
                    }
                }

                if (round >= 1)
                {
                    for (int i = 0; i < NpcTournamentBracket.QuarterMatchCount; i++)
                    {
                        if (bracket.GetQuarterWinner(i) == playerLeaf)
                        {
                            RectTransform quarter = GetQuarterRect(i);
                            if (quarter != null)
                            {
                                return quarter;
                            }
                        }
                    }
                }
            }

            NpcTournamentFighterSlotView leafSlot = GetLeafSlot(playerLeaf);
            return leafSlot != null ? leafSlot.RectTransform : null;
        }

        /// <inheritdoc/>
        public void Hide()
        {
            waitingAdvanceClick = false;
            advanceClickReceived = false;
            abortToTitleRequested = false;
            championRewardMode = false;
            championRewardAcknowledged = false;
            RestoreConfirmNoButton();
            HideConfirm();
            // elevated状態は維持する(プレイヤー試合の戦闘後解決で使う)
            CanvasVisibilityUtility.SetCanvasEnabled(rootCanvas, false);
        }

        /// <inheritdoc/>
        public void HideImmediate()
        {
            Hide();
        }

        /// <inheritdoc/>
        public async UniTask ShowChampionRewardAsync(
            int points,
            CancellationToken cancellationToken)
        {
            if (confirmCanvas == null || confirmMessageText == null || confirmYesButton == null)
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 優勝報酬ウィンドウが未配線です",
                    this);
                return;
            }

            championRewardPoints = Mathf.Max(0, points);
            championRewardAcknowledged = false;
            championRewardMode = true;
            waitingAdvanceClick = false;
            advanceClickReceived = false;
            abortToTitleRequested = false;
            SetConfirmNoButtonVisible(false);
            ShowConfirm();

            try
            {
                await UniTask.WaitUntil(
                    () => championRewardAcknowledged,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                championRewardMode = false;
                championRewardAcknowledged = false;
                RestoreConfirmNoButton();
                HideConfirm();
                ApplyLocalizedLabels();
            }
        }

        private void PrepareBracketContent(NpcTournamentBracket bracket)
        {
            EnsureCanvasLayout();
            EnsureBracketLines();
            if (travelersElevated)
            {
                // 戦闘後復帰では合流トラベラーを出さない
                // 明転前に解決へ進むため元枠だけ隠して待つ
                HideTravelers();
                HideElevatedSourceThumbnails();
                RefreshDefeated(bracket);
                RefreshAdvanceSlots(bracket);
                HideElevatedSourceThumbnails();
            }
            else
            {
                HideTravelers();
                ClearElevatedState();
                RefreshDefeated(bracket);
                RefreshAdvanceSlots(bracket);
            }

            HideConfirm();
            ApplyLocalizedLabels();
            bracketLines?.Apply();
        }

        /// <inheritdoc/>
        public async UniTask<NpcTournamentBracketWaitResult> WaitForAdvanceOrAbortAsync(
            CancellationToken cancellationToken)
        {
            advanceClickReceived = false;
            abortToTitleRequested = false;
            waitingAdvanceClick = false;
            HideConfirm();
            panZoom?.ConsumePanForClickBlock();

            // 直前UIのクリック解放を1フレーム捨てる
            await UniTask.NextFrame(cancellationToken);
            await UniTask.WaitUntil(
                () =>
                {
                    Mouse mouse = Mouse.current;
                    return mouse == null || !mouse.leftButton.isPressed;
                },
                cancellationToken: cancellationToken);
            await UniTask.NextFrame(cancellationToken);

            waitingAdvanceClick = true;
            try
            {
                await UniTask.WaitUntil(
                    () => advanceClickReceived || abortToTitleRequested,
                    cancellationToken: cancellationToken);
                return abortToTitleRequested
                    ? NpcTournamentBracketWaitResult.AbortToTitle
                    : NpcTournamentBracketWaitResult.Advance;
            }
            finally
            {
                waitingAdvanceClick = false;
                advanceClickReceived = false;
                abortToTitleRequested = false;
                HideConfirm();
            }
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
            if (confirmVisible)
            {
                ApplyConfirmMessage();
            }
        }

        /// <inheritdoc/>
        public void SetLeafThumbnail(int leafIndex, Sprite sprite, string displayName)
        {
            if (leafSlots == null || leafIndex < 0 || leafIndex >= leafSlots.Length)
            {
                return;
            }

            leafSlots[leafIndex]?.SetContent(sprite, displayName);
        }

        /// <inheritdoc/>
        public void RefreshDefeated(NpcTournamentBracket bracket)
        {
            if (bracket == null || leafSlots == null)
            {
                return;
            }

            for (int i = 0; i < leafSlots.Length; i++)
            {
                NpcTournamentFighter fighter = i < bracket.Leaves.Count ? bracket.Leaves[i] : null;
                leafSlots[i]?.SetDefeated(fighter != null && fighter.IsDefeated);
            }
        }

        /// <inheritdoc/>
        public void RefreshAdvanceSlots(NpcTournamentBracket bracket)
        {
            if (bracket == null)
            {
                return;
            }

            for (int i = 0; i < quarterSlots.Length; i++)
            {
                int winnerLeaf = bracket.GetQuarterWinner(i);
                if (winnerLeaf >= 0 && ResolveThumbnailDisplayTier(bracket, winnerLeaf) == 0)
                {
                    ApplyAdvanceSlot(quarterSlots[i], winnerLeaf, bracket);
                }
                else
                {
                    ClearSlotVisual(quarterSlots[i]);
                }
            }

            for (int i = 0; i < semiSlots.Length; i++)
            {
                int winnerLeaf = bracket.GetSemiWinner(i);
                if (winnerLeaf >= 0 && ResolveThumbnailDisplayTier(bracket, winnerLeaf) == 1)
                {
                    ApplyAdvanceSlot(semiSlots[i], winnerLeaf, bracket);
                }
                else
                {
                    ClearSlotVisual(semiSlots[i]);
                }
            }

            int championLeaf = bracket.ChampionLeafIndex;
            if (championLeaf >= 0
                && ResolveThumbnailDisplayTier(bracket, championLeaf) == 2
                && !suppressFinalSlotVisual)
            {
                ApplyAdvanceSlot(finalSlot, championLeaf, bracket);
            }
            else
            {
                ClearSlotVisual(finalSlot);
            }

            ApplyLeafVisibilityAfterAdvance(bracket);
        }

        /// <inheritdoc/>
        public async UniTask PlayTravelRiseAndShakeAsync(
            int round,
            int matchIndex,
            int leftLeaf,
            int rightLeaf,
            bool restoreSlotsAfter,
            CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                return;
            }

            HideConfirm();
            HideTravelers();
            ClearElevatedState();
            RectTransform leftStart = ResolveFighterRect(round, matchIndex, leftLeaf, isLeft: true);
            RectTransform rightStart = ResolveFighterRect(round, matchIndex, rightLeaf, isLeft: false);
            RectTransform meet = ResolveMeetRect(round, matchIndex);
            if (leftStart == null || rightStart == null || meet == null)
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 上昇演出のスロットRectが不足しています"
                    + $" round={round} match={matchIndex}",
                    this);
                return;
            }

            NpcTournamentFighterSlotView leftSlot = ResolveFighterSlot(
                round,
                matchIndex,
                leftLeaf,
                isLeft: true);
            NpcTournamentFighterSlotView rightSlot = ResolveFighterSlot(
                round,
                matchIndex,
                rightLeaf,
                isLeft: false);
            Sprite leftSprite = ResolveFighterSprite(leftSlot, leftLeaf);
            Sprite rightSprite = ResolveFighterSprite(rightSlot, rightLeaf);
            if (leftSprite == null || rightSprite == null)
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 移動用サムネが不足しています"
                    + $" left={(leftSprite != null)} right={(rightSprite != null)}",
                    this);
                return;
            }

            // サムネ中心から開始(線のfromTop点へ飛ばない)
            Vector2 leftStartPt = ToContentPoint(leftStart, new Vector2(0.5f, 0.5f));
            Vector2 rightStartPt = ToContentPoint(rightStart, new Vector2(0.5f, 0.5f));

            // 線の肘・合流Yはブラケット線と同じ計算(全段中心起点)
            if (!TryResolveBracketPath(
                    leftStart,
                    rightStart,
                    meet,
                    fromTop: false,
                    out _,
                    out _,
                    out Vector2 leftElbow,
                    out Vector2 rightElbow,
                    out Vector2 stemBase,
                    out Vector2 parentCenter))
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 上昇経路の計算に失敗しました",
                    this);
                return;
            }

            // 左右で同じstemBaseに集まらず合流水平線上で向き合う
            // 茎への再上昇はしない(水平のあと上がるのは不要)
            Vector2 leftMeet = new Vector2(parentCenter.x - MeetSideOffset, stemBase.y);
            Vector2 rightMeet = new Vector2(parentCenter.x + MeetSideOffset, stemBase.y);

            leftSlot?.SetThumbnailVisible(false);
            rightSlot?.SetThumbnailVisible(false);
            PrepareTraveler(travelerA, leftSprite, leftStartPt);
            PrepareTraveler(travelerB, rightSprite, rightStartPt);

            // 上昇→水平合流のみ(左右同時)
            float riseSeconds = RiseDurationSeconds * 0.55f;
            float mergeSeconds = RiseDurationSeconds * 0.45f;
            await MoveDualTravelersAlongPathsAsync(
                travelerA,
                new[] { leftStartPt, leftElbow },
                travelerB,
                new[] { rightStartPt, rightElbow },
                riseSeconds,
                cancellationToken);
            await MoveDualTravelersAlongPathsAsync(
                travelerA,
                new[] { leftElbow, leftMeet },
                travelerB,
                new[] { rightElbow, rightMeet },
                mergeSeconds,
                cancellationToken);
            SetTravelerContentPosition(travelerA, leftMeet);
            SetTravelerContentPosition(travelerB, rightMeet);
            FocusCameraOnTravelersMidpoint();
            await ShakeTravelersAsync(cancellationToken);

            travelersElevated = true;
            elevatedLeftLeaf = leftLeaf;
            elevatedRightLeaf = rightLeaf;
            elevatedRound = round;
            elevatedMatchIndex = matchIndex;
            elevatedLeftMeet = leftMeet;
            elevatedRightMeet = rightMeet;
            pendingMatchLeftLeaf = leftLeaf;
            pendingMatchRightLeaf = rightLeaf;

            if (restoreSlotsAfter)
            {
                // elevatedは維持しトラベラー表示だけ一時的に消す
                HideTravelers();
            }
        }

        /// <inheritdoc/>
        public async UniTask PlayPostBattleResolveAsync(
            int round,
            int matchIndex,
            int winnerLeaf,
            int loserLeaf,
            CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                return;
            }

            HideConfirm();
            NpcTournamentFighterSlotView winnerLeafSlot = GetLeafSlot(winnerLeaf);
            NpcTournamentFighterSlotView loserLeafSlot = GetLeafSlot(loserLeaf);
            NpcTournamentFighterSlotView advanceSlot = ResolveAdvanceSlotView(round, matchIndex);

            if (!TryResolveMatchSides(
                    round,
                    matchIndex,
                    winnerLeaf,
                    loserLeaf,
                    out _,
                    out bool loserIsLeft,
                    out int leftLeaf,
                    out int rightLeaf))
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 試合の左右が解決できないため解決演出をスキップします",
                    this);
                PlaceWinnerOnAdvanceSlot(advanceSlot, winnerLeafSlot, winnerLeaf);
                HideTravelers();
                ClearElevatedState();
                return;
            }

            NpcTournamentFighterSlotView loserSourceSlot = ResolveFighterSlot(
                round,
                matchIndex,
                loserLeaf,
                loserIsLeft);

            if (travelersElevated
                && elevatedRound == round
                && elevatedMatchIndex == matchIndex
                && (winnerLeaf == elevatedLeftLeaf || winnerLeaf == elevatedRightLeaf))
            {
                await PlayElevatedResolveAsync(
                    round,
                    matchIndex,
                    winnerLeaf,
                    winnerLeafSlot,
                    loserLeafSlot,
                    loserSourceSlot,
                    advanceSlot,
                    cancellationToken);
                return;
            }

            // フォールバック:元枠で暗転し勝者を次枠へ定着する
            await DarkenLoserOnSourceAndPlaceWinnerAsync(
                round,
                matchIndex,
                leftLeaf,
                rightLeaf,
                winnerLeaf,
                winnerLeafSlot,
                loserLeafSlot,
                loserSourceSlot,
                advanceSlot,
                cancellationToken);
        }

        /// <inheritdoc/>
        public async UniTask PrepareChampionAtIntersectionAsync(
            int round,
            int matchIndex,
            int winnerLeaf,
            int loserLeaf,
            CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                return;
            }

            HideConfirm();
            NpcTournamentFighterSlotView winnerLeafSlot = GetLeafSlot(winnerLeaf);
            NpcTournamentFighterSlotView loserLeafSlot = GetLeafSlot(loserLeaf);
            if (!TryResolveMatchSides(
                    round,
                    matchIndex,
                    winnerLeaf,
                    loserLeaf,
                    out _,
                    out bool loserIsLeft,
                    out int leftLeaf,
                    out int rightLeaf))
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 決勝の左右が解決できないため交差配置をスキップします",
                    this);
                return;
            }

            NpcTournamentFighterSlotView loserSourceSlot = ResolveFighterSlot(
                round,
                matchIndex,
                loserLeaf,
                loserIsLeft);
            RectTransform leftRect = ResolveFighterRect(round, matchIndex, leftLeaf, isLeft: true);
            RectTransform rightRect = ResolveFighterRect(round, matchIndex, rightLeaf, isLeft: false);
            RectTransform finalRect = finalSlot != null ? finalSlot.RectTransform : null;
            if (leftRect == null
                || rightRect == null
                || finalRect == null
                || !TryResolveBracketPath(
                    leftRect,
                    rightRect,
                    finalRect,
                    fromTop: false,
                    out _,
                    out _,
                    out _,
                    out _,
                    out Vector2 stemBase,
                    out Vector2 parentCenter))
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 決勝交差位置の計算に失敗しました",
                    this);
                return;
            }

            Sprite winnerSprite = ResolveFighterSprite(winnerLeafSlot, winnerLeaf);
            if (winnerSprite == null)
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 優勝サムネがありません",
                    this);
                return;
            }

            HideTravelers();
            HideMatchSourceThumbnails(round, matchIndex, leftLeaf, rightLeaf);
            ClearSlotVisual(finalSlot);
            suppressFinalSlotVisual = true;
            championRiseFrom = stemBase;
            championRiseTo = parentCenter;
            championRiseWinnerLeaf = winnerLeaf;

            NpcTournamentFighterSlotView darkenSlot = loserSourceSlot != null
                ? loserSourceSlot
                : loserLeafSlot;
            if (darkenSlot != null)
            {
                darkenSlot.SetThumbnailVisible(true);
                await darkenSlot.AnimateDarkenAsync(DarkenDurationSeconds, cancellationToken);
            }
            else
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(DarkenDurationSeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: cancellationToken);
            }

            PrepareTraveler(travelerA, winnerSprite, stemBase);
            if (travelerB != null)
            {
                HideTraveler(travelerB);
            }

            ClearElevatedState();
        }

        /// <inheritdoc/>
        public async UniTask PlayChampionRiseAsync(
            int winnerLeaf,
            CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                return;
            }

            NpcTournamentFighterSlotView winnerLeafSlot = GetLeafSlot(winnerLeaf);
            Sprite winnerSprite = ResolveFighterSprite(winnerLeafSlot, winnerLeaf);
            if (winnerSprite == null)
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 優勝上昇用サムネがありません",
                    this);
                suppressFinalSlotVisual = false;
                return;
            }

            Vector2 from = championRiseFrom;
            Vector2 to = championRiseTo;
            if (finalSlot != null)
            {
                to = ToContentPoint(finalSlot.RectTransform, new Vector2(0.5f, 0.5f));
            }

            PrepareTraveler(travelerA, winnerSprite, from);
            if (travelerB != null)
            {
                HideTraveler(travelerB);
            }

            ClearSlotVisual(finalSlot);
            UniTask travelerMove = MoveTravelerAlongPathAsync(
                travelerA,
                new[] { from, to },
                RiseDurationSeconds,
                cancellationToken,
                followCamera: true);
            UniTask crownRise = PlayChampionCrownRiseAsync(cancellationToken);
            await UniTask.WhenAll(travelerMove, crownRise);
            PlaceWinnerOnAdvanceSlot(finalSlot, winnerLeafSlot, winnerLeaf);
            HideTravelers();
            suppressFinalSlotVisual = false;
            championRiseWinnerLeaf = -1;
            if (finalSlot != null)
            {
                FocusCameraOnRect(finalSlot.RectTransform);
            }

            await PlayChampionCrownGlowAsync(cancellationToken);
        }

        private void CaptureChampionCrownRestPosition()
        {
            if (championCrown == null)
            {
                championCrownRestCaptured = false;
                return;
            }

            championCrownRestPosition = championCrown.anchoredPosition;
            championCrownRestCaptured = true;
        }

        private void ResetChampionCrownToRest()
        {
            if (championCrown == null || !championCrownRestCaptured)
            {
                return;
            }

            championCrown.anchoredPosition = championCrownRestPosition;
            Image crownImage = championCrown.GetComponent<Image>();
            if (crownImage != null)
            {
                crownImage.color = Color.white;
            }

            championCrown.localScale = Vector3.one;
        }

        private async UniTask PlayChampionCrownRiseAsync(CancellationToken cancellationToken)
        {
            if (championCrown == null || !championCrownRestCaptured)
            {
                return;
            }

            Vector2 start = championCrown.anchoredPosition;
            Vector2 end = new Vector2(start.x, championCrownRaisedY);
            if (Mathf.Approximately(start.y, end.y))
            {
                return;
            }

            float elapsed = 0f;
            while (elapsed < RiseDurationSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / RiseDurationSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                championCrown.anchoredPosition = Vector2.LerpUnclamped(start, end, eased);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            championCrown.anchoredPosition = end;
        }

        private async UniTask PlayChampionCrownGlowAsync(CancellationToken cancellationToken)
        {
            if (championCrown == null)
            {
                return;
            }

            Image crownImage = championCrown.GetComponent<Image>();
            if (crownImage == null || !crownImage.enabled)
            {
                return;
            }

            Color baseColor = Color.white;
            Color peakColor = new Color(1f, 0.95f, 0.55f, 1f);
            Vector3 baseScale = championCrown.localScale;
            const float scaleAmplitude = 0.18f;

            float duration = Mathf.Max(0.01f, ChampionGlowDurationSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float glow = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f));
                crownImage.color = Color.Lerp(baseColor, peakColor, glow);
                championCrown.localScale = baseScale * (1f + (scaleAmplitude * glow));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            crownImage.color = baseColor;
            championCrown.localScale = baseScale;
        }

        private bool TryResolveMatchSides(
            int round,
            int matchIndex,
            int winnerLeaf,
            int loserLeaf,
            out bool winnerIsLeft,
            out bool loserIsLeft,
            out int leftLeaf,
            out int rightLeaf)
        {
            winnerIsLeft = false;
            loserIsLeft = false;
            leftLeaf = -1;
            rightLeaf = -1;

            if (pendingMatchLeftLeaf >= 0 && pendingMatchRightLeaf >= 0)
            {
                leftLeaf = pendingMatchLeftLeaf;
                rightLeaf = pendingMatchRightLeaf;
            }
            else if (elevatedLeftLeaf >= 0 && elevatedRightLeaf >= 0)
            {
                leftLeaf = elevatedLeftLeaf;
                rightLeaf = elevatedRightLeaf;
            }
            else if (round == 0
                && matchIndex >= 0
                && matchIndex < NpcTournamentBracket.QuarterMatchCount)
            {
                leftLeaf = matchIndex * 2;
                rightLeaf = leftLeaf + 1;
            }
            else
            {
                return false;
            }

            if (winnerLeaf == leftLeaf && loserLeaf == rightLeaf)
            {
                winnerIsLeft = true;
                loserIsLeft = false;
                return true;
            }

            if (winnerLeaf == rightLeaf && loserLeaf == leftLeaf)
            {
                winnerIsLeft = false;
                loserIsLeft = true;
                return true;
            }

            return false;
        }

        private void HideElevatedSourceThumbnails()
        {
            if (!travelersElevated)
            {
                return;
            }

            HideMatchSourceThumbnails(
                elevatedRound,
                elevatedMatchIndex,
                elevatedLeftLeaf,
                elevatedRightLeaf);
        }

        private void ShowElevatedTravelersAtMeet()
        {
            if (!travelersElevated)
            {
                return;
            }

            ShowElevatedTraveler(travelerA, elevatedLeftMeet);
            ShowElevatedTraveler(travelerB, elevatedRightMeet);
        }

        private void ShowElevatedTraveler(Image traveler, Vector2 meetPosition)
        {
            if (traveler == null)
            {
                return;
            }

            RectTransform root = ResolveTravelerRoot(traveler);
            if (traveler.sprite == null)
            {
                ApplyTravelerChrome(root, hasContent: false);
                if (root != null)
                {
                    root.gameObject.SetActive(false);
                }

                return;
            }

            ApplyTravelerChrome(root, hasContent: true);
            if (root != null && !root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
            }

            traveler.enabled = true;
            traveler.color = Color.white;
            SetTravelerContentPosition(traveler, meetPosition);
        }

        private void HideMatchSourceThumbnails(
            int round,
            int matchIndex,
            int leftLeaf,
            int rightLeaf)
        {
            ResolveFighterSlot(round, matchIndex, leftLeaf, isLeft: true)
                ?.SetThumbnailVisible(false);
            ResolveFighterSlot(round, matchIndex, rightLeaf, isLeft: false)
                ?.SetThumbnailVisible(false);
            GetLeafSlot(leftLeaf)?.SetThumbnailVisible(false);
            GetLeafSlot(rightLeaf)?.SetThumbnailVisible(false);
        }

        private async UniTask DarkenLoserOnSourceAndPlaceWinnerAsync(
            int round,
            int matchIndex,
            int leftLeaf,
            int rightLeaf,
            int winnerLeaf,
            NpcTournamentFighterSlotView winnerLeafSlot,
            NpcTournamentFighterSlotView loserLeafSlot,
            NpcTournamentFighterSlotView loserSourceSlot,
            NpcTournamentFighterSlotView advanceSlot,
            CancellationToken cancellationToken)
        {
            // 先に合流トラベラーを消し元枠を空ける
            // 勝者進出と同時に出すと一瞬位置が重なって見える
            HideTravelers();
            HideMatchSourceThumbnails(round, matchIndex, leftLeaf, rightLeaf);
            PlaceWinnerOnAdvanceSlot(advanceSlot, winnerLeafSlot, winnerLeaf);

            // 敗者は元枠の位置で暗転する
            NpcTournamentFighterSlotView darkenSlot = loserSourceSlot != null
                ? loserSourceSlot
                : loserLeafSlot;
            if (darkenSlot != null)
            {
                darkenSlot.SetThumbnailVisible(true);
                await darkenSlot.AnimateDarkenAsync(DarkenDurationSeconds, cancellationToken);
            }
            else
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(DarkenDurationSeconds),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: cancellationToken);
            }

            ClearElevatedState();
        }

        private async UniTask PlayElevatedResolveAsync(
            int round,
            int matchIndex,
            int winnerLeaf,
            NpcTournamentFighterSlotView winnerLeafSlot,
            NpcTournamentFighterSlotView loserLeafSlot,
            NpcTournamentFighterSlotView loserSourceSlot,
            NpcTournamentFighterSlotView advanceSlot,
            CancellationToken cancellationToken)
        {
            await DarkenLoserOnSourceAndPlaceWinnerAsync(
                round,
                matchIndex,
                elevatedLeftLeaf,
                elevatedRightLeaf,
                winnerLeaf,
                winnerLeafSlot,
                loserLeafSlot,
                loserSourceSlot,
                advanceSlot,
                cancellationToken);
        }

        private void ClearElevatedState()
        {
            travelersElevated = false;
            elevatedLeftLeaf = -1;
            elevatedRightLeaf = -1;
            elevatedRound = -1;
            elevatedMatchIndex = -1;
            pendingMatchLeftLeaf = -1;
            pendingMatchRightLeaf = -1;
        }

        private async UniTask MoveDualTravelersAlongPathsAsync(
            Image leftTraveler,
            Vector2[] leftPath,
            Image rightTraveler,
            Vector2[] rightPath,
            float durationSeconds,
            CancellationToken cancellationToken)
        {
            if (!TryBuildPathMotion(leftPath, out float leftLength, out Vector2 leftEnd)
                || !TryBuildPathMotion(rightPath, out float rightLength, out Vector2 rightEnd))
            {
                if (leftTraveler != null && leftPath != null && leftPath.Length > 0)
                {
                    SetTravelerContentPosition(leftTraveler, leftPath[leftPath.Length - 1]);
                }

                if (rightTraveler != null && rightPath != null && rightPath.Length > 0)
                {
                    SetTravelerContentPosition(rightTraveler, rightPath[rightPath.Length - 1]);
                }

                FocusCameraOnTravelersMidpoint();
                return;
            }

            float duration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - (2f * t));
                if (leftTraveler != null)
                {
                    SetTravelerContentPosition(
                        leftTraveler,
                        SamplePath(leftPath, eased * leftLength));
                }

                if (rightTraveler != null)
                {
                    SetTravelerContentPosition(
                        rightTraveler,
                        SamplePath(rightPath, eased * rightLength));
                }

                FocusCameraOnTravelersMidpoint();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (leftTraveler != null)
            {
                SetTravelerContentPosition(leftTraveler, leftEnd);
            }

            if (rightTraveler != null)
            {
                SetTravelerContentPosition(rightTraveler, rightEnd);
            }

            FocusCameraOnTravelersMidpoint();
        }

        private async UniTask MoveTravelerAlongPathAsync(
            Image traveler,
            Vector2[] path,
            float durationSeconds,
            CancellationToken cancellationToken,
            bool followCamera = false)
        {
            if (traveler == null || path == null || path.Length == 0)
            {
                return;
            }

            if (!TryBuildPathMotion(path, out float totalLength, out Vector2 end))
            {
                SetTravelerContentPosition(traveler, path[path.Length - 1]);
                if (followCamera)
                {
                    FocusCameraOnTraveler(traveler);
                }

                return;
            }

            float duration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - (2f * t));
                SetTravelerContentPosition(traveler, SamplePath(path, eased * totalLength));
                if (followCamera)
                {
                    FocusCameraOnTraveler(traveler);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetTravelerContentPosition(traveler, end);
            if (followCamera)
            {
                FocusCameraOnTraveler(traveler);
            }
        }

        private static bool TryBuildPathMotion(
            Vector2[] path,
            out float totalLength,
            out Vector2 end)
        {
            totalLength = 0f;
            end = default;
            if (path == null || path.Length == 0)
            {
                return false;
            }

            end = path[path.Length - 1];
            if (path.Length == 1)
            {
                return false;
            }

            for (int i = 1; i < path.Length; i++)
            {
                totalLength += Vector2.Distance(path[i - 1], path[i]);
            }

            return totalLength >= 0.01f;
        }

        private static Vector2 SamplePath(Vector2[] path, float distanceAlong)
        {
            float remaining = Mathf.Max(0f, distanceAlong);
            for (int i = 1; i < path.Length; i++)
            {
                float segment = Vector2.Distance(path[i - 1], path[i]);
                if (segment < 0.01f)
                {
                    continue;
                }

                if (remaining <= segment)
                {
                    return Vector2.Lerp(path[i - 1], path[i], remaining / segment);
                }

                remaining -= segment;
            }

            return path[path.Length - 1];
        }

        private async UniTask ShakeTravelersAsync(CancellationToken cancellationToken)
        {
            Vector2 baseA = GetTravelerContentPosition(travelerA);
            Vector2 baseB = GetTravelerContentPosition(travelerB);
            float elapsed = 0f;
            while (elapsed < ShakeDurationSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float waveX = Mathf.Sin(elapsed * 58f) * ShakeAmplitude;
                float waveY = Mathf.Cos(elapsed * 49f) * (ShakeAmplitude * 0.7f);
                SetTravelerContentPosition(travelerA, baseA + new Vector2(waveX, waveY));
                SetTravelerContentPosition(travelerB, baseB + new Vector2(-waveX, -waveY));
                FocusCameraOnTravelersMidpoint();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            SetTravelerContentPosition(travelerA, baseA);
            SetTravelerContentPosition(travelerB, baseB);
            FocusCameraOnTravelersMidpoint();
        }

        private void FocusCameraOnTraveler(Image traveler)
        {
            if (panZoom == null || traveler == null)
            {
                return;
            }

            panZoom.FocusOnContentPoint(GetTravelerContentPosition(traveler));
        }

        private void FocusCameraOnTravelersMidpoint()
        {
            if (panZoom == null)
            {
                return;
            }

            Vector2 a = GetTravelerContentPosition(travelerA);
            Vector2 b = GetTravelerContentPosition(travelerB);
            panZoom.FocusOnContentPoint((a + b) * 0.5f);
        }

        private void FocusCameraOnRect(RectTransform target)
        {
            if (panZoom == null || target == null || content == null)
            {
                return;
            }

            panZoom.FocusOnContentPoint(ToContentPoint(target, new Vector2(0.5f, 0.5f)));
        }

        private bool TryResolveBracketPath(
            RectTransform left,
            RectTransform right,
            RectTransform parent,
            bool fromTop,
            out Vector2 leftStart,
            out Vector2 rightStart,
            out Vector2 leftElbow,
            out Vector2 rightElbow,
            out Vector2 stemBase,
            out Vector2 parentCenter)
        {
            leftStart = default;
            rightStart = default;
            leftElbow = default;
            rightElbow = default;
            stemBase = default;
            parentCenter = default;
            if (left == null || right == null || parent == null)
            {
                return false;
            }

            Vector2 childPivot = fromTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
            leftStart = ToContentPoint(left, childPivot);
            rightStart = ToContentPoint(right, childPivot);
            parentCenter = ToContentPoint(parent, new Vector2(0.5f, 0.5f));
            float mergeY = NpcTournamentBracketLines.ResolveMergeYWithMinStem(
                Mathf.Max(leftStart.y, rightStart.y),
                parentCenter.y);
            leftElbow = new Vector2(leftStart.x, mergeY);
            rightElbow = new Vector2(rightStart.x, mergeY);
            stemBase = new Vector2(parentCenter.x, mergeY);
            return true;
        }

        private Vector2 ToContentPoint(RectTransform target, Vector2 normalizedPivot)
        {
            if (target == null)
            {
                return Vector2.zero;
            }

            if (content == null)
            {
                return target.anchoredPosition;
            }

            Rect rect = target.rect;
            Vector2 local = new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, normalizedPivot.x),
                Mathf.Lerp(rect.yMin, rect.yMax, normalizedPivot.y));
            Vector3 world = target.TransformPoint(local);
            return content.InverseTransformPoint(world);
        }

        private Vector2 GetTravelerContentPosition(Image traveler)
        {
            RectTransform root = ResolveTravelerRoot(traveler);
            if (root == null)
            {
                return Vector2.zero;
            }

            if (content == null)
            {
                return root.anchoredPosition;
            }

            return content.InverseTransformPoint(root.position);
        }

        private static void SetTravelerContentPosition(Image traveler, Vector2 contentLocal)
        {
            RectTransform root = ResolveTravelerRoot(traveler);
            if (root == null)
            {
                return;
            }

            // content配下ならanchoredPositionでcontentローカルと一致させる
            root.anchoredPosition = contentLocal;
        }

        /// <summary>
        /// 移動と表示切替の対象Rectを返す
        /// ThumbnailMask配下ならその親をルートにする
        /// </summary>
        /// <param name="traveler">サムネImage</param>
        /// <returns>ルートRect</returns>
        private static RectTransform ResolveTravelerRoot(Image traveler)
        {
            if (traveler == null)
            {
                return null;
            }

            Transform parent = traveler.transform.parent;
            if (parent != null
                && parent.name == "ThumbnailMask"
                && parent.parent is RectTransform maskParentRoot)
            {
                return maskParentRoot;
            }

            return traveler.rectTransform;
        }

        private void BindButtons()
        {
            titleReturnSubscription?.Dispose();
            confirmYesSubscription?.Dispose();
            confirmNoSubscription?.Dispose();

            titleReturnButton?.EnsureUiSoundFeedback();
            confirmYesButton?.EnsureUiSoundFeedback();
            confirmNoButton?.EnsureUiSoundFeedback();

            titleReturnSubscription = titleReturnButton != null
                ? titleReturnButton.SubscribeOnClick(OnClickTitleReturn)
                : null;
            confirmYesSubscription = confirmYesButton != null
                ? confirmYesButton.SubscribeOnClick(OnClickConfirmYes)
                : null;
            confirmNoSubscription = confirmNoButton != null
                ? confirmNoButton.SubscribeOnClick(OnClickConfirmNo)
                : null;
        }

        private void OnClickTitleReturn()
        {
            if (!waitingAdvanceClick
                || confirmVisible
                || abortToTitleRequested
                || championRewardMode)
            {
                return;
            }

            advanceClickReceived = false;
            ShowConfirm();
        }

        private void OnClickConfirmYes()
        {
            if (!confirmVisible)
            {
                return;
            }

            if (championRewardMode)
            {
                HideConfirm();
                championRewardAcknowledged = true;
                return;
            }

            advanceClickReceived = false;
            HideConfirm();
            abortToTitleRequested = true;
        }

        private void OnClickConfirmNo()
        {
            if (!confirmVisible || championRewardMode)
            {
                return;
            }

            advanceClickReceived = false;
            HideConfirm();
            panZoom?.ConsumePanForClickBlock();
        }

        private bool IsPointerOverUiButton()
        {
            if (EventSystem.current == null || Mouse.current == null)
            {
                return false;
            }

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue(),
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].gameObject != null
                    && results[i].gameObject.GetComponentInParent<Button>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ShowConfirm()
        {
            if (confirmCanvas == null || confirmMessageText == null)
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] 確認ウィンドウが未配線です",
                    this);
                return;
            }

            if (!championRewardMode)
            {
                RestoreConfirmNoButton();
            }

            confirmVisible = true;
            ApplyConfirmMessage();
            ApplyLocalizedLabels();
            ApplyConfirmYesButtonPosition();
            CanvasVisibilityUtility.SetCanvasEnabled(
                confirmCanvas,
                true,
                ConfirmSortingOrder);
        }

        private void HideConfirm()
        {
            confirmVisible = false;
            RestoreConfirmYesButtonPosition();
            CanvasVisibilityUtility.SetCanvasEnabled(confirmCanvas, false);
        }

        private void ApplyConfirmMessage()
        {
            if (confirmMessageText == null)
            {
                return;
            }

            if (championRewardMode)
            {
                confirmMessageText.text = LocalizedText.GetOrFallback(
                    GameTextKeys.NpcTournamentChampionReward,
                    "優勝おめでとうございます。\n{points}ポイントを入手しました。",
                    "points",
                    championRewardPoints);
                return;
            }

            confirmMessageText.text = LocalizedText.GetOrFallback(
                GameTextKeys.NpcTournamentAbortConfirm,
                "中断してタイトルへ戻りますか？");
        }

        private void ApplyLocalizedLabels()
        {
            CaptureLabelOriginalsIfNeeded();
            if (titleReturnButton != null)
            {
                LhButtonLabelUtility.SetLabel(
                    titleReturnButton,
                    SceneLocalizedLabel.Resolve(
                        GameTextKeys.TrainingHudBackToTitle,
                        titleReturnOriginal));
            }

            if (confirmYesButton != null)
            {
                string yesLabel = championRewardMode
                    ? SceneLocalizedLabel.Resolve(GameTextKeys.CommonOk, "OK")
                    : SceneLocalizedLabel.Resolve(GameTextKeys.CommonYes, confirmYesOriginal);
                LhButtonLabelUtility.SetLabel(confirmYesButton, yesLabel);
            }

            if (confirmNoButton != null && !championRewardMode)
            {
                LhButtonLabelUtility.SetLabel(
                    confirmNoButton,
                    SceneLocalizedLabel.Resolve(GameTextKeys.CommonNo, confirmNoOriginal));
            }
        }

        private void SetConfirmNoButtonVisible(bool visible)
        {
            if (confirmNoButton == null)
            {
                return;
            }

            Canvas noCanvas = confirmNoButton.GetComponent<Canvas>();
            if (noCanvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(noCanvas, visible);
                return;
            }

            confirmNoButton.gameObject.SetActive(visible);
        }

        private void RestoreConfirmNoButton()
        {
            SetConfirmNoButtonVisible(true);
            RestoreConfirmYesButtonPosition();
        }

        private void CaptureConfirmYesDualPositionIfNeeded()
        {
            if (confirmYesDualPositionCaptured || confirmYesButton == null)
            {
                return;
            }

            RectTransform yesRect = confirmYesButton.transform as RectTransform;
            if (yesRect == null)
            {
                return;
            }

            confirmYesDualPosition = yesRect.anchoredPosition;
            confirmYesDualPositionCaptured = true;
        }

        private void ApplyConfirmYesButtonPosition()
        {
            CaptureConfirmYesDualPositionIfNeeded();
            if (!confirmYesDualPositionCaptured || confirmYesButton == null)
            {
                return;
            }

            RectTransform yesRect = confirmYesButton.transform as RectTransform;
            if (yesRect == null)
            {
                return;
            }

            // 優勝OKのみ中央はい/いいえ並びはシーン配置のまま
            yesRect.anchoredPosition = championRewardMode
                ? new Vector2(0f, confirmYesDualPosition.y)
                : confirmYesDualPosition;
        }

        private void RestoreConfirmYesButtonPosition()
        {
            if (!confirmYesDualPositionCaptured || confirmYesButton == null)
            {
                return;
            }

            RectTransform yesRect = confirmYesButton.transform as RectTransform;
            if (yesRect == null)
            {
                return;
            }

            yesRect.anchoredPosition = confirmYesDualPosition;
        }

        private void CaptureLabelOriginalsIfNeeded()
        {
            if (labelOriginalsCaptured)
            {
                return;
            }

            titleReturnOriginal = SceneLocalizedLabel.Capture(titleReturnButton, titleReturnOriginal);
            confirmYesOriginal = SceneLocalizedLabel.Capture(confirmYesButton, confirmYesOriginal);
            confirmNoOriginal = SceneLocalizedLabel.Capture(confirmNoButton, confirmNoOriginal);
            labelOriginalsCaptured = true;
        }

        private void EnsureBracketLines()
        {
            if (bracketLines == null)
            {
                Debug.LogError(
                    "[NpcTournamentBracketView] NpcTournamentBracketLinesが未配線です",
                    this);
                return;
            }

            DisableLegacyLines();
            bracketLines.Bind(
                BuildSlotRects(leafSlots),
                BuildSlotRects(quarterSlots),
                BuildSlotRects(semiSlots),
                finalSlot != null ? finalSlot.RectTransform : null);
            bracketLines.Apply();
        }

        private void DisableLegacyLines()
        {
            if (legacyLines == null)
            {
                return;
            }

            for (int i = 0; i < legacyLines.Length; i++)
            {
                NpcTournamentLineBinder legacy = legacyLines[i];
                if (legacy == null)
                {
                    continue;
                }

                legacy.enabled = false;
            }
        }

        private static RectTransform[] BuildSlotRects(NpcTournamentFighterSlotView[] slots)
        {
            if (slots == null)
            {
                return Array.Empty<RectTransform>();
            }

            var rects = new RectTransform[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                rects[i] = slots[i] != null ? slots[i].RectTransform : null;
            }

            return rects;
        }

        private void ClearAdvanceSlotVisuals()
        {
            if (quarterSlots != null)
            {
                for (int i = 0; i < quarterSlots.Length; i++)
                {
                    quarterSlots[i]?.SetContent(null, string.Empty);
                    quarterSlots[i]?.SetThumbnailVisible(false);
                }
            }

            if (semiSlots != null)
            {
                for (int i = 0; i < semiSlots.Length; i++)
                {
                    semiSlots[i]?.SetContent(null, string.Empty);
                    semiSlots[i]?.SetThumbnailVisible(false);
                }
            }

            if (finalSlot != null)
            {
                finalSlot.SetContent(null, string.Empty);
                finalSlot.SetThumbnailVisible(false);
            }
        }

        private void ApplyAdvanceSlot(
            NpcTournamentFighterSlotView slot,
            int winnerLeaf,
            NpcTournamentBracket bracket = null)
        {
            if (slot == null)
            {
                return;
            }

            if (winnerLeaf < 0)
            {
                slot.SetContent(null, string.Empty);
                slot.SetThumbnailVisible(false);
                return;
            }

            NpcTournamentFighterSlotView source = GetLeafSlot(winnerLeaf);
            if (source == null)
            {
                slot.SetContent(null, string.Empty);
                slot.SetThumbnailVisible(false);
                return;
            }

            slot.CopyFrom(source);

            // 合流中の元枠は中身だけ持たせて非表示(表復帰時の一瞬ズレ防止)
            if (travelersElevated && IsElevatedSourceSlot(slot))
            {
                slot.SetDefeated(false);
                slot.SetThumbnailVisible(false);
                return;
            }

            // 後続で敗退してもその進出枠に暗転して残す(画像のトーナメント表と同じ)
            bool defeated = false;
            if (bracket != null
                && winnerLeaf < bracket.Leaves.Count
                && bracket.Leaves[winnerLeaf] != null)
            {
                defeated = bracket.Leaves[winnerLeaf].IsDefeated;
            }

            slot.SetDefeated(defeated);
            slot.SetThumbnailVisible(true);
        }

        private bool IsElevatedSourceSlot(NpcTournamentFighterSlotView slot)
        {
            if (!travelersElevated || slot == null)
            {
                return false;
            }

            NpcTournamentFighterSlotView leftSource = ResolveFighterSlot(
                elevatedRound,
                elevatedMatchIndex,
                elevatedLeftLeaf,
                isLeft: true);
            NpcTournamentFighterSlotView rightSource = ResolveFighterSlot(
                elevatedRound,
                elevatedMatchIndex,
                elevatedRightLeaf,
                isLeft: false);
            return slot == leftSource || slot == rightSource;
        }

        private bool IsElevatedSourceLeaf(int leafIndex)
        {
            return travelersElevated
                && (leafIndex == elevatedLeftLeaf || leafIndex == elevatedRightLeaf);
        }

        private void ApplyLeafVisibilityAfterAdvance(NpcTournamentBracket bracket)
        {
            if (bracket == null || leafSlots == null)
            {
                return;
            }

            for (int i = 0; i < leafSlots.Length; i++)
            {
                NpcTournamentFighterSlotView slot = leafSlots[i];
                if (slot == null)
                {
                    continue;
                }

                if (IsElevatedSourceLeaf(i))
                {
                    slot.SetDefeated(false);
                    slot.SetThumbnailVisible(false);
                    continue;
                }

                NpcTournamentFighter fighter = i < bracket.Leaves.Count ? bracket.Leaves[i] : null;
                bool defeated = fighter != null && fighter.IsDefeated;
                int displayTier = ResolveThumbnailDisplayTier(bracket, i);
                if (displayTier != -1)
                {
                    // 上段に出しているので葉は空ける(1モンスター1枚)
                    slot.SetDefeated(false);
                    slot.SetThumbnailVisible(false);
                }
                else if (defeated)
                {
                    // 初戦敗退は葉に暗転して1枚だけ残す
                    slot.SetDefeated(true);
                    slot.SetThumbnailVisible(slot.ThumbnailSprite != null);
                }
                else
                {
                    slot.SetDefeated(false);
                    slot.SetThumbnailVisible(slot.ThumbnailSprite != null);
                }
            }
        }

        /// <summary>
        /// サムネを出す段を返す(-1葉0準々1準決2決勝)
        /// 1モンスターにつき最も進んだ段だけを選ぶ
        /// </summary>
        private static int ResolveThumbnailDisplayTier(NpcTournamentBracket bracket, int leaf)
        {
            if (bracket == null || leaf < 0)
            {
                return -1;
            }

            if (bracket.ChampionLeafIndex == leaf)
            {
                return 2;
            }

            for (int i = 0; i < NpcTournamentBracket.SemiMatchCount; i++)
            {
                if (bracket.GetSemiWinner(i) == leaf)
                {
                    return 1;
                }
            }

            for (int i = 0; i < NpcTournamentBracket.QuarterMatchCount; i++)
            {
                if (bracket.GetQuarterWinner(i) == leaf)
                {
                    return 0;
                }
            }

            return -1;
        }

        private static void ClearSlotVisual(NpcTournamentFighterSlotView slot)
        {
            if (slot == null)
            {
                return;
            }

            slot.SetContent(null, string.Empty);
            slot.SetThumbnailVisible(false);
        }

        private void PlaceWinnerOnAdvanceSlot(
            NpcTournamentFighterSlotView advanceSlot,
            NpcTournamentFighterSlotView winnerLeafSlot,
            int winnerLeaf)
        {
            if (advanceSlot == null)
            {
                return;
            }

            if (winnerLeafSlot != null)
            {
                advanceSlot.CopyFrom(winnerLeafSlot);
            }
            else
            {
                ApplyAdvanceSlot(advanceSlot, winnerLeaf);
            }

            advanceSlot.SetDefeated(false);
            advanceSlot.SetThumbnailVisible(true);
        }

        private NpcTournamentFighterSlotView ResolveAdvanceSlotView(int round, int matchIndex)
        {
            if (round == 0)
            {
                if (quarterSlots == null || matchIndex < 0 || matchIndex >= quarterSlots.Length)
                {
                    return null;
                }

                return quarterSlots[matchIndex];
            }

            if (round == 1)
            {
                if (semiSlots == null || matchIndex < 0 || matchIndex >= semiSlots.Length)
                {
                    return null;
                }

                return semiSlots[matchIndex];
            }

            return finalSlot;
        }

        private NpcTournamentFighterSlotView ResolveFighterSlot(
            int round,
            int matchIndex,
            int leaf,
            bool isLeft)
        {
            if (round <= 0)
            {
                return GetLeafSlot(leaf);
            }

            if (round == 1)
            {
                int quarterIndex = (matchIndex * 2) + (isLeft ? 0 : 1);
                if (quarterSlots == null
                    || quarterIndex < 0
                    || quarterIndex >= quarterSlots.Length)
                {
                    return GetLeafSlot(leaf);
                }

                return quarterSlots[quarterIndex];
            }

            if (round == 2)
            {
                int semiIndex = isLeft ? 0 : 1;
                if (semiSlots == null || semiIndex < 0 || semiIndex >= semiSlots.Length)
                {
                    return GetLeafSlot(leaf);
                }

                return semiSlots[semiIndex];
            }

            return GetLeafSlot(leaf);
        }

        private RectTransform ResolveFighterRect(
            int round,
            int matchIndex,
            int leaf,
            bool isLeft)
        {
            NpcTournamentFighterSlotView slot = ResolveFighterSlot(round, matchIndex, leaf, isLeft);
            return slot != null ? slot.RectTransform : GetLeafRect(leaf);
        }

        private Sprite ResolveFighterSprite(NpcTournamentFighterSlotView slot, int leaf)
        {
            if (slot != null && slot.ThumbnailSprite != null)
            {
                return slot.ThumbnailSprite;
            }

            NpcTournamentFighterSlotView leafSlot = GetLeafSlot(leaf);
            return leafSlot != null ? leafSlot.ThumbnailSprite : null;
        }

        private NpcTournamentFighterSlotView GetLeafSlot(int leaf)
        {
            if (leafSlots == null || leaf < 0 || leaf >= leafSlots.Length)
            {
                return null;
            }

            return leafSlots[leaf];
        }

        private RectTransform ResolveMeetRect(int round, int matchIndex)
        {
            if (round == 0)
            {
                return GetQuarterRect(matchIndex);
            }

            if (round == 1)
            {
                return GetSemiRect(matchIndex);
            }

            if (round == 2)
            {
                return finalSlot != null ? finalSlot.RectTransform : null;
            }

            return null;
        }

        private RectTransform GetLeafRect(int leaf)
        {
            if (leafSlots == null || leaf < 0 || leaf >= leafSlots.Length || leafSlots[leaf] == null)
            {
                return null;
            }

            return leafSlots[leaf].RectTransform;
        }

        private RectTransform GetQuarterRect(int index)
        {
            if (quarterSlots == null || index < 0 || index >= quarterSlots.Length || quarterSlots[index] == null)
            {
                return null;
            }

            return quarterSlots[index].RectTransform;
        }

        private RectTransform GetSemiRect(int index)
        {
            if (semiSlots == null || index < 0 || index >= semiSlots.Length || semiSlots[index] == null)
            {
                return null;
            }

            return semiSlots[index].RectTransform;
        }

        private static void PrepareTraveler(Image traveler, Sprite sprite, Vector2 position)
        {
            if (traveler == null)
            {
                return;
            }

            RectTransform root = ResolveTravelerRoot(traveler);
            if (sprite == null)
            {
                traveler.sprite = null;
                traveler.enabled = false;
                ApplyTravelerChrome(root, hasContent: false);
                if (root != null)
                {
                    root.gameObject.SetActive(false);
                }

                return;
            }

            traveler.sprite = sprite;
            traveler.color = Color.white;
            traveler.enabled = true;
            traveler.preserveAspect = false;
            traveler.type = Image.Type.Simple;
            traveler.maskable = true;
            ApplyTravelerChrome(root, hasContent: true);
            if (root != null)
            {
                root.gameObject.SetActive(true);
            }

            SetTravelerContentPosition(traveler, position);
        }

        private void HideTravelers()
        {
            // elevated状態は消さない(戦闘後解決で使う)
            HideTraveler(travelerA);
            HideTraveler(travelerB);
        }

        private static void HideTraveler(Image traveler)
        {
            if (traveler == null)
            {
                return;
            }

            traveler.color = Color.white;
            traveler.enabled = false;
            RectTransform root = ResolveTravelerRoot(traveler);
            ApplyTravelerChrome(root, hasContent: false);
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Traveler配下の枠とマスク絵の表示を内容有無に合わせる
        /// </summary>
        /// <param name="root">Travelerルート</param>
        /// <param name="hasContent">サムネがあるか</param>
        private static void ApplyTravelerChrome(RectTransform root, bool hasContent)
        {
            if (root == null)
            {
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.name != "Outline" && child.name != "ThumbnailMask")
                {
                    continue;
                }

                Image image = child.GetComponent<Image>();
                if (image == null)
                {
                    continue;
                }

                image.enabled = hasContent;
                image.raycastTarget = false;
            }
        }

        private void EnsureCanvasLayout()
        {
            if (rootCanvas == null)
            {
                return;
            }

            // レイアウトはシーン配置を正とし実行時は描画順のみ整える
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.worldCamera = null;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = DisplaySortingOrder;
        }
    }
}
