using Lighthouse.Scene;
using Lighthouse.Scene.SceneCamera;
using LighthouseExtends.UIComponent.CanvasSceneObject;
using LighthouseExtends.UIComponent.InputBlocker;
using SampleProduct.Core;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.Core
{
    public class ClayMonstersLifetimeScope : LifetimeScope
    {
        [SerializeField] ClayMonstersLifetimeScopeSettings clayMonstersLifetimeScopeSettings;
        [SerializeField] LHCanvasSceneObject canvasSceneObjectPrefab;
        [SerializeField] LHInputBlocker inputBlockerPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ClayMonstersEntryPoint>();
            builder.RegisterInstance(clayMonstersLifetimeScopeSettings);

            // lighthouseのシーン管理やカメラ管理に必要なクラスを登録
            builder.Register<SceneManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ClayMonstersSceneManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SceneTransitionController>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<DefaultSceneTransitionContextFactory>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SceneGroupProvider>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<MainSceneManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ModuleSceneManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<SceneCameraManager>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<DefaultSceneTransitionSequenceProvider>(Lifetime.Singleton).AsImplementedInterfaces();

            // lighthouseの拡張機能関連
            //builder.Register<ExclusiveInputService>(Lifetime.Singleton).AsImplementedInterfaces();
            //builder.Register<LanguageService>(Lifetime.Singleton).AsImplementedInterfaces();
            //builder.RegisterInstance(supportedLanguageSettings);
            //builder.Register<SupportedLanguageService>(Lifetime.Singleton).AsImplementedInterfaces();
            //builder.Register<TextTableService>(Lifetime.Singleton).AsImplementedInterfaces();
            //builder.RegisterInstance(languageFontSettings);
            //builder.Register<FontService>(Lifetime.Singleton).AsImplementedInterfaces();
            //builder.RegisterBuildCallback(container =>
            //{
            //    container.Resolve<ITextTableService>();
            //    container.Resolve<IFontService>();
            //});
            //builder.Register<ScreenStackModuleProxy>(Lifetime.Singleton).AsImplementedInterfaces();


            builder.Register<Launcher>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInNewPrefab(canvasSceneObjectPrefab, Lifetime.Singleton).DontDestroyOnLoad().AsImplementedInterfaces();
            builder.RegisterComponentInNewPrefab(inputBlockerPrefab, Lifetime.Singleton).DontDestroyOnLoad().AsImplementedInterfaces();

            //var inputActions = new InputActions();
            //builder.RegisterInstance(inputActions);
            //builder.RegisterInstance(inputActions.asset).As<InputActionAsset>();
            //builder.Register<InputLayerController>(Lifetime.Singleton).AsImplementedInterfaces();

            //builder.Register<AssetManager>(Lifetime.Singleton).AsImplementedInterfaces();
            //builder.Register<ProductAssetLoader>(Lifetime.Singleton).AsImplementedInterfaces();

            //// Module
            //builder.Register<OverlayModuleProxy>(Lifetime.Singleton).AsImplementedInterfaces();
            //builder.Register<GlobalHeaderModuleProxy>(Lifetime.Singleton).AsImplementedInterfaces();
            //builder.Register<BackgroundModuleProxy>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}
