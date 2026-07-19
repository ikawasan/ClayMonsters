using Battle.Interface;
using Camera.Model;
using Camera.Presenter;
using Camera.View;
using Extensions;
using SaveData;
using SaveData.Interface;
using SaveData.Service;
using Scene.BattleNpcScene;
using Scene.BattleNpcScene.View;
using Scene.Core;
using Scene.TrainingScene.Presenter;
using Scene.TrainingScene.View;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;

namespace Scene.TrainingScene
{
    /// <summary>
    /// 育成シーンのDI登録
    /// FlowRunner・HUD・カメラ・モデル表示・セーブを束ねる
    /// </summary>
    public sealed class TrainingLifetimeScope : LifetimeScope
    {
        private const string ScopeTag = "TrainingLifetimeScope";

        [Header("Scene")]
        [SerializeField] private TrainingScene trainingScene;

        [Header("Flow")]
        [SerializeField] private TrainingFlowRunner flowRunner;
        [SerializeField] private TrainingBattleRunner battleRunner;
        [SerializeField] private TrainingHudView hudView;
        [FormerlySerializedAs("selectionUiGroup")]
        [FormerlySerializedAs("selectionCanvas")]
        [SerializeField] private Canvas selectionCanvas;

        [Header("Camera")]
        [SerializeField] private ClayEditCameraView cameraView;

        [Header("Model")]
        [SerializeField] private TrainingDisplay trainingDisplay;
        [SerializeField] private TrainingBackgroundView backgroundView;
        [SerializeField] private TrainingLocationCameraView locationCameraView;

        [Header("Post Process")]
        [SerializeField] private BattleNpcPostProcessView postProcessView;
        [SerializeField] private BattleClassroomLighting classroomLighting;

        [Header("UI")]
        [SerializeField] private LoadSlotView loadSlotView;
        [SerializeField] private TrainingTrainedSaveView trainedSaveView;
        [SerializeField] private TrainingModeSelectView modeSelectView;
        [SerializeField] private TrainingAutoResultView autoResultView;

        protected override void Awake()
        {
            TrainingScene primaryScene = ConsolidateTrainingSceneRoots();
            if (primaryScene == null)
            {
                EnsureParentScope();
                base.Awake();
                return;
            }

            TrainingLifetimeScope primaryScope = primaryScene.GetComponent<TrainingLifetimeScope>();
            if (primaryScope != this)
            {
                DestroySelfAsDuplicate();
                return;
            }

            WireSerializedReferencesFromPrimary(primaryScene);
            EnsureParentScope();
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            EnsureSerializedReferences();

            VContainerComponentRegistration.RegisterComponent(builder, trainingScene, ScopeTag);
            builder.Register<TrainingPresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, hudView, ScopeTag);
            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, trainedSaveView, ScopeTag);
            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, modeSelectView, ScopeTag);
            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, autoResultView, ScopeTag);
            VContainerComponentRegistration.RegisterComponent(builder, flowRunner, ScopeTag);
            VContainerComponentRegistration.RegisterComponent(builder, battleRunner, ScopeTag);
            VContainerComponentRegistration.RegisterComponent(builder, trainingDisplay, ScopeTag);
            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, backgroundView, ScopeTag);
            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, locationCameraView, ScopeTag);
            VContainerComponentRegistration.RegisterComponent(builder, loadSlotView, ScopeTag);

            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, cameraView, ScopeTag);
            builder.Register<ClayEditCameraPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();

            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, postProcessView, ScopeTag);
            VContainerComponentRegistration.RegisterComponent(builder, classroomLighting, ScopeTag);
            builder.Register<BattleCanvasTransition>(Lifetime.Singleton).As<IBattleCanvasTransition>();
            builder.RegisterComponentInHierarchy<BattleStartOverlayView>();

            builder.Register<ClayModelSaveService>(Lifetime.Singleton).As<IClayModelSaveService>();
            builder.Register<ClayModelGltfImporter>(Lifetime.Singleton).As<IClayModelImporter>();
            builder.Register<ClayModelGltfExporter>(Lifetime.Singleton).As<IClayModelExporter>();
        }

        private TrainingScene ConsolidateTrainingSceneRoots()
        {
            TrainingScene[] scenes = FindObjectsByType<TrainingScene>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (scenes.Length == 0)
            {
                return null;
            }

            TrainingScene primary = ResolvePrimaryTrainingScene(scenes, gameObject.scene);
            if (primary == null)
            {
                return null;
            }

            if (!primary.gameObject.activeSelf)
            {
                primary.gameObject.SetActive(true);
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                TrainingScene scene = scenes[i];
                if (scene == null || scene == primary || scene.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (scene.gameObject == gameObject)
                {
                    continue;
                }

                Debug.LogWarning(
                    $"[TrainingLifetimeScope] 重複TrainingSceneルートを削除します: {scene.gameObject.name}");
                DestroyDuplicateTrainingSceneRoot(scene.gameObject);
            }

            return primary;
        }

        private void DestroySelfAsDuplicate()
        {
            GameObject duplicateRoot = gameObject;
            duplicateRoot.SetActive(false);
            Destroy(duplicateRoot);
        }

        private static void DestroyDuplicateTrainingSceneRoot(GameObject duplicateRoot)
        {
            if (duplicateRoot == null)
            {
                return;
            }

            duplicateRoot.SetActive(false);

            // LighthouseがOnLoad直後にFindSceneBaseするため他ルートは即時削除する
            // 自身のAwake中はDestroyImmediateするとMissingReferenceExceptionになる
            DestroyImmediate(duplicateRoot);
        }

        private static TrainingScene ResolvePrimaryTrainingScene(
            TrainingScene[] scenes,
            UnityEngine.SceneManagement.Scene unityScene)
        {
            TrainingScene best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < scenes.Length; i++)
            {
                TrainingScene scene = scenes[i];
                if (scene == null || scene.gameObject.scene != unityScene)
                {
                    continue;
                }

                int score = ScoreTrainingSceneRoot(scene);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = scene;
                }
            }

            return best;
        }

        private static int ScoreTrainingSceneRoot(TrainingScene scene)
        {
            int score = scene.GetComponentsInChildren<Transform>(true).Length;
            if (scene.GetComponent<TrainingBattleRunner>() != null)
            {
                score += 1000;
            }

            if (scene.GetComponent<TrainingLifetimeScope>() != null)
            {
                score += 500;
            }

            return score;
        }

        private void WireSerializedReferencesFromPrimary(TrainingScene primaryScene)
        {
            trainingScene = primaryScene;
            flowRunner = primaryScene.GetComponent<TrainingFlowRunner>();
            battleRunner = primaryScene.GetComponent<TrainingBattleRunner>();
            EnsureSerializedReferences();
        }

        private void EnsureParentScope()
        {
            if (parentReference.Object != null)
            {
                return;
            }

            parentReference.Object = FindFirstObjectByType<ClayMonstersLifetimeScope>();
            if (parentReference.Object == null)
            {
                Debug.LogError(
                    "[TrainingLifetimeScope] ClayMonstersLifetimeScopeが見つかりません。"
                        + "Bootstrapシーンから起動してください");
            }
        }

        private void EnsureSerializedReferences()
        {
            if (trainingScene == null)
            {
                trainingScene = GetComponent<TrainingScene>();
            }

            if (flowRunner == null)
            {
                flowRunner = FindFirstObjectByType<TrainingFlowRunner>(FindObjectsInactive.Include);
            }

            if (battleRunner == null)
            {
                battleRunner = FindFirstObjectByType<TrainingBattleRunner>(FindObjectsInactive.Include);
            }

            if (hudView == null)
            {
                hudView = FindFirstObjectByType<TrainingHudView>(FindObjectsInactive.Include);
            }

            if (trainedSaveView == null)
            {
                trainedSaveView = FindFirstObjectByType<TrainingTrainedSaveView>(FindObjectsInactive.Include);
            }

            if (modeSelectView == null)
            {
                modeSelectView = FindFirstObjectByType<TrainingModeSelectView>(FindObjectsInactive.Include);
            }

            if (autoResultView == null)
            {
                autoResultView = FindFirstObjectByType<TrainingAutoResultView>(FindObjectsInactive.Include);
            }

            if (cameraView == null)
            {
                cameraView = FindFirstObjectByType<ClayEditCameraView>(FindObjectsInactive.Include);
            }

            if (trainingDisplay == null)
            {
                trainingDisplay = FindFirstObjectByType<TrainingDisplay>(FindObjectsInactive.Include);
            }

            if (backgroundView == null)
            {
                backgroundView = FindFirstObjectByType<TrainingBackgroundView>(FindObjectsInactive.Include);
            }

            backgroundView?.EnsureSceneOwnership();

            if (locationCameraView == null)
            {
                locationCameraView = FindFirstObjectByType<TrainingLocationCameraView>(FindObjectsInactive.Include);
            }

            if (postProcessView == null)
            {
                postProcessView = FindFirstObjectByType<BattleNpcPostProcessView>(FindObjectsInactive.Include);
            }

            if (classroomLighting == null)
            {
                classroomLighting = FindFirstObjectByType<BattleClassroomLighting>(FindObjectsInactive.Include);
            }

            if (classroomLighting == null)
            {
                classroomLighting = gameObject.AddComponent<BattleClassroomLighting>();
            }

            if (loadSlotView == null)
            {
                loadSlotView = FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
            }

            loadSlotView?.ConfigureSavePool(ModelSavePool.Player);

            if (selectionCanvas == null && loadSlotView != null)
            {
                selectionCanvas = loadSlotView.SelectionCanvas;
            }
        }
    }
}
