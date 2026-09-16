using Extensions;
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
        private const int MatchmakingCanvasSortingOrder = 500;

        [SerializeField] private GameObject uiRoot;

        private IPvpLobbyRegistry registry;
        private IPvpLobbyPresenter presenter;
        private IBattlePvpMatchmakingService matchmakingService;
        private bool isInitialized;
        private Canvas matchmakingCanvas;

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

            SetMatchmakingCanvasVisible(false);
        }

        /// <summary>
        /// Editorフォールバック生成時にUIルートを設定する
        /// </summary>
        public void ConfigureForRuntime(GameObject root)
        {
            uiRoot = root;
            matchmakingCanvas = null;
            SetMatchmakingCanvasVisible(false);
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
            SetMatchmakingCanvasVisible(true);
            presenter?.OnShow();
        }

        /// <summary>
        /// ロビーUIを非表示にする
        /// </summary>
        public void Hide()
        {
            EnsureUiRoot();
            presenter?.OnHide();
            SetMatchmakingCanvasVisible(false);
        }

        private void EnsureUiRoot()
        {
            if (uiRoot != null)
            {
                return;
            }

            Transform canvasTransform = transform.Find("MatchmakingCanvas");
            uiRoot = canvasTransform != null ? canvasTransform.gameObject : gameObject;
            matchmakingCanvas = null;
        }

        private void SetMatchmakingCanvasVisible(bool visible)
        {
            EnsureUiRoot();
            if (uiRoot == null)
            {
                return;
            }

            if (matchmakingCanvas == null)
            {
                matchmakingCanvas = uiRoot.GetComponent<Canvas>();
            }

            if (matchmakingCanvas == null)
            {
                Debug.LogError(
                    "[PvpLobby] MatchmakingCanvasのCanvasがありません",
                    this);
                uiRoot.SetActive(visible);
                return;
            }

            if (visible)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(
                    matchmakingCanvas,
                    true,
                    MatchmakingCanvasSortingOrder);
                return;
            }

            CanvasVisibilityUtility.SetCanvasEnabled(matchmakingCanvas, false);
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
