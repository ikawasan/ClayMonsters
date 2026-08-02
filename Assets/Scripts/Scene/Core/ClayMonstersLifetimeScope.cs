using Audio;
using Audio.Interface;
using Audio.Service;
using Lighthouse.Scene;
using Lighthouse.Scene.SceneCamera;
using LighthouseExtends.Font;
using LighthouseExtends.Language;
using LighthouseExtends.TextTable;
using LighthouseExtends.UIComponent.CanvasSceneObject;
using LighthouseExtends.UIComponent.InputBlocker;
using Localization;
using SampleProduct.Core;
using SaveData.Service;
using Scene.Core.View;
using Scene.PvpLobby;
using Scene.PvpLobby.Interface;
using UI.Option;
using UI.Option.Service;
using UI.Option.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.Core
{
    public class ClayMonstersLifetimeScope : LifetimeScope
    {
        [Header("System Presets")]
        [SerializeField] ClayMonstersLifetimeScopeSettings clayMonstersLifetimeScopeSettings;
        [SerializeField] LHCanvasSceneObject canvasSceneObjectPrefab;
        [SerializeField] LHInputBlocker inputBlockerPrefab;

        // シーン遷移時のフェード用オーバーレイ(色・時間はこのプレハブのSceneFadeViewで設定)
        [SerializeField] SceneFadeView sceneFadePrefab;

        [Header("Global Settings")]
        // アプリ全体で常駐させるオプション画面のプレハブ
        [SerializeField] OptionView optionViewPrefab;
        [SerializeField] SupportedLanguageSettings supportedLanguageSettings;
        [SerializeField] LanguageFontSettings languageFontSettings;

        [Header("PVP")]
        [SerializeField] GameObject pvpLobbyHostPrefab;

        [SerializeField] UiSoundSettings uiSoundSettings;

        /// <summary>
        /// PvpLobbyHostの予備プレハブ
        /// </summary>
        public GameObject PvpLobbyHostPrefab => pvpLobbyHostPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            // エントリーポイントと基本設定の登録
            builder.RegisterEntryPoint<ClayMonstersEntryPoint>();
            builder.RegisterInstance(clayMonstersLifetimeScopeSettings);

            RegisterLocalization(builder);

            builder.Register<IBgmService, BgmService>(Lifetime.Singleton);
            builder.RegisterInstance(uiSoundSettings);
            builder.Register<IUiSoundService, UiSoundService>(Lifetime.Singleton);
            builder.Register<ISeService, SeService>(Lifetime.Singleton);
            builder.Register<OptionService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<NpcBattleProgressService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<PointsService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SkillTreeService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponent(optionViewPrefab).AsImplementedInterfaces();
            builder.Register<OptionPresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            // Lighthouse コアシステムの登録
            builder.Register<SceneManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ClayMonstersSceneManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SceneTransitionController>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<DefaultSceneTransitionContextFactory>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SceneGroupProvider>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<MainSceneManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ModuleSceneManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SceneCameraManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<DefaultSceneTransitionSequenceProvider>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<Launcher>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<PvpLobbyRegistry>(Lifetime.Singleton)
                .As<IPvpLobbyRegistry>()
                .As<IPvpLobby>()
                .As<IPvpSessionController>();

            // LighthouseのUI・入力制御用のプレハブ登録
            builder.RegisterComponentInNewPrefab(canvasSceneObjectPrefab, Lifetime.Singleton).DontDestroyOnLoad().AsImplementedInterfaces();
            builder.RegisterComponentInNewPrefab(inputBlockerPrefab, Lifetime.Singleton).DontDestroyOnLoad().AsImplementedInterfaces();

            // シーン遷移フェード用オーバーレイの登録(常駐)
            builder.RegisterComponentInNewPrefab(sceneFadePrefab, Lifetime.Singleton).DontDestroyOnLoad().AsImplementedInterfaces();
        }

        private void RegisterLocalization(IContainerBuilder builder)
        {
            if (supportedLanguageSettings == null)
            {
                Debug.LogError(
                    "[ClayMonstersLifetimeScope] supportedLanguageSettingsが未配線です",
                    this);
            }
            else
            {
                builder.RegisterInstance(supportedLanguageSettings);
            }

            if (languageFontSettings == null)
            {
                Debug.LogError(
                    "[ClayMonstersLifetimeScope] languageFontSettingsが未配線です",
                    this);
            }
            else
            {
                builder.RegisterInstance(languageFontSettings);
            }

            builder.Register<SupportedLanguageService>(Lifetime.Singleton).As<ISupportedLanguageService>();
            builder.Register<LanguageService>(Lifetime.Singleton).As<ILanguageService>();
            builder.Register<StreamingAssetsTextTableLoader>(Lifetime.Singleton).As<ITextTableLoader>();
            builder.Register<TextTableService>(Lifetime.Singleton).As<ITextTableService>();
            builder.Register<FontService>(Lifetime.Singleton).As<IFontService>();
            builder.Register<LanguageOptionStore>(Lifetime.Singleton).As<ILanguageOptionStore>();
            builder.RegisterEntryPoint<LocalizedFontDriver>();
            builder.Register<LanguageInitializer>(Lifetime.Singleton).As<ILanguageInitializer>();
        }
    }
}