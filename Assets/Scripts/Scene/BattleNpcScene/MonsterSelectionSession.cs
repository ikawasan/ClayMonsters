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
    /// 表示切替とロード待ちを一箇所に集約する
    /// </summary>
    public sealed class MonsterSelectionSession : IMonsterSelectionSession
    {
        private readonly LoadSlotView loadSlotView;
        private readonly IBattleCanvasTransition presentationTransition;
        private readonly ISceneFade sceneFade;
        private bool isRevealed;
        private int selectedSlotIndex = -1;

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
        public void PrepareEntry()
        {
            isRevealed = false;
            selectedSlotIndex = -1;
            // シーン再利用時に前回ロード済みモデルで選択待ちをスキップしない
            loadSlotView?.HideForLeave();
            loadSlotView?.PrepareLayout();
            Hide();
        }

        /// <inheritdoc />
        public async UniTask<GameObject> WaitForModelAsync(CancellationToken cancellationToken)
        {
            if (loadSlotView == null)
            {
                Debug.LogError("[MonsterSelectionSession] LoadSlotViewが未設定です");
                return null;
            }

            // 再入場・再戦では必ず選択UIを出し直す
            // 前回LoadedModelが残っていてもスキップしない
            await EnsureRevealedAsync(cancellationToken);
            loadSlotView.PrepareForSelectionWait();

            GameObject selected = null;
            using (loadSlotView.OnModelLoaded.Subscribe(model => selected = model))
            {
                await UniTask.WaitUntil(
                    () => selected != null || loadSlotView.LoadedModel != null,
                    cancellationToken: cancellationToken);
            }

            // Detach前にスロット番号を保持する(BattleFlowが直後に参照する)
            selectedSlotIndex = loadSlotView.SelectedSlotIndex;
            GameObject model = loadSlotView.DetachLoadedModel() ?? selected;
            loadSlotView.HideSelectionUi();
            presentationTransition?.ReleasePresentationInput();
            return model;
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
            await RevealSelectionUiAsync(cancellationToken);
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
                await FinalizeSelectionLayoutAsync(cancellationToken);
                return;
            }

            isRevealed = true;
            await RevealSelectionUiAsync(cancellationToken);
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
            loadSlotView.EnsureSelectionReady();

            Canvas selectionCanvas = loadSlotView.SelectionCanvas;
            if (presentationTransition != null)
            {
                await presentationTransition.SetCanvasEnabledAsync(
                    selectionCanvas,
                    true,
                    cancellationToken,
                    fadeOutBeforeChange: false,
                    fadeInAfterChange: true);
            }
            else
            {
                loadSlotView.PrepareForDisplay();
                if (sceneFade != null)
                {
                    await sceneFade.FadeInAsync(cancellationToken);
                }
            }

            loadSlotView.EnsureSelectionInputEnabled();
            await FinalizeSelectionLayoutAsync(cancellationToken);
            presentationTransition?.ReleasePresentationInput();
        }

        private async UniTask FinalizeSelectionLayoutAsync(CancellationToken cancellationToken)
        {
            if (loadSlotView == null)
            {
                return;
            }

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            loadSlotView.DetachSelectionUiToSceneRoot(loadSlotView.transform.root);
            loadSlotView.PrepareLayout();
            loadSlotView.EnsureSelectionReady();
            loadSlotView.Refresh();

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            Canvas.ForceUpdateCanvases();
        }
    }
}
