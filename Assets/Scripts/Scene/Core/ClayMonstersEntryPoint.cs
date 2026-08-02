using Audio.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using Lighthouse.Scene;
using LighthouseExtends.Font;
using LighthouseExtends.TextTable;
using Localization;
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
        readonly ILanguageInitializer languageInitializer;
        readonly ITextTableService textTableService;
        readonly IFontService fontService;

        [Inject]
        public ClayMonstersEntryPoint(
            ClayMonstersLifetimeScope clayMonstersLifetimeScope,
            ClayMonstersLifetimeScopeSettings clayMonstersLifetimeScopeSettings,
            ILauncher launcher,
            IMainSceneManager mainSceneManager,
            IUiSoundService uiSoundService,
            ILanguageInitializer languageInitializer,
            ITextTableService textTableService,
            IFontService fontService)
        {
            this.clayMonstersLifetimeScope = clayMonstersLifetimeScope;
            this.clayMonstersLifetimeScopeSettings = clayMonstersLifetimeScopeSettings;
            this.launcher = launcher;
            this.mainSceneManager = mainSceneManager;
            this.uiSoundService = uiSoundService;
            this.languageInitializer = languageInitializer;
            this.textTableService = textTableService;
            this.fontService = fontService;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            // TextTable/Fontを先に解決し言語ハンドラ登録を保証する
            _ = textTableService;
            _ = fontService;
            await languageInitializer.InitializeAsync(cancellation);
            ButtonRxExtensions.ConfigureUiSound(uiSoundService);
            mainSceneManager.SetEnqueueParentLifetimeScope(() => LifetimeScope.EnqueueParent(clayMonstersLifetimeScope));
            PvpLobbyRuntimeFactory.SetParentScope(clayMonstersLifetimeScope);
            PvpLobbyRuntimeFactory.SetFallbackPrefab(clayMonstersLifetimeScope.PvpLobbyHostPrefab);
            PvpLobbyBootstrap.EnsureLobby();
            await launcher.Launch();
        }
    }
}
