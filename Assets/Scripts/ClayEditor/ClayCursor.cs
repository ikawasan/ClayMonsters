using ClayEditor.Input.Interface;
using ClayEditor.Interface;
using GameData;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using R3;

namespace ClayEditor
{
    public class ClayCursor : MonoBehaviour, ITickable
    {
        [Inject] private readonly ClayEditor editor;
        [Inject] private readonly IClayInputProvider input;
        [Inject] private readonly IClaySceneContext sceneContext;

        [SerializeField] private GameObject cursorObject;
        [SerializeField] private MeshRenderer cursorMeshRenderer;
        [SerializeField] private LayerMask raycastLayerMask = -1;
        [SerializeField] private float brushSizeChangeSpeed = 0.1f;

        private UnityEngine.Camera mainCamera;
        private readonly CursorRaycaster raycaster = new();

        void Start()
        {
            mainCamera = UnityEngine.Camera.main;

            if (mainCamera != null)
            {
                raycaster.InitializeDepth(mainCamera, cursorObject.transform.position);
            }

            input.OnUndo
                .Subscribe(_ => TryInvoke(() => editor.Undo()))
                .AddTo(this);

            input.OnRedo
                .Subscribe(_ => TryInvoke(() => editor.Redo()))
                .AddTo(this);

            input.OnDelete
                .Subscribe(_ => TryInvoke(() =>
                {
                    // 全削除の前に状態を保存しておく
                    editor.SaveState();
                    editor.ClearMesh();
                }))
                .AddTo(this);

            input.OnScroll
                .Subscribe(scroll =>
                {
                    // 無効状態 / Alt中（カメラ操作中）/ Clayモード以外ではブラシサイズを変更しない
                    if (!isActiveAndEnabled || input.IsAltPressed)
                    {
                        return;
                    }

                    if (sceneContext.CurrentMode.Value != EditModeType.Clay)
                    {
                        return;
                    }

                    editor.ChangeBrushRadius(scroll * 0.001f * brushSizeChangeSpeed);
                })
                .AddTo(this);

            // 造形を始めた最初の1フレーム目に状態を保存する
            input.OnPrimaryPressed
                .Subscribe(_ => TrySaveStateOnPress())
                .AddTo(this);

            input.OnSecondaryPressed
                .Subscribe(_ => TrySaveStateOnPress())
                .AddTo(this);

            Observable.CombineLatest(
                sceneContext.CurrentMode,
                input.OnPointerOverUIChanged,
                (mode, isOverUI) => mode == EditModeType.Clay && !isOverUI)
            .Subscribe(isVisible =>
            {
                if (cursorMeshRenderer != null)
                {
                    cursorMeshRenderer.enabled = isVisible;
                }
            })
            .AddTo(this);
        }

        /// <inheritdoc />
        public void Tick()
        {
            // 無効状態（モード切替で GameObject 非アクティブ等）では何もしない
            if (!isActiveAndEnabled)
            {
                return;
            }

            // Clay モード以外では造形しない
            if (sceneContext.CurrentMode.Value != EditModeType.Clay)
            {
                return;
            }

            if (mainCamera == null)
            {
                return;
            }

            if (input.IsPointerOverUI)
            {
                return;
            }

            // カーソル位置の更新
            Vector3 worldPos = raycaster.Resolve(
                mainCamera,
                editor.transform,
                input.PointerPosition,
                raycastLayerMask,
                lockDepth: input.IsShiftPressed,
                resetDepthOnMiss: true);

            if (cursorObject != null)
            {
                cursorObject.transform.position = worldPos;
                cursorObject.transform.localScale = Vector3.one * editor.BrushRadius;
            }

            // Alt中（カメラ操作中）は造形しない
            if (input.IsAltPressed)
            {
                return;
            }

            // 左ドラッグ：Ctrlで削り、それ以外は盛る
            if (input.IsPrimaryHeld)
            {
                editor.ModifyAtWorldPosition(worldPos, input.IsCtrlPressed);
            }

            // 右ドラッグ：常に削る
            if (input.IsSecondaryHeld)
            {
                editor.ModifyAtWorldPosition(worldPos, true);
            }
        }

        private void TrySaveStateOnPress()
        {
            if (!isActiveAndEnabled || input.IsAltPressed)
            {
                return;
            }

            if (sceneContext.CurrentMode.Value != EditModeType.Clay)
            {
                return;
            }

            editor.SaveState();
        }

        // 無効状態では操作を無視
        private void TryInvoke(System.Action action)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            action();
        }
    }
}