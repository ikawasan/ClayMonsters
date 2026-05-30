using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using UnityEngine;
using VContainer;
using static Scene.TitleScene.TitleScene;

namespace Scene.Core
{
    public class Launcher : ILauncher
    {
        static readonly string LauncherSceneName = "Bootstrap";

        readonly ISceneManager sceneManager;

        [Inject]
        public Launcher(ISceneManager sceneManager)
        {
            this.sceneManager = sceneManager;
        }

        void ILauncher.Reboot()
        {
            RebootProcess().Forget();

            async UniTask RebootProcess()
            {
                await sceneManager.PreReboot();

                await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(LauncherSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
                await LaunchProcess();

                TransitionNextScene();
            }
        }

        async UniTask ILauncher.Launch()
        {
            await FirstLaunchProcess();
            await LaunchProcess();
            TransitionNextScene();
        }

        UniTask FirstLaunchProcess()
        {
            return UniTask.CompletedTask;
        }

        UniTask LaunchProcess()
        {
            return UniTask.CompletedTask;
        }

        void TransitionNextScene()
        {
            UniTask.Void(async () =>
            {
                // ‰ŠúƒV[ƒ“‚ğTitle‚Ö•ÏX
                await sceneManager.TransitionScene(new TitleTransitionData());

                if (!string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetSceneByName(LauncherSceneName).name))
                {
                    await UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(LauncherSceneName);
                }
            });
        }
    }
}
