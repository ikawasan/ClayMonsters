using Camera.Model;
using Camera.Presenter;
using Camera.View;
using ClayEditor;
using ClayEditor.Input;
using ClayEditor.Input.Interface;
using ClayEditor.Interface;
using ClayEditor.Paint;
using Scene.ClayEditScene.Presenter;
using Scene.ClayEditScene.View;
using UI.ClayEditor.View;
using UI.ClayEditor.ViewModel;
using UI.ColorPicker;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.ClayEditScene
{
    public class ClayEditLifetimeScope : LifetimeScope
    {
        [Header("Scene Views")]
        [SerializeField] ClayEditScene clayEditScene;
        [SerializeField] ClayEditView clayEditView;

        [Header("Camera")]
        [SerializeField] ClayEditCameraView cameraView;

        [Header("Clay Editor Components")]
        [SerializeField] ClayEditor.ClayEditor clayEditor;
        [SerializeField] ClayVoxelEngine clayVoxelEngine;
        [SerializeField] ClayAutoRigger clayAutoRigger;
        [SerializeField] ClayEditorRangeVisualizer clayEditorRangeVisualizer;
        [SerializeField] ClayCursor clayCursor;

        [Header("Paint Components")]
        [SerializeField] ColorPicker colorPicker;
        [SerializeField] ClayPainter clayPainter;
        [SerializeField] ClayPaintCursor clayPaintCursor;

        [Header("UI Views")]
        [SerializeField] ModelTypeSelectionView modelTypeSelectionView;
        [SerializeField] ClayEditModeView clayEditModeView;
        [SerializeField] ClayModelAnimationView clayModelAnimationView;

        protected override void Configure(IContainerBuilder builder)
        {
            // シーンの基本的なコンポーネントとプレゼンターの登録
            builder.RegisterComponent(clayEditScene);
            builder.RegisterComponent(clayEditView).AsImplementedInterfaces();
            builder.Register<ClayEditPresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            // Camera関連の登録
            builder.RegisterComponent(cameraView).AsImplementedInterfaces();
            builder.Register<ClayEditCameraPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();

            // ViewModelとシーンコンテキストの登録
            builder.Register<ClayEditModeViewModel>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<ClayModelAnimationViewModel>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<ModelTypeSelectionViewModel>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.RegisterEntryPoint<ClayInputProvider>(Lifetime.Singleton).As<IClayInputProvider>();
            builder.Register<ClayHistoryManager>(Lifetime.Singleton);
            builder.Register<ClaySceneContext>(Lifetime.Singleton).As<IClaySceneContext>();

            // Clay Editor関連
            builder.RegisterComponent(clayEditor);
            builder.RegisterComponent(clayVoxelEngine).AsSelf().As<IInitializable>();
            builder.RegisterComponent(clayAutoRigger);
            builder.RegisterComponent(clayEditorRangeVisualizer);
            builder.RegisterComponent(clayCursor).AsSelf().As<ITickable>();

            // Paint関連
            builder.RegisterComponent(colorPicker);
            builder.RegisterComponent(clayPainter);
            builder.RegisterComponent(clayPaintCursor).AsSelf().As<ITickable>();

            // UI Views
            builder.RegisterComponent(modelTypeSelectionView);
            builder.RegisterComponent(clayEditModeView);
            builder.RegisterComponent(clayModelAnimationView);
        }
    }
}