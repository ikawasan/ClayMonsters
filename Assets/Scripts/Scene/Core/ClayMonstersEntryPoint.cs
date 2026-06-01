using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Scene.Core.Interface;
using System.Threading;
using VContainer;
using VContainer.Unity;

namespace Scene.Core
{
    public class ClayMonstersEntryPoint : IAsyncStartable
    {
        readonly ClayMonstersLifetimeScope clayMonstersLifetimeScope;
        readonly ClayMonstersLifetimeScopeSettings clayMonstersLifetimeScopeSettings;
        readonly ILauncher launcher;
        readonly IMainSceneManager mainSceneManager;

        [Inject]
        public ClayMonstersEntryPoint(
            ClayMonstersLifetimeScope clayMonstersLifetimeScope,
            ClayMonstersLifetimeScopeSettings clayMonstersLifetimeScopeSettings,
            ILauncher launcher,
            IMainSceneManager mainSceneManager)
        {
            this.clayMonstersLifetimeScope = clayMonstersLifetimeScope;
            this.clayMonstersLifetimeScopeSettings = clayMonstersLifetimeScopeSettings;
            this.launcher = launcher;
            this.mainSceneManager = mainSceneManager;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            mainSceneManager.SetEnqueueParentLifetimeScope(() => LifetimeScope.EnqueueParent(clayMonstersLifetimeScope));
            await launcher.Launch();
        }
    }
}
