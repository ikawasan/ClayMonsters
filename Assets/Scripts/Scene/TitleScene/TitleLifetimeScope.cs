using Camera.Model;
using Camera.View;
using SaveData;
using SaveData.Interface;
using SaveData.Service;
using Scene.TitleScene.Presenter;
using Scene.TitleScene.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.TitleScene
{
    public class TitleLifetimeScope : LifetimeScope
    {
        [SerializeField] TitleScene titleScene;
        [SerializeField] TitleView titleView;
        [SerializeField] TitleMessageWindowView messageWindowView;

        [Header("Camera")]
        [SerializeField] ClayEditCameraView cameraView;

        [Header("Model Display")]
        [SerializeField] TitleModelDisplay titleModelDisplay;
        [SerializeField] TitleSceneCamera titleSceneCamera;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(titleScene);
            builder.RegisterComponent(titleView).AsImplementedInterfaces();
            if (messageWindowView == null)
            {
                messageWindowView = titleView.GetComponent<TitleMessageWindowView>();
            }

            if (messageWindowView == null)
            {
                Debug.LogError("[TitleLifetimeScope] TitleMessageWindowViewが未設定です", this);
            }
            else
            {
                builder.RegisterComponent(messageWindowView).AsImplementedInterfaces();
            }
            builder.Register<TitlePresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponent(cameraView).AsImplementedInterfaces();
            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponent(titleModelDisplay);
            builder.RegisterComponent(titleSceneCamera);

            builder.Register<ClayModelSaveService>(Lifetime.Singleton).As<IClayModelSaveService>();
            builder.Register<ClayModelGltfImporter>(Lifetime.Singleton).As<IClayModelImporter>();
            builder.Register<ClayModelGltfExporter>(Lifetime.Singleton).As<IClayModelExporter>();
        }
    }
}