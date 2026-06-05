using Lighthouse.Scene;
using Lighthouse.Scene.SceneCamera;
using LighthouseExtends.UIComponent.CanvasSceneObject;
using LighthouseExtends.UIComponent.InputBlocker;
using SampleProduct.Core;
using UI.Option;
using UI.Option.Interface;
using UI.Option.Service;
using UI.Option.View;
using UnityEngine;
using UnityEngine.Rendering;
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

        [Header("Global Settings")]
        [SerializeField] VolumeProfile globalPostProcessProfile;

        // アプリ全体で常駐させるオプション画面のプレハブ
        [SerializeField] OptionView optionViewPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            // エントリーポイントと基本設定の登録
            builder.RegisterEntryPoint<ClayMonstersEntryPoint>();
            builder.RegisterInstance(clayMonstersLifetimeScopeSettings);
            builder.RegisterInstance(globalPostProcessProfile);
            builder.Register<OptionService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInNewPrefab(optionViewPrefab, Lifetime.Singleton)
                   .DontDestroyOnLoad()
                   .AsImplementedInterfaces();
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

            // LighthouseUI・入力制御用のプレハブ登録
            builder.RegisterComponentInNewPrefab(canvasSceneObjectPrefab, Lifetime.Singleton).DontDestroyOnLoad().AsImplementedInterfaces();
            builder.RegisterComponentInNewPrefab(inputBlockerPrefab, Lifetime.Singleton).DontDestroyOnLoad().AsImplementedInterfaces();
        }
    }
}