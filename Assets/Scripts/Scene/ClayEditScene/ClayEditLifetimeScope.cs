using Camera.Model;
using Camera.Presenter;
using Camera.View;
using ClayEditor;
using ClayEditor.Input;
using ClayEditor.Input.Interface;
using ClayEditor.Interface;
using ClayEditor.Paint;
using ClayEditor.Paint.Interface;
using ClayEditor.Rigging;
using ClayEditor.Rigging.Interface;
using SaveData;
using SaveData.Interface;
using SaveData.Service;
using Scene.BattleNpcScene;
using Scene.BattleNpcScene.View;
using Scene.ClayEditScene.Presenter;
using Scene.ClayEditScene.View;
using Scene.DesktopPet;
using UI.ClayEditor.Interface;
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
        [SerializeField] ClayEditBackgroundColorView clayEditBackgroundColorView;
        [SerializeField] ClayModelAnimationView clayModelAnimationView;
        [SerializeField] SaveSlotView saveSlotView;

        [Header("Entry Flow")]
        [SerializeField] ClayEditEntryView clayEditEntryView;
        [SerializeField] ClayEditRemakeLoadSlotView clayEditRemakeLoadSlotView;
        [SerializeField] ClayEditEditorUiGate clayEditEditorUiGate;

        [Header("Editor Only")]
        [Tooltip("FBX頂点カラースポーンEditor専用ROMには含めない")]
        [SerializeField] ClayEditFbxVertexColorSpawner fbxVertexColorSpawner;

        [Header("Visualizer")]
        [SerializeField] ClayBoneVisualizer clayBoneVisualizer;
        [SerializeField] SkeletonPartAnalyzer skeletonPartAnalyzer;

        protected override void Configure(IContainerBuilder builder)
        {
            ValidateRequiredReferences();
            PrepareClayEditPresentation();

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
            builder.Register<ClayPaintSplashEffect>(Lifetime.Singleton).As<IClayPaintSplashEffect>();

            builder.RegisterComponent(clayEditModeView);
            if (clayEditOperationGuideView != null)
            {
                builder.RegisterComponent(clayEditOperationGuideView);
            }

            if (clayEditBackgroundColorView != null)
            {
                builder.RegisterComponent(clayEditBackgroundColorView);
            }

            builder.RegisterComponent(clayModelAnimationView);

            builder.Register<ClayModelSaveService>(Lifetime.Singleton).As<IClayModelSaveService>();
            builder.RegisterComponent(saveSlotView);
            builder.Register<ClayModelGltfImporter>(Lifetime.Singleton).As<IClayModelImporter>();
            builder.Register<ClayModelGltfExporter>(Lifetime.Singleton).As<IClayModelExporter>();
            builder.Register<DesktopPetSpritePreBakeService>(Lifetime.Singleton)
                .As<IPlayerModelSaveSideEffect>();

            builder.RegisterComponent(clayEditEntryView).AsImplementedInterfaces();
            builder.RegisterComponent(clayEditRemakeLoadSlotView);
            builder.RegisterComponent(clayEditEditorUiGate).AsImplementedInterfaces();

            builder.RegisterComponent(clayBoneVisualizer);
            builder.RegisterComponent(skeletonPartAnalyzer);

#if UNITY_EDITOR
            builder.Register<ClayEditFbxVertexColorImporter>(Lifetime.Singleton);
            if (fbxVertexColorSpawner != null)
            {
                builder.RegisterComponent(fbxVertexColorSpawner);
            }
#endif
        }

        private void PrepareClayEditPresentation()
        {
            BattleClassroomLighting lighting = GetComponent<BattleClassroomLighting>();
            if (lighting == null)
            {
                Debug.LogError(
                    "[ClayEditLifetimeScope] BattleClassroomLightingが未配線です",
                    this);
            }
            else
            {
                lighting.enabled = true;
                lighting.Apply();
            }

            if (clayEditPostProcessView == null)
            {
                return;
            }

            BattleNpcPostProcessView classroomPostProcess =
                clayEditPostProcessView.GetComponent<BattleNpcPostProcessView>();
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

        private void ValidateRequiredReferences()
        {
            if (clayEditScene == null
                || clayEditView == null
                || cameraView == null
                || clayEditPostProcessView == null
                || clayEditor == null
                || clayVoxelEngine == null
                || clayAutoRigger == null
                || clayAutoRigController == null
                || clayEditModelInitializer == null
                || clayEditorRangeVisualizer == null
                || clayCursor == null
                || colorPicker == null
                || clayPainter == null
                || clayPaintCursor == null
                || clayEditModeView == null
                || clayEditBackgroundColorView == null
                || clayModelAnimationView == null
                || saveSlotView == null
                || clayEditEntryView == null
                || clayEditRemakeLoadSlotView == null
                || clayEditEditorUiGate == null
                || clayBoneVisualizer == null
                || skeletonPartAnalyzer == null)
            {
                Debug.LogError(
                    "[ClayEditLifetimeScope] 必須SerializeFieldが未配線ですHierarchyで接続してください",
                    this);
            }
        }
    }
}
