using ClayEditor.Input.Interface;
using ClayEditor.Interface;
using ClayEditor.Paint;
using GameData;
using R3;
using UI.ColorPicker;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ClayEditor
{
    /// <summary>
    /// Paint モード専用のカーソル。カラーピッカーの色を取得して、
    /// メッシュ表面にレイキャストして頂点カラーをペイントする。
    /// クレイカーソルと同様、レイがヒットしなくてもカーソルは深度に投影した位置へ追従する。
    /// </summary>
    public class ClayPaintCursor : MonoBehaviour, ITickable
    {
        [Inject] private readonly ClayPainter painter;
        [Inject] private readonly IClayInputProvider input;
        [Inject] private readonly IClaySceneContext sceneContext;
        [Inject] private readonly ColorPicker colorPicker;

        [SerializeField] private GameObject cursorObject;
        [SerializeField] private MeshRenderer cursorMeshRenderer;
        [SerializeField] private LayerMask raycastLayerMask = -1;
        [SerializeField] private float brushSizeChangeSpeed = 0.1f;

        private UnityEngine.Camera mainCamera;
        private readonly CursorRaycaster raycaster = new();

        // カーソルの色表示に使うマテリアルのインスタンス
        private Material cursorMaterial;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Start()
        {
            mainCamera = UnityEngine.Camera.main;
            if (cursorObject == null)
            {
                cursorObject = gameObject;
            }

            // カーソルのマテリアルインスタンスを取得
            if (cursorMeshRenderer != null)
            {
                cursorMaterial = cursorMeshRenderer.material;
            }

            // 初期色をカーソルへ反映
            ApplyCursorColor(painter.CurrentColor);

            // カラーピッカーで選ばれた色をペイント色とカーソル色の両方へ反映する
            colorPicker.OnColorSelected
                .Subscribe(color =>
                {
                    painter.SetColor(color);
                    ApplyCursorColor(color);
                })
                .AddTo(this);

            // ホイールでペイントブラシ半径を変更
            input.OnScroll
                .Subscribe(scroll =>
                {
                    if (!isActiveAndEnabled || input.IsAltPressed)
                    {
                        return;
                    }

                    if (sceneContext.CurrentMode.Value != EditModeType.Paint)
                    {
                        return;
                    }

                    painter.ChangeBrushRadius(scroll * 0.001f * brushSizeChangeSpeed);
                })
                .AddTo(this);

            // PaintモードかつUI上でないときだけカーソルを表示する
            Observable.CombineLatest(
                    sceneContext.CurrentMode,
                    input.OnPointerOverUIChanged,
                    (mode, isOverUI) => mode == EditModeType.Paint && !isOverUI)
                .Subscribe(isVisible =>
                {
                    if (cursorMeshRenderer != null)
                    {
                        cursorMeshRenderer.enabled = isVisible;
                    }
                })
                .AddTo(this);

            if (mainCamera != null)
            {
                raycaster.InitializeDepth(mainCamera, cursorObject.transform.position);
            }
        }

        /// <inheritdoc />
        public void Tick()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (mainCamera == null)
            {
                return;
            }

            // Paintモード以外では何もしない
            if (sceneContext.CurrentMode.Value != EditModeType.Paint)
            {
                return;
            }

            if (input.IsPointerOverUI)
            {
                return;
            }

            // カーソル位置の更新（ヒットしなくても深度に投影した位置へ追従させる）
            Vector3 worldPos = raycaster.Resolve(
                mainCamera,
                painter.transform,
                input.PointerPosition,
                raycastLayerMask,
                lockDepth: input.IsShiftPressed,
                resetDepthOnMiss: true);

            if (cursorObject != null)
            {
                cursorObject.transform.position = worldPos;
                cursorObject.transform.localScale = Vector3.one * painter.BrushRadius;
            }

            // Alt中（カメラ操作中）はペイントしない
            if (input.IsAltPressed)
            {
                return;
            }

            // ペイントはメッシュ表面にヒットしたときのみ行う
            if (input.IsPrimaryHeld)
            {
                Ray ray = mainCamera.ScreenPointToRay(input.PointerPosition);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, raycastLayerMask))
                {
                    painter.PaintAtWorldPosition(hit.point);
                }
            }
        }

        private void ApplyCursorColor(Color color)
        {
            if (cursorMaterial == null)
            {
                return;
            }

            // URP は _BaseColor、Built-in は _Color を使う
            if (cursorMaterial.HasProperty(BaseColorId))
            {
                cursorMaterial.SetColor(BaseColorId, color);
            }
            else if (cursorMaterial.HasProperty(ColorId))
            {
                cursorMaterial.SetColor(ColorId, color);
            }
        }

        private void OnDestroy()
        {
            if (cursorMaterial != null)
            {
                Destroy(cursorMaterial);
            }
        }
    }
}