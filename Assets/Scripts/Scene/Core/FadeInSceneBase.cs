using Audio;
using Audio.Interface;
using Camera.Utility;
using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Lighthouse.Scene.SceneBase;
using Scene.Core.Interface;
using System.Threading;
using VContainer;

namespace Scene.Core
{
    /// <summary>
    /// 入場時に明転(フェード明け)を行うシーンの基底クラス。
    /// 各シーンは CanvasMainSceneBase の代わりにこれを継承する。
    /// OnEnter は基底が担当するため、シーン固有の入場処理は OnEnterCore に書く。
    /// 暗転(フェード)は ClayMonstersSceneManager 側が遷移前に行う。
    /// </summary>
    public abstract class FadeInSceneBase<TTransitionData> : CanvasMainSceneBase<TTransitionData>
        where TTransitionData : TransitionDataBase
    {
        [Inject]
        private IBgmService bgmService;

        [Inject]
        private ISceneFade sceneFade;

        /// <summary>
        /// 入場時に再生するBGM。nullならBattleFlow等に任せる
        /// </summary>
        protected virtual BgmTrackId? SceneEntryBgmTrackId
        {
            get
            {
                return SceneBgmMapping.TryGetTrack(MainSceneId, out BgmTrackId trackId)
                    ? trackId
                    : null;
            }
        }

        protected sealed override async UniTask OnEnter(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            using (CinemachineSceneBlendScope.EnterCutBlend())
            {
                await OnEnterCore(context, cancelToken);
                await base.OnEnter(context, cancelToken);
                await StabilizeSceneCameraForFadeInAsync(cancelToken);

                UniTask bgmTask = PlaySceneEntryBgmAsync(cancelToken);
                if (sceneFade != null && PerformEntryFadeIn)
                {
                    await UniTask.WhenAll(sceneFade.FadeInAsync(cancelToken), bgmTask);
                }
                else
                {
                    await bgmTask;
                }

                SceneCameraEntryCoordinator.ReleaseOrbitDamping();
            }

            await OnEnterAfterFadeInCore(context, cancelToken);
        }

        /// <summary>
        /// 入場時に基底の明転を行うか。OFFならOnEnterAfterFadeInCore側でUI明転を行う
        /// </summary>
        protected virtual bool PerformEntryFadeIn => true;

        /// <summary>
        /// シーン入場時の処理(従来のOnEnter相当)。必要なシーンだけオーバーライドする
        /// </summary>
        protected virtual UniTask OnEnterCore(ISceneTransitionContext context, CancellationToken cancelToken)
            => UniTask.CompletedTask;

        /// <summary>
        /// 明転完了後の入場処理。UI表示やフロー開始はここで行う
        /// </summary>
        protected virtual UniTask OnEnterAfterFadeInCore(ISceneTransitionContext context, CancellationToken cancelToken)
            => UniTask.CompletedTask;

        private async UniTask PlaySceneEntryBgmAsync(CancellationToken cancelToken)
        {
            BgmTrackId? trackId = SceneEntryBgmTrackId;
            if (!trackId.HasValue || bgmService == null)
            {
                return;
            }

            await bgmService.PlayAsync(trackId.Value, cancellationToken: cancelToken);
        }

        private static async UniTask StabilizeSceneCameraForFadeInAsync(CancellationToken cancelToken)
        {
            SceneCameraEntryCoordinator.PrepareForSceneFadeIn();
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancelToken);
            SceneCameraEntryCoordinator.PrepareForSceneFadeIn();
            SceneCameraEntryCoordinator.SnapBrainToActiveCamera();
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancelToken);
            SceneCameraEntryCoordinator.PrepareForSceneFadeIn();
            SceneCameraEntryCoordinator.SnapBrainToActiveCamera();
        }
    }
}