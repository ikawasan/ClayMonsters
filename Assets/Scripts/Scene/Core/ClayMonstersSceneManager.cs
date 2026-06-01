using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Scene.Core.Interface;
using System;
using UnityEngine;
using VContainer;

namespace SampleProduct.Core
{
    public sealed class ClayMonstersSceneManager : IClayMonsterSceneManager
    {
        readonly ISceneManager sceneManager;
        readonly ILauncher launcher;

        public bool IsTransition => sceneManager.IsTransition;

        [Inject]
        public ClayMonstersSceneManager(ISceneManager sceneManager, ILauncher launcher)
        {
            this.sceneManager = sceneManager;
            this.launcher = launcher;
        }

        async UniTask IClayMonsterSceneManager.TransitionScene(
            TransitionDataBase nextTransitionData,
            TransitionType transitionType,
            MainSceneId backMainSceneId)
        {
            try
            {
                await sceneManager.TransitionScene(nextTransitionData, transitionType, backMainSceneId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProductSceneManager] Unhandled exception during transition. Rebooting.\n{e}");

                // NOTE: The sample does not reboot.
                // In a real project, it is recommended to reboot after displaying a dialog box and reporting errors.
                // launcher.Reboot();
            }
        }

        async UniTask IClayMonsterSceneManager.BackScene(TransitionType transitionType)
        {
            try
            {
                await sceneManager.BackScene(transitionType);
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
    }
}
