using Battle.Interface;
using Camera.Model;
using Camera.Presenter;
using Camera.View;
using SaveData;
using SaveData.Interface;
using SaveData.Service;
using Scene.BattleNpcScene;
using Scene.BattleNpcScene.View;
using Scene.BattlePVPScene.View;
using Scene.Core;
using UI.ClayEditor.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.BattlePvpArena
{
    /// <summary>
    /// BattlePvpArenaシーンのDI構成
    /// </summary>
    public sealed class BattlePvpArenaLifetimeScope : LifetimeScope
    {
        [Header("Scene Views")]
        [SerializeField] private BattlePvpArenaScene arenaScene;
        [SerializeField] private BattlePvpVictoryReturnView pvpVictoryReturnView;
        [SerializeField] private BattlePvpDisconnectView pvpDisconnectView;
        [SerializeField] private BattlePvpOpponentWaitView pvpOpponentWaitView;

        [Header("Camera")]
        [SerializeField] private ClayEditCameraView cameraView;

        [Header("Battle")]
        [SerializeField] private BattlePvpArenaFlowRunner arenaFlowRunner;

        [Header("Post Process")]
        [SerializeField] private BattleNpcPostProcessView postProcessView;
        [SerializeField] private BattleClassroomLighting classroomLighting;

        [Header("UI Views")]
        [SerializeField] private LoadSlotView loadSlotView;

        protected override void Awake()
        {
            EnsureSerializedReferences();
            if (parentReference.Object == null)
            {
                parentReference.Object = FindFirstObjectByType<ClayMonstersLifetimeScope>();
            }

            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            EnsureSerializedReferences();
            RegisterComponent(builder, arenaScene);
            RegisterComponent(builder, arenaFlowRunner);
            RegisterComponentAsInterfaces(builder, pvpVictoryReturnView);
            RegisterComponentAsInterfaces(builder, pvpDisconnectView);
            RegisterComponentAsInterfaces(builder, pvpOpponentWaitView);
            RegisterComponentAsInterfaces(builder, cameraView);
            builder.Register<ClayEditCameraPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();
            RegisterComponentAsInterfaces(builder, postProcessView);
            RegisterComponent(builder, classroomLighting);
            builder.Register<BattleCanvasTransition>(Lifetime.Singleton).As<IBattleCanvasTransition>();
            builder.Register<MonsterSelectionSession>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<BattleStartOverlayView>();
            builder.Register<ClayModelSaveService>(Lifetime.Singleton).As<IClayModelSaveService>();
            builder.Register<ClayModelGltfImporter>(Lifetime.Singleton).As<IClayModelImporter>();
            builder.Register<ClayModelGltfExporter>(Lifetime.Singleton).As<IClayModelExporter>();
            RegisterComponent(builder, loadSlotView);
        }

        private void EnsureSerializedReferences()
        {
            if (arenaScene == null)
            {
                arenaScene = GetComponent<BattlePvpArenaScene>();
            }

            if (arenaFlowRunner == null)
            {
                arenaFlowRunner = GetComponentInChildren<BattlePvpArenaFlowRunner>(true);
            }

            if (pvpVictoryReturnView == null)
            {
                pvpVictoryReturnView = GetComponentInChildren<BattlePvpVictoryReturnView>(true);
            }

            if (pvpDisconnectView == null)
            {
                pvpDisconnectView = GetComponentInChildren<BattlePvpDisconnectView>(true);
            }

            if (pvpOpponentWaitView == null)
            {
                pvpOpponentWaitView = GetComponentInChildren<BattlePvpOpponentWaitView>(true);
            }

            if (cameraView == null)
            {
                cameraView = GetComponentInChildren<ClayEditCameraView>(true);
            }

            if (postProcessView == null)
            {
                postProcessView = GetComponentInChildren<BattleNpcPostProcessView>(true);
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
                loadSlotView = GetComponentInChildren<LoadSlotView>(true);
            }

            loadSlotView?.ConfigureSavePool(ModelSavePool.TrainedPlayer, "未育成");
        }

        private static void RegisterComponent<T>(IContainerBuilder builder, T component) where T : Component
        {
            if (component == null)
            {
                Debug.LogError($"[BattlePvpArenaLifetimeScope] {typeof(T).Name} が未設定です");
                return;
            }

            builder.RegisterComponent(component);
        }

        private static void RegisterComponentAsInterfaces<T>(IContainerBuilder builder, T component) where T : Component
        {
            if (component == null)
            {
                Debug.LogError($"[BattlePvpArenaLifetimeScope] {typeof(T).Name} が未設定です");
                return;
            }

            builder.RegisterComponent(component).AsImplementedInterfaces();
        }
    }
}
