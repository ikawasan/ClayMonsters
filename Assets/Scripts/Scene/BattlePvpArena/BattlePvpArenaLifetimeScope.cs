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
            if (arenaScene == null
                || arenaFlowRunner == null
                || pvpVictoryReturnView == null
                || pvpDisconnectView == null
                || pvpOpponentWaitView == null
                || cameraView == null
                || postProcessView == null
                || classroomLighting == null
                || matchupBackground == null
                || loadSlotView == null)
            {
                Debug.LogError(
                    $"[{ScopeTag}] 必須SerializeFieldが未配線ですHierarchyで接続してください",
                    this);
            }

            loadSlotView?.ConfigureSavePool(
                ModelSavePool.TrainedPlayer,
                Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.SaveUntrainedPool,
                    "未育成"));
        }
    }
}
