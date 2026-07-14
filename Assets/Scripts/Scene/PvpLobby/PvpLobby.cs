using Scene.BattlePVPScene.Interface;
using Scene.PvpLobby.Interface;
using Scene.PvpLobby.Presenter;
using UnityEngine;
using VContainer;

namespace Scene.PvpLobby
{
    /// <summary>
    /// DDOL常駐のPvPマッチングロビー
    /// </summary>
    public sealed class PvpLobby : MonoBehaviour
    {
        [SerializeField] private GameObject uiRoot;

        private IPvpLobbyRegistry registry;
        private IPvpLobbyPresenter presenter;
        private IBattlePvpMatchmakingService matchmakingService;
        private bool isInitialized;

        [Inject]
        public void Construct(
            IPvpLobbyRegistry registry,
            IPvpLobbyPresenter presenter,
            IBattlePvpMatchmakingService matchmakingService)
        {
            this.registry = registry;
            this.presenter = presenter;
            this.matchmakingService = matchmakingService;
        }

        private void Awake()
        {
            if (uiRoot == null)
            {
                uiRoot = gameObject;
            }

            uiRoot.SetActive(false);
        }

        /// <summary>
        /// Editorフォールバック生成時にUIルートを設定する
        /// </summary>
        public void ConfigureForRuntime(GameObject root)
        {
            uiRoot = root;
            uiRoot.SetActive(false);
        }

        private void Start()
        {
            InitializeIfNeeded();
        }

        /// <summary>
        /// ロビーUIを表示する
        /// </summary>
        public void Show()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            EnsureUiRoot();
            InitializeIfNeeded();
            uiRoot.SetActive(true);
            presenter?.OnShow();
        }

        /// <summary>
        /// ロビーUIを非表示にする
        /// </summary>
        public void Hide()
        {
            EnsureUiRoot();
            presenter?.OnHide();
            if (uiRoot != null)
            {
                uiRoot.SetActive(false);
            }
        }

        private void EnsureUiRoot()
        {
            if (uiRoot != null)
            {
                return;
            }

            Transform canvasTransform = transform.Find("MatchmakingCanvas");
            uiRoot = canvasTransform != null ? canvasTransform.gameObject : gameObject;
        }

        private void InitializeIfNeeded()
        {
            if (isInitialized)
            {
                return;
            }

            if (registry == null || presenter == null)
            {
                Debug.LogWarning(
                    "[PvpLobby] DI未完了"
                    + $" registry={(registry != null)}"
                    + $" presenter={(presenter != null)}");
                return;
            }

            registry.Bind(this, matchmakingService);
            presenter.Setup();
            isInitialized = true;
        }
    }
}
