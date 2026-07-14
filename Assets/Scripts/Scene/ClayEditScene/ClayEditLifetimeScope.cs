using Camera.Model;

using Camera.Presenter;

using Camera.View;

using ClayEditor;

using ClayEditor.Input;

using ClayEditor.Input.Interface;

using ClayEditor.Interface;

using ClayEditor.Paint;

using ClayEditor.Rigging;

using ClayEditor.Rigging.Interface;

using SaveData;

using SaveData.Interface;

using SaveData.Service;

using Scene.BattleNpcScene;
using Scene.BattleNpcScene.View;
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
        [SerializeField] BattleNpcPostProcessView battleNpcPostProcessView;
        [SerializeField] BattleClassroomLighting classroomLighting;



        [Header("Clay Editor Components")]

        [SerializeField] ClayEditor.ClayEditor clayEditor;

        [SerializeField] ClayVoxelEngine clayVoxelEngine;

        [SerializeField] ClayAutoRigger clayAutoRigger;

        [SerializeField] ClayAutoRigController clayAutoRigController;

        [SerializeField] ClayEditModelInitializer clayEditModelInitializer;

        [SerializeField] ClayEditorRangeVisualizer clayEditorRangeVisualizer;

        [SerializeField] ClayCursor clayCursor;



        [Header("Paint Components")]

        [SerializeField] ColorPicker colorPicker;

        [SerializeField] ClayPainter clayPainter;

        [SerializeField] ClayPaintCursor clayPaintCursor;



        [Header("UI Views")]

        [SerializeField] ClayEditModeView clayEditModeView;

        [SerializeField] ClayEditOperationGuideView clayEditOperationGuideView;

        [SerializeField] ClayModelAnimationView clayModelAnimationView;

        [SerializeField] SaveSlotView saveSlotView;

        [Header("Entry Flow")]
        [SerializeField] ClayEditEntryView clayEditEntryView;
        [SerializeField] ClayEditRemakeLoadSlotView clayEditRemakeLoadSlotView;
        [SerializeField] ClayEditEditorUiGate clayEditEditorUiGate;



        [Header("Visualizer")]

        [SerializeField] ClayBoneVisualizer clayBoneVisualizer;

        [SerializeField] SkeletonPartAnalyzer skeletonPartAnalyzer;



        protected override void Configure(IContainerBuilder builder)

        {
            ValidateEntryFlowComponents();

            // シーンの基本的なコンポーネントとプレゼンターの登録

            builder.RegisterComponent(clayEditScene);

            builder.RegisterComponent(clayEditView).AsImplementedInterfaces();

            builder.Register<ClayEditPresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<ClayEditSceneResetter>(Lifetime.Singleton);



            // Camera 関連の登録

            builder.RegisterComponent(cameraView).AsImplementedInterfaces();
            builder.RegisterComponent(battleNpcPostProcessView).AsSelf().AsImplementedInterfaces();
            builder.RegisterComponent(classroomLighting);

            builder.Register<ClayEditCameraPresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();



            // ViewModel?????????????(ClayVoxelEngine???????????????)

            builder.RegisterEntryPoint<ClayInputProvider>(Lifetime.Singleton).As<IClayInputProvider>();

            builder.Register<ClaySceneContext>(Lifetime.Singleton).As<IClaySceneContext>();
            builder.Register<ClayEditSessionContext>(Lifetime.Singleton);
            builder.Register<ClayEditSavedModelImporter>(Lifetime.Singleton);



            // Clay Editor ??

            builder.RegisterComponent(clayEditor);

            builder.RegisterComponent(clayVoxelEngine).AsSelf().As<IInitializable>();

            builder.Register<ClayEditModeViewModel>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();

            builder.RegisterComponent(clayAutoRigger);

            builder.RegisterComponent(clayEditorRangeVisualizer);

            builder.RegisterComponent(clayCursor).AsSelf().As<ITickable>();

            builder.Register<ClayHistoryManager>(Lifetime.Singleton);



            // 自動リギング関連（型選択の代わりにメッシュ形状からボーンを生成する）

            builder.Register<BoneSkeletonGenerator>(Lifetime.Singleton).As<IBoneSkeletonGenerator>();

            builder.RegisterComponent(clayAutoRigController);

            builder.RegisterComponent(clayEditModelInitializer).AsSelf().As<IInitializable>();



            // Paint 関連

            builder.RegisterComponent(colorPicker);

            builder.RegisterComponent(clayPainter);

            builder.RegisterComponent(clayPaintCursor).AsSelf().As<ITickable>();

            builder.Register<ClayPaintHistoryManager>(Lifetime.Singleton);



            // UI Views

            builder.RegisterComponent(clayEditModeView);

            if (clayEditOperationGuideView != null)
            {
                builder.RegisterComponent(clayEditOperationGuideView);
            }

            builder.RegisterComponent(clayModelAnimationView);



            // SaveData 関連

            builder.Register<ClayModelSaveService>(Lifetime.Singleton).As<IClayModelSaveService>();

            builder.RegisterComponent(saveSlotView);
            builder.Register<ClayModelGltfImporter>(Lifetime.Singleton).As<IClayModelImporter>();

            builder.Register<ClayModelGltfExporter>(Lifetime.Singleton).As<IClayModelExporter>();

            builder.RegisterComponent(clayEditEntryView).AsImplementedInterfaces();
            builder.RegisterComponent(clayEditRemakeLoadSlotView);
            builder.RegisterComponent(clayEditEditorUiGate).AsImplementedInterfaces();



            // Visualizer

            builder.RegisterComponent(clayBoneVisualizer); 

            builder.RegisterComponent(skeletonPartAnalyzer);

        }

        private void ValidateEntryFlowComponents()
        {
            if (clayEditEntryView == null
                || clayEditRemakeLoadSlotView == null
                || clayEditEditorUiGate == null)
            {
                Debug.LogError(
                    "[ClayEditLifetimeScope] ?????UI???????Tools/ClayMonsters/Migrate ClayEdit Scene UI?????????",
                    this);
            }
        }

    }

}


