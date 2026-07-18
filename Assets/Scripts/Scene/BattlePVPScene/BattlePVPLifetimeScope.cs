using Battle.Interface;
using Camera.Model;
using Camera.Presenter;
using Camera.View;
using SaveData;
using SaveData.Interface;
using SaveData.Service;
using Scene.BattleNpcScene;
using Scene.BattleNpcScene.Presenter;
using Scene.BattleNpcScene.View;
using Scene.BattlePVPScene.Interface;
using Scene.BattlePVPScene.Network;
using Scene.BattlePVPScene.Presenter;
using Scene.BattlePVPScene.Service;
using Scene.BattlePVPScene.View;
using System.Diagnostics;
using UI.ClayEditor.View;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Scene.BattlePVPScene
{
    /// <summary>
    /// BattlePVPシーンのDI構成
    /// LifetimeScope.DisposeはgameObjectをDestroyするためBattlePVPSceneルートとは別GameObjectに置く
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class BattlePVPLifetimeScope : LifetimeScope
    {
        private const string DedicatedScopeObjectName = "BattlePvpDi";

        [Header("Scene")]
        [FormerlySerializedAs("battlePVPScene")]
        [SerializeField] private BattlePVPScene battlePvpScene;
        [FormerlySerializedAs("battlePVPView")]
        [SerializeField] private BattlePVPView battlePvpView;

        [Header("Camera")]
        [SerializeField] private ClayEditCameraView cameraView;

        [Header("Battle")]
        [SerializeField] private BattlePvpFlowRunner battlePvpFlowRunner;

        [Header("Post Process")]
        [SerializeField] private BattleNpcPostProcessView postProcessView;

        [Header("Network")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private BattlePvpSessionSpawner sessionSpawner;

        [Header("UI")]
        [SerializeField] private LoadSlotView loadSlotView;
        [SerializeField] private BattlePvpVictoryReturnView pvpVictoryReturnView;
        [SerializeField] private BattlePvpDisconnectView pvpDisconnectView;
        [SerializeField] private BattlePvpOpponentWaitView pvpOpponentWaitView;

        protected override void Awake()
        {
            if (GetComponent<BattlePVPScene>() != null)
            {
                MigrateToDedicatedChildScope();
                return;
            }

            base.Awake();
        }

        /// <summary>
        /// VContainer標準DisposeはgameObjectをDestroyするためgameObject破棄を行わない
        /// </summary>
        public new void Dispose()
        {
            DisposeCore();
        }

        protected override void OnDestroy()
        {
            if (!applicationQuitting)
            {
                Debug.LogWarning(
                    "[BattlePVPLifetimeScope] OnDestroy"
                    + $" owner={name}"
                    + $" scene={gameObject.scene.name}"
                    + $" stack={new StackTrace(1, true)}");
            }

            base.OnDestroy();
        }

        private static bool applicationQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetQuitFlag()
        {
            applicationQuitting = false;
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            applicationQuitting = true;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            EnsureSerializedReferences();

            RegisterComponent(builder, battlePvpScene);
            RegisterComponentAsInterfaces(builder, battlePvpView);
            builder.Register<BattlePVPPresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            RegisterComponentAsInterfaces(builder, cameraView);
            builder.Register<ClayEditCameraPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();

            RegisterFlowRunner(builder);
            RegisterComponentAsInterfaces(builder, pvpVictoryReturnView);
            RegisterComponentAsInterfaces(builder, pvpDisconnectView);
            RegisterComponentAsInterfaces(builder, pvpOpponentWaitView);
            RegisterComponentAsInterfaces(builder, postProcessView);
            builder.Register<BattleCanvasTransition>(Lifetime.Singleton).As<IBattleCanvasTransition>();
            builder.Register<MonsterSelectionSession>(Lifetime.Singleton).AsImplementedInterfaces();
            RegisterOverlayFromSceneRoot(builder);

            builder.Register<ClayModelSaveService>(Lifetime.Singleton).As<IClayModelSaveService>();
            builder.Register<ClayModelGltfImporter>(Lifetime.Singleton).As<IClayModelImporter>();
            builder.Register<ClayModelGltfExporter>(Lifetime.Singleton).As<IClayModelExporter>();
            RegisterComponent(builder, loadSlotView);

            RegisterComponent(builder, networkManager);
            RegisterComponent(builder, sessionSpawner);
            builder.Register<NetcodeBattlePvpMatchmakingService>(Lifetime.Singleton).AsImplementedInterfaces();
        }

        private void MigrateToDedicatedChildScope()
        {
            Transform existingChild = transform.Find(DedicatedScopeObjectName);
            if (existingChild != null && existingChild.GetComponent<BattlePVPLifetimeScope>() != null)
            {
                autoRun = false;
                Destroy(this);
                return;
            }

            var dedicatedObject = new GameObject(DedicatedScopeObjectName);
            dedicatedObject.transform.SetParent(transform, false);
            BattlePVPLifetimeScope migratedScope = dedicatedObject.AddComponent<BattlePVPLifetimeScope>();
            migratedScope.CopyReferencesFrom(this);

            Debug.LogWarning(
                "[BattlePVPLifetimeScope] シーンルートから専用子オブジェクトへ移行しました"
                + $" parent={name}");

            autoRun = false;
            Destroy(this);
        }

        internal void CopyReferencesFrom(BattlePVPLifetimeScope source)
        {
            battlePvpScene = source.battlePvpScene;
            battlePvpView = source.battlePvpView;
            cameraView = source.cameraView;
            battlePvpFlowRunner = source.battlePvpFlowRunner;
            postProcessView = source.postProcessView;
            networkManager = source.networkManager;
            sessionSpawner = source.sessionSpawner;
            loadSlotView = source.loadSlotView;
            pvpVictoryReturnView = source.pvpVictoryReturnView;
            pvpDisconnectView = source.pvpDisconnectView;
            pvpOpponentWaitView = source.pvpOpponentWaitView;
            parentReference = source.parentReference;
        }

        private void EnsureSerializedReferences()
        {
            Transform sceneRoot = ResolveSceneRootTransform();

            if (battlePvpScene == null && sceneRoot != null)
            {
                battlePvpScene = sceneRoot.GetComponent<BattlePVPScene>();
            }

            if (battlePvpView == null && sceneRoot != null)
            {
                battlePvpView = sceneRoot.GetComponentInChildren<BattlePVPView>(true);
            }

            if (cameraView == null && sceneRoot != null)
            {
                cameraView = sceneRoot.GetComponentInChildren<ClayEditCameraView>(true);
            }

            if (battlePvpFlowRunner == null && sceneRoot != null)
            {
                battlePvpFlowRunner = sceneRoot.GetComponent<BattlePvpFlowRunner>();
                if (battlePvpFlowRunner == null)
                {
                    battlePvpFlowRunner = sceneRoot.GetComponentInChildren<BattlePvpFlowRunner>(true);
                }
            }

            if (postProcessView == null && sceneRoot != null)
            {
                postProcessView = sceneRoot.GetComponentInChildren<BattleNpcPostProcessView>(true);
            }

            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }

            if (sessionSpawner == null)
            {
                sessionSpawner = FindFirstObjectByType<BattlePvpSessionSpawner>(FindObjectsInactive.Include);
            }

            if (loadSlotView == null && sceneRoot != null)
            {
                loadSlotView = sceneRoot.GetComponentInChildren<LoadSlotView>(true);
            }

            loadSlotView?.ConfigureSavePool(ModelSavePool.TrainedPlayer, "未育成");

            if (pvpVictoryReturnView == null && sceneRoot != null)
            {
                pvpVictoryReturnView = sceneRoot.GetComponentInChildren<BattlePvpVictoryReturnView>(true);
            }

            if (pvpDisconnectView == null && sceneRoot != null)
            {
                pvpDisconnectView = sceneRoot.GetComponentInChildren<BattlePvpDisconnectView>(true);
            }

            if (pvpOpponentWaitView == null && sceneRoot != null)
            {
                pvpOpponentWaitView = sceneRoot.GetComponentInChildren<BattlePvpOpponentWaitView>(true);
            }
        }

        private Transform ResolveSceneRootTransform()
        {
            BattlePVPScene scene = GetComponentInParent<BattlePVPScene>(true);
            if (scene != null)
            {
                return scene.transform;
            }

            return battlePvpScene != null ? battlePvpScene.transform : null;
        }

        private void RegisterOverlayFromSceneRoot(IContainerBuilder builder)
        {
            Transform sceneRoot = ResolveSceneRootTransform();
            if (sceneRoot == null)
            {
                return;
            }

            BattleStartOverlayView overlay = sceneRoot.GetComponentInChildren<BattleStartOverlayView>(true);
            if (overlay != null)
            {
                builder.RegisterComponent(overlay);
            }
        }

        private void RegisterFlowRunner(IContainerBuilder builder)
        {
            if (battlePvpFlowRunner == null)
            {
                Debug.LogError(
                    "[BattlePVPLifetimeScope] BattlePvpFlowRunner が未設定です。シーン上の参照をHierarchyで確認してください");
                return;
            }

            builder.RegisterComponent(battlePvpFlowRunner).AsSelf().AsImplementedInterfaces();
        }

        private static void RegisterComponent<T>(IContainerBuilder builder, T component) where T : Component
        {
            if (component == null)
            {
                Debug.LogError(
                    $"[BattlePVPLifetimeScope] {typeof(T).Name} が未設定です。シーン上の参照をHierarchyで確認してください");
                return;
            }

            builder.RegisterComponent(component);
        }

        private static void RegisterComponentAsInterfaces<T>(IContainerBuilder builder, T component) where T : Component
        {
            if (component == null)
            {
                Debug.LogError(
                    $"[BattlePVPLifetimeScope] {typeof(T).Name} が未設定です。シーン上の参照をHierarchyで確認してください");
                return;
            }

            builder.RegisterComponent(component).AsImplementedInterfaces();
        }
    }
}
