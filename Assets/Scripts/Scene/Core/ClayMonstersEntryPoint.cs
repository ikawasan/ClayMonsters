using Audio.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using Lighthouse.Scene;
using Scene.Core;
using Scene.Core.Interface;
using Scene.PvpLobby;
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
        readonly IUiSoundService uiSoundService;

        [Inject]
        public ClayMonstersEntryPoint(
            ClayMonstersLifetimeScope clayMonstersLifetimeScope,
            ClayMonstersLifetimeScopeSettings clayMonstersLifetimeScopeSettings,
            ILauncher launcher,
            IMainSceneManager mainSceneManager,
            IUiSoundService uiSoundService)
        {
            this.clayMonstersLifetimeScope = clayMonstersLifetimeScope;
            this.clayMonstersLifetimeScopeSettings = clayMonstersLifetimeScopeSettings;
            this.launcher = launcher;
            this.mainSceneManager = mainSceneManager;
            this.uiSoundService = uiSoundService;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            ButtonRxExtensions.ConfigureUiSound(uiSoundService);
            mainSceneManager.SetEnqueueParentLifetimeScope(() => LifetimeScope.EnqueueParent(clayMonstersLifetimeScope));
            PvpLobbyRuntimeFactory.SetParentScope(clayMonstersLifetimeScope);
            PvpLobbyRuntimeFactory.SetFallbackPrefab(clayMonstersLifetimeScope.PvpLobbyHostPrefab);
            PvpLobbyBootstrap.EnsureLobby();
            await launcher.Launch();
        }
    }
}
