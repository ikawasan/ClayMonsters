using Battle.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using R3;
using SaveData;
using Scene.Core.Interface;
using System.Threading;
using UI.ClayEditor.View;
using UnityEngine;
using VContainer;

namespace Scene.BattleNpcScene
{
    /// <summary>
    /// LoadSlotViewを使ったモンスター選択セッション
    /// 表示制御とロード完了を一括で扱う
    /// </summary>
    public sealed class MonsterSelectionSession : IMonsterSelectionSession
    {
        private readonly LoadSlotView loadSlotView;
        private readonly IBattleCanvasTransition presentationTransition;
        private readonly ISceneFade sceneFade;
        private bool isRevealed;
        private bool selectionCancelled;
        private int selectedSlotIndex = -1;
        private EnemyStrengthTier selectedStrengthTier = EnemyStrengthTier.Normal;

        /// <summary>
        /// DIで依存を受け取る
        /// </summary>
        [Inject]
        public MonsterSelectionSession(
            LoadSlotView loadSlotView,
            IBattleCanvasTransition presentationTransition,
            ISceneFade sceneFade)
        {
            this.loadSlotView = loadSlotView;
            this.presentationTransition = presentationTransition;
            this.sceneFade = sceneFade;
        }

        /// <inheritdoc />
        public ModelSavePool SavePool => loadSlotView != null ? loadSlotView.SavePool : ModelSavePool.TrainedPlayer;

        /// <inheritdoc />
        public int SelectedSlotIndex => selectedSlotIndex;

        /// <inheritdoc />
        public EnemyStrengthTier SelectedStrengthTier => selectedStrengthTier;

        /// <inheritdoc />
        public bool WasSelectionCancelled => selectionCancelled;

        /// <inheritdoc />
        public void PrepareEntry()
        {
            isRevealed = false;
            selectionCancelled = false;
            selectedSlotIndex = -1;
            selectedStrengthTier = EnemyStrengthTier.Normal;
            loadSlotView?.HideForLeave();
            loadSlotView?.ConfigureSavePool(ModelSavePool.TrainedPlayer);
            loadSlotView?.PrepareLayout();
            loadSlotView?.WarmEnemyCatalog();
            // 明転前に育成済みサムネを先読みする
            loadSlotView?.BeginPrefetchTrainedPlayerThumbnails(
                loadSlotView.GetCancellationTokenOnDestroy());
            Hide();
        }

        /// <inheritdoc />
        public void ConfigureSavePool(ModelSavePool pool)
        {
            selectedSlotIndex = -1;
            selectedStrengthTier = EnemyStrengthTier.Normal;
            selectionCancelled = false;
            // 次のWaitForModelAsyncでFadeIn付き再表示が必要なので解除する
            isRevealed = false;
            loadSlotView?.ClearLoadedModelForNewSelection();
            loadSlotView?.InvalidateSelectionContents();
            loadSlotView?.ConfigureSavePool(pool);
        }

        /// <inheritdoc />
        public async UniTask<GameObject> WaitForModelAsync(CancellationToken cancellationToken)
        {
            if (loadSlotView == null)
            {
                Debug.LogError("[MonsterSelectionSession] LoadSlotViewが未設定です");
                return null;
            }

            selectionCancelled = false;
            await EnsureRevealedAsync(cancellationToken);
            loadSlotView.PrepareForSelectionWait();

            GameObject selected = null;
            using (loadSlotView.OnModelLoaded.Subscribe(model => selected = model))
            {
                await UniTask.WaitUntil(
                    () => selectionCancelled
                        || selected != null
                        || loadSlotView.LoadedModel != null,
                    cancellationToken: cancellationToken);
            }

            if (selectionCancelled)
            {
                if (presentationTransition != null)
                {
                    await presentationTransition.FadeOutAsync(cancellationToken);
                }

                loadSlotView.HideSelectionUi();
                presentationTransition?.ReleasePresentationInput();
                return null;
            }

            selectedSlotIndex = loadSlotView.SelectedSlotIndex;
            selectedStrengthTier = loadSlotView.SelectedStrengthTier;
            GameObject model = loadSlotView.DetachLoadedModel() ?? selected;
            loadSlotView.HideSelectionUi();
            presentationTransition?.ReleasePresentationInput();
            return model;
        }

        /// <inheritdoc />
        public void CancelWaitingSelection()
        {
            selectionCancelled = true;
        }

        /// <inheritdoc />
        public void Hide()
        {
            if (loadSlotView == null)
            {
                return;
            }

            CanvasVisibilityUtility.SetCanvasEnabled(loadSlotView.SelectionCanvas, false);
        }

        /// <inheritdoc />
        public void HideForLeave()
        {
            isRevealed = false;
            selectedSlotIndex = -1;
            selectedStrengthTier = EnemyStrengthTier.Normal;
            selectionCancelled = false;
            loadSlotView?.HideForLeave();
            Hide();
        }

        /// <inheritdoc />
        public async UniTask RestoreAfterParticipantFailureAsync(CancellationToken cancellationToken)
        {
            if (loadSlotView == null)
            {
                return;
            }

            loadSlotView.RestoreAfterParticipantFailure();
            loadSlotView.InvalidateSelectionContents();
            isRevealed = false;
            await EnsureRevealedAsync(cancellationToken);
        }

        private async UniTask EnsureRevealedAsync(CancellationToken cancellationToken)
        {
            if (loadSlotView == null)
            {
                return;
            }

            if (isRevealed)
            {
                loadSlotView.EnsureSelectionInputEnabled();
                return;
            }

            await RevealSelectionUiAsync(cancellationToken);
            isRevealed = true;
        }

        private async UniTask RevealSelectionUiAsync(CancellationToken cancellationToken)
        {
            if (loadSlotView == null)
            {
                return;
            }

            Transform sceneRoot = loadSlotView.transform.root;
            loadSlotView.DetachSelectionUiToSceneRoot(sceneRoot);
            loadSlotView.PrepareLayout();

            // 暗転のままCanvasを有効化し一覧の準備を完了させてから明転する
            Canvas selectionCanvas = loadSlotView.SelectionCanvas;
            if (presentationTransition != null)
            {
                await presentationTransition.SetCanvasEnabledAsync(
                    selectionCanvas,
                    true,
                    cancellationToken,
                    fadeOutBeforeChange: false,
                    fadeInAfterChange: false);
            }
            else
            {
                loadSlotView.PrepareForDisplay();
            }

            loadSlotView.EnsureSelectionInputEnabled();
            await loadSlotView.PrepareSelectionContentsAsync(cancellationToken);
            // 準備完了後に選択待ちへ入り再構築を避ける
            loadSlotView.PrepareForSelectionWait();
            // プレイヤー選択中に敵一覧サムネを先読みする
            if (loadSlotView.SavePool == ModelSavePool.TrainedPlayer)
            {
                loadSlotView.BeginPrefetchEnemyThumbnails(cancellationToken);
            }

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            Canvas.ForceUpdateCanvases();

            if (presentationTransition != null)
            {
                await presentationTransition.FadeInAsync(cancellationToken);
            }
            else if (sceneFade != null)
            {
                await sceneFade.FadeInAsync(cancellationToken);
            }

            loadSlotView.EnsureSelectionInputEnabled();
            presentationTransition?.ReleasePresentationInput();
        }
    }
}
