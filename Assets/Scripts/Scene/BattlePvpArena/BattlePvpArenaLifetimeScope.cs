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
        private const string ScopeTag = "BattlePvpArenaLifetimeScope";

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
        [SerializeField] private BattleMatchupBackgroundView matchupBackground;

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
            VContainerComponentRegistration.RegisterComponent(builder, arenaScene, ScopeTag);
            VContainerComponentRegistration.RegisterComponent(builder, arenaFlowRunner, ScopeTag);
            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, pvpVictoryReturnView, ScopeTag);
            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, pvpDisconnectView, ScopeTag);
            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, pvpOpponentWaitView, ScopeTag);
            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, cameraView, ScopeTag);
            builder.Register<ClayEditCameraPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();
            VContainerComponentRegistration.RegisterComponentAsInterfaces(builder, postProcessView, ScopeTag);
            VContainerComponentRegistration.RegisterComponent(builder, classroomLighting, ScopeTag);
            VContainerComponentRegistration.RegisterComponent(builder, matchupBackground, ScopeTag);
            builder.Register<BattleCanvasTransition>(Lifetime.Singleton).As<IBattleCanvasTransition>();
            builder.Register<MonsterSelectionSession>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<BattleStartOverlayView>();
            builder.Register<ClayModelSaveService>(Lifetime.Singleton).As<IClayModelSaveService>();
            builder.Register<ClayModelGltfImporter>(Lifetime.Singleton).As<IClayModelImporter>();
            builder.Register<ClayModelGltfExporter>(Lifetime.Singleton).As<IClayModelExporter>();
            VContainerComponentRegistration.RegisterComponent(builder, loadSlotView, ScopeTag);
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

            if (matchupBackground == null)
            {
                Debug.LogError($"[{ScopeTag}] matchupBackgroundが未配線です", this);
            }

            if (loadSlotView == null)
            {
                loadSlotView = GetComponentInChildren<LoadSlotView>(true);
            }

            loadSlotView?.ConfigureSavePool(ModelSavePool.TrainedPlayer, "未育成");
        }
    }
}
