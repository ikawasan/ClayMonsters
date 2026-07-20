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
using UnityEngine.Rendering;
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
        [SerializeField] ClayEditPostProcessView clayEditPostProcessView;

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
            EnsureSerializedReferences();

            BattleClassroomLighting classroomLighting = GetComponent<BattleClassroomLighting>();
            if (classroomLighting != null)
            {
                builder.RegisterComponent(classroomLighting);
            }

            builder.RegisterComponent(clayEditScene);
            builder.RegisterComponent(clayEditView).AsImplementedInterfaces();
            builder.Register<ClayEditPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ClayEditSceneResetter>(Lifetime.Singleton);

            builder.RegisterComponent(cameraView).AsImplementedInterfaces();
            builder.RegisterComponent(clayEditPostProcessView).AsSelf().AsImplementedInterfaces();

            builder.Register<ClayEditCameraPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterEntryPoint<ClayInputProvider>(Lifetime.Singleton).As<IClayInputProvider>();
            builder.Register<ClaySceneContext>(Lifetime.Singleton).As<IClaySceneContext>();
            builder.Register<ClayEditSessionContext>(Lifetime.Singleton);
            builder.Register<ClayEditSavedModelImporter>(Lifetime.Singleton);

            builder.RegisterComponent(clayEditor);
            builder.RegisterComponent(clayVoxelEngine).AsSelf().As<IInitializable>();
            builder.Register<ClayEditModeViewModel>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.RegisterComponent(clayAutoRigger);
            builder.RegisterComponent(clayEditorRangeVisualizer);
            builder.RegisterComponent(clayCursor).AsSelf().As<ITickable>();
            builder.Register<ClayHistoryManager>(Lifetime.Singleton);

            builder.Register<BoneSkeletonGenerator>(Lifetime.Singleton).As<IBoneSkeletonGenerator>();
            builder.RegisterComponent(clayAutoRigController);
            builder.RegisterComponent(clayEditModelInitializer).AsSelf().As<IInitializable>();

            builder.RegisterComponent(colorPicker);
            builder.RegisterComponent(clayPainter);
            builder.RegisterComponent(clayPaintCursor).AsSelf().As<ITickable>();
            builder.Register<ClayPaintHistoryManager>(Lifetime.Singleton);

            builder.RegisterComponent(clayEditModeView);
            if (clayEditOperationGuideView != null)
            {
                builder.RegisterComponent(clayEditOperationGuideView);
            }

            builder.RegisterComponent(clayModelAnimationView);

            builder.Register<ClayModelSaveService>(Lifetime.Singleton).As<IClayModelSaveService>();
            builder.RegisterComponent(saveSlotView);
            builder.Register<ClayModelGltfImporter>(Lifetime.Singleton).As<IClayModelImporter>();
            builder.Register<ClayModelGltfExporter>(Lifetime.Singleton).As<IClayModelExporter>();

            builder.RegisterComponent(clayEditEntryView).AsImplementedInterfaces();
            builder.RegisterComponent(clayEditRemakeLoadSlotView);
            builder.RegisterComponent(clayEditEditorUiGate).AsImplementedInterfaces();

            builder.RegisterComponent(clayBoneVisualizer);
            builder.RegisterComponent(skeletonPartAnalyzer);
        }

        private void EnsureSerializedReferences()
        {
            PrepareClayEditPresentation();
            EnsureClayEditPostProcess();
        }

        private void PrepareClayEditPresentation()
        {
            BattleClassroomLighting lighting = GetComponent<BattleClassroomLighting>();
            if (lighting == null)
            {
                lighting = gameObject.AddComponent<BattleClassroomLighting>();
            }

            lighting.enabled = true;
            lighting.Apply();

            BattleNpcPostProcessView classroomPostProcess =
                GetComponentInChildren<BattleNpcPostProcessView>(true);
            if (classroomPostProcess == null)
            {
                return;
            }

            classroomPostProcess.enabled = false;
            Volume classroomVolume = classroomPostProcess.GetComponent<Volume>();
            if (classroomVolume != null)
            {
                classroomVolume.enabled = false;
            }
        }

        private void EnsureClayEditPostProcess()
        {
            if (clayEditPostProcessView != null)
            {
                return;
            }

            clayEditPostProcessView = GetComponentInChildren<ClayEditPostProcessView>(true);
            if (clayEditPostProcessView != null)
            {
                return;
            }

            BattleNpcPostProcessView classroomPostProcess =
                GetComponentInChildren<BattleNpcPostProcessView>(true);
            if (classroomPostProcess != null)
            {
                clayEditPostProcessView =
                    classroomPostProcess.gameObject.GetComponent<ClayEditPostProcessView>()
                    ?? classroomPostProcess.gameObject.AddComponent<ClayEditPostProcessView>();
                return;
            }

            Transform postProcessRoot = transform.Find("PostProcess");
            GameObject host = postProcessRoot != null
                ? postProcessRoot.gameObject
                : new GameObject("PostProcess");
            if (postProcessRoot == null)
            {
                host.transform.SetParent(transform, false);
            }

            clayEditPostProcessView = host.AddComponent<ClayEditPostProcessView>();
        }

        private void ValidateEntryFlowComponents()
        {
            if (clayEditEntryView == null
                || clayEditRemakeLoadSlotView == null
                || clayEditEditorUiGate == null)
            {
                Debug.LogError(
                    "[ClayEditLifetimeScope] Entry/Remake/UIGate?????????Hierarchy?????????",
                    this);
            }
        }
    }
}
