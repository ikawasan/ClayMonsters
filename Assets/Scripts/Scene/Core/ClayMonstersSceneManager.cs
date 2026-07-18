using Audio;
using Audio.Interface;
using Cysharp.Threading.Tasks;
using Camera.Utility;
using Lighthouse.Scene;
using Scene.Core;
using Scene.Core.Interface;
using System;
using UnityEngine;
using VContainer;

namespace SampleProduct.Core
{
    public sealed class ClayMonstersSceneManager : IClayMonsterSceneManager
    {
        readonly IBgmService bgmService;
        readonly ISceneManager sceneManager;
        readonly ILauncher launcher;
        readonly ISceneFade sceneFade;

        public bool IsTransition => sceneManager.IsTransition;

        [Inject]
        public ClayMonstersSceneManager(
            IBgmService bgmService,
            ISceneManager sceneManager,
            ILauncher launcher,
            ISceneFade sceneFade)
        {
            this.bgmService = bgmService;
            this.sceneManager = sceneManager;
            this.launcher = launcher;
            this.sceneFade = sceneFade;
        }

        async UniTask IClayMonsterSceneManager.TransitionScene(
            TransitionDataBase nextTransitionData,
            TransitionType transitionType,
            MainSceneId backMainSceneId)
        {
            try
            {
                await PrepareLeaveTransitionAsync(nextTransitionData.MainSceneId, isBackNavigation: false);

                using (CinemachineSceneBlendScope.EnterCutBlend())
                {
                    SceneCameraEntryCoordinator.DeactivateAllCameras();
                    await sceneManager.TransitionScene(nextTransitionData, transitionType, backMainSceneId);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProductSceneManager] Unhandled exception during transition. Rebooting.\n{e}");

                if (sceneFade != null)
                {
                    await sceneFade.FadeInAsync();
                }
            }
        }

        async UniTask IClayMonsterSceneManager.BackScene(TransitionType transitionType)
        {
            try
            {
                await PrepareLeaveTransitionAsync(null, isBackNavigation: true);

                using (CinemachineSceneBlendScope.EnterCutBlend())
                {
                    SceneCameraEntryCoordinator.DeactivateAllCameras();
                    await sceneManager.BackScene(transitionType);
                }

                // 戻り先が入場明転を行わず暗転のまま残った場合のみ明転する
                if (sceneFade != null && sceneFade.IsOpaque)
                {
                    await sceneFade.FadeInAsync();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProductSceneManager] Unhandled exception during back transition. Rebooting.\n{e}");
                launcher.Reboot();
            }
        }

        UniTask IClayMonsterSceneManager.PreReboot() => sceneManager.PreReboot();

        private async UniTask PrepareLeaveTransitionAsync(MainSceneId nextMainSceneId, bool isBackNavigation)
        {
            UniTask screenFadeTask = sceneFade != null
                ? sceneFade.FadeOutAsync()
                : UniTask.CompletedTask;

            UniTask bgmFadeTask = CreateBgmFadeOutTask(nextMainSceneId, isBackNavigation);
            await UniTask.WhenAll(screenFadeTask, bgmFadeTask);

            // シーン切替直前に暗転を確定し入場先での中身露出を防ぐ
            sceneFade?.EnsureOpaque();
        }

        private UniTask CreateBgmFadeOutTask(MainSceneId nextMainSceneId, bool isBackNavigation)
        {
            if (bgmService == null || !bgmService.IsPlaying)
            {
                return UniTask.CompletedTask;
            }

            if (isBackNavigation)
            {
                return bgmService.FadeOutAsync();
            }

            BgmTrackId? currentTrackId = bgmService.TryGetCurrentTrack(out BgmTrackId currentTrack)
                ? currentTrack
                : null;

            if (!SceneBgmMapping.ShouldFadeOutForTransition(nextMainSceneId, currentTrackId))
            {
                return UniTask.CompletedTask;
            }

            return bgmService.FadeOutAsync();
        }
    }
}
