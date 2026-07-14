using Scene.BattlePVPScene.Interface;
using Scene.BattlePVPScene.Network;
using Scene.BattlePVPScene.Service;
using Scene.BattlePVPScene.View;
using Scene.Core;
using Scene.PvpLobby.Interface;
using Scene.PvpLobby.Presenter;
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.PvpLobby
{
    /// <summary>
    /// PvpLobbyのDI構成
    /// LifetimeScopeはPvpLobbyDi子オブジェクトに置きルートとは分離する
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class PvpLobbyLifetimeScope : LifetimeScope
    {
        private const string DedicatedScopeObjectName = "PvpLobbyDi";

        private static PendingRuntimeConfiguration pendingRuntimeConfiguration;
        private static LifetimeScope pendingParentScope;

        [Header("Lobby")]
        [SerializeField] private PvpLobby pvpLobby;
        [SerializeField] private BattlePVPView lobbyView;

        [Header("Network")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private BattlePvpSessionSpawner sessionSpawner;

        /// <summary>
        /// 親LifetimeScopeを設定する
        /// </summary>
        public static void SetPendingParentScope(LifetimeScope parentScope)
        {
            pendingParentScope = parentScope;
        }

        protected override void Awake()
        {
            ApplyPendingRuntimeConfiguration();

            if (parentReference.Object == null && pendingParentScope != null)
            {
                parentReference.Object = pendingParentScope;
            }

            if (GetComponent<PvpLobby>() != null)
            {
                MigrateToDedicatedChildScope();
                return;
            }

            if (parentReference.Object == null)
            {
                parentReference.Object = FindFirstObjectByType<ClayMonstersLifetimeScope>();
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

        /// <summary>
        /// Editorフォールバック生成時に参照を設定する
        /// </summary>
        public static void SetPendingRuntimeConfiguration(
            PvpLobby lobby,
            BattlePVPView view,
            NetworkManager manager,
            BattlePvpSessionSpawner spawner)
        {
            pendingRuntimeConfiguration = new PendingRuntimeConfiguration
            {
                Lobby = lobby,
                View = view,
                NetworkManager = manager,
                SessionSpawner = spawner
            };
        }

        protected override void Configure(IContainerBuilder builder)
        {
            ApplyPendingRuntimeConfiguration();
            EnsureSerializedReferences();

            RegisterComponent(builder, pvpLobby);
            RegisterComponentAsInterfaces(builder, lobbyView);
            builder.Register<PvpLobbyPresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            RegisterComponent(builder, networkManager);
            RegisterComponent(builder, sessionSpawner);
            builder.Register<NetcodeBattlePvpMatchmakingService>(Lifetime.Singleton).AsImplementedInterfaces();
        }

        private void MigrateToDedicatedChildScope()
        {
            Transform existingChild = transform.Find(DedicatedScopeObjectName);
            if (existingChild != null && existingChild.GetComponent<PvpLobbyLifetimeScope>() != null)
            {
                autoRun = false;
                Destroy(this);
                return;
            }

            var dedicatedObject = new GameObject(DedicatedScopeObjectName);
            dedicatedObject.transform.SetParent(transform, false);
            PvpLobbyLifetimeScope migratedScope = dedicatedObject.AddComponent<PvpLobbyLifetimeScope>();
            migratedScope.pvpLobby = pvpLobby != null ? pvpLobby : GetComponent<PvpLobby>();
            migratedScope.lobbyView = lobbyView;
            migratedScope.networkManager = networkManager;
            migratedScope.sessionSpawner = sessionSpawner;
            migratedScope.parentReference = parentReference;

            autoRun = false;
            Destroy(this);
        }

        private void EnsureSerializedReferences()
        {
            if (pvpLobby == null)
            {
                pvpLobby = GetComponentInParent<PvpLobby>(true);
            }

            if (lobbyView == null && pvpLobby != null)
            {
                lobbyView = pvpLobby.GetComponentInChildren<BattlePVPView>(true);
            }

            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }

            if (sessionSpawner == null)
            {
                sessionSpawner = FindFirstObjectByType<BattlePvpSessionSpawner>(FindObjectsInactive.Include);
            }
        }

        private void ApplyPendingRuntimeConfiguration()
        {
            if (pendingRuntimeConfiguration == null)
            {
                return;
            }

            pvpLobby = pendingRuntimeConfiguration.Lobby;
            lobbyView = pendingRuntimeConfiguration.View;
            networkManager = pendingRuntimeConfiguration.NetworkManager;
            sessionSpawner = pendingRuntimeConfiguration.SessionSpawner;
            pendingRuntimeConfiguration = null;
        }

        private sealed class PendingRuntimeConfiguration
        {
            public PvpLobby Lobby;
            public BattlePVPView View;
            public NetworkManager NetworkManager;
            public BattlePvpSessionSpawner SessionSpawner;
        }

        private static void RegisterComponent<T>(IContainerBuilder builder, T component) where T : Component
        {
            if (component == null)
            {
                Debug.LogError($"[PvpLobbyLifetimeScope] {typeof(T).Name} が未設定です");
                return;
            }

            builder.RegisterComponent(component);
        }

        private static void RegisterComponentAsInterfaces<T>(IContainerBuilder builder, T component) where T : Component
        {
            if (component == null)
            {
                Debug.LogError($"[PvpLobbyLifetimeScope] {typeof(T).Name} が未設定です");
                return;
            }

            builder.RegisterComponent(component).AsImplementedInterfaces();
        }
    }
}
