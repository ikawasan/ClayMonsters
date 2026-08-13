using Camera.Model;
using Camera.View;
using SaveData;
using SaveData.Interface;
using SaveData.Service;
using Scene.DesktopPet;
using Scene.DesktopPet.Interface;
using Scene.TitleScene.Interface;
using Scene.TitleScene.Presenter;
using Scene.TitleScene.View;
using UI.ModelGallery.Presenter;
using UI.ModelGallery.Service;
using UI.ModelGallery.View;
using UI.SkillTree.Presenter;
using UI.SkillTree.View;
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
        [SerializeField] TitleConfirmWindowView confirmWindowView;
        [SerializeField] TitleDesktopPetSlotSelectView desktopPetSlotSelectView;
        [SerializeField] SkillTreeView skillTreeView;
        [SerializeField] ModelGalleryView modelGalleryView;

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

            if (confirmWindowView == null)
            {
                confirmWindowView = titleView.GetComponentInChildren<TitleConfirmWindowView>(true);
            }

            if (confirmWindowView == null)
            {
                Debug.LogError(
                    "[TitleLifetimeScope] TitleConfirmWindowViewが未配線ですTitleMessageWindowを複製してはいいいえ用に接続してください",
                    this);
                builder.Register<NullTitleConfirmWindowView>(Lifetime.Singleton)
                    .As<ITitleConfirmWindowView>();
            }
            else
            {
                builder.RegisterComponent(confirmWindowView).AsImplementedInterfaces();
            }

            if (desktopPetSlotSelectView == null)
            {
                Debug.LogError(
                    "[TitleLifetimeScope] TitleDesktopPetSlotSelectViewが未配線です未育成スロットUIを配置し接続してください",
                    this);
                builder.Register<NullTitleDesktopPetSlotSelectView>(Lifetime.Singleton)
                    .As<ITitleDesktopPetSlotSelectView>();
            }
            else
            {
                builder.RegisterComponent(desktopPetSlotSelectView).AsImplementedInterfaces();
            }

            if (skillTreeView == null)
            {
                Debug.LogError("[TitleLifetimeScope] skillTreeViewが未配線です", this);
            }
            else
            {
                builder.RegisterComponent(skillTreeView).AsImplementedInterfaces();
            }

            if (modelGalleryView == null)
            {
                Debug.LogError(
                    "[TitleLifetimeScope] modelGalleryViewが未配線です。展示室UIを配置しInspectorで接続してください",
                    this);
            }
            else
            {
                builder.RegisterComponent(modelGalleryView).AsImplementedInterfaces();
            }

            builder.Register<SkillTreePresenter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<LocalModelGalleryService>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ModelGalleryPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<DesktopPetLauncher>(Lifetime.Singleton).As<IDesktopPetLauncher>();
            builder.Register<TitlePresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponent(cameraView).AsImplementedInterfaces();
            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponent(titleModelDisplay);
            if (titleModelDisplay != null && titleModelDisplay.Configurator != null)
            {
                builder.RegisterInstance(titleModelDisplay.Configurator);
            }
            else
            {
                Debug.LogError(
                    "[TitleLifetimeScope] TitleModelDisplay.Configuratorが未配線です",
                    this);
            }

            builder.RegisterComponent(titleSceneCamera);

            builder.Register<ClayModelSaveService>(Lifetime.Singleton).As<IClayModelSaveService>();
            builder.Register<ClayModelGltfImporter>(Lifetime.Singleton).As<IClayModelImporter>();
            builder.Register<ClayModelGltfExporter>(Lifetime.Singleton).As<IClayModelExporter>();
        }
    }
}
