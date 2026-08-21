using Audio;
using Audio.Interface;
using ClayEditor.Input.Interface;
using ClayEditor.Interface;
using GameData;
using R3;
using UI.ColorPicker;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ClayEditor.Paint
{
    /// <summary>
    /// Paintモード専用のカーソル
    /// </summary>
    public class ClayPaintCursor : MonoBehaviour, ITickable
    {
        [Inject] private readonly ClayPainter painter;
        [Inject] private readonly ClayVoxelEngine engine;
        [Inject] private readonly IClayInputProvider input;
        [Inject] private readonly IClaySceneContext sceneContext;
        [Inject] private readonly ColorPicker colorPicker;
        [Inject] private readonly ISeService seService;

        [SerializeField] private GameObject cursorObject;
        [SerializeField] private MeshRenderer cursorMeshRenderer;
        [SerializeField] private LayerMask raycastLayerMask = -1;
        [SerializeField] private float brushSizeChangeSpeed = 0.1f;
        [SerializeField] [Range(0.05f, 1f)] private float cursorAlpha = 0.4f;

        private UnityEngine.Camera mainCamera;
        private readonly CursorRaycaster raycaster = new();
        private float cursorMeshDiameter = 1f;

        // カーソルの色表示に使うマテリアルのインスタンス
        private Material cursorMaterial;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        // 直前フレームに塗っていたか(ストローク終了の検知に使う)
        private bool wasPainting;
        // 直前フレームでShiftによる深度固定中だったか
        private bool wasShiftPressed;

        // 現在のストロークで実際に1回でも塗ったか(空振りを履歴へ入れないため)
        private bool paintedInStroke;
        private SeTrackId? playingPaintSe;
        private Vector2 lastPaintPointer;
        private bool hasLastPaintPointer;
        private bool playedPaintIn;
        private bool suppressPaintUntilPrimaryRelease;

        private const float MinPaintSePitch = 0.55f;
        private const float MaxPaintSePitch = 2.6f;
        private const float SpeedForMaxPaintSePitch = 1.2f;
        private const float MinPaintMovePixels = 1.25f;

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
                ApplyCursorTransparency();
            }

            CacheCursorMeshDiameter();

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
                    if (!IsPaintActive())
                    {
                        return;
                    }

                    painter.ChangeBrushRadius(scroll * 0.001f * brushSizeChangeSpeed);
                })
                .AddTo(this);

            // ストローク開始時に履歴へ新しいストロークを開始する
            input.OnPrimaryPressed
                .Subscribe(_ =>
                {
                    if (!IsPaintActive())
                    {
                        return;
                    }

                    if (colorPicker != null && colorPicker.IsEyedropperActive)
                    {
                        TryPickColorAtPointer();
                        return;
                    }

                    painter.BeginStroke();
                    paintedInStroke = false;
                })
                .AddTo(this);

            input.OnSecondaryPressed
                .Subscribe(_ =>
                {
                    if (colorPicker != null && colorPicker.IsEyedropperActive)
                    {
                        colorPicker.CancelEyedropper();
                    }
                })
                .AddTo(this);

            // Ctrl + Z で取り消す(Paintモード時のみ)
            input.OnUndo
                .Subscribe(_ =>
                {
                    if (!isActiveAndEnabled)
                    {
                        return;
                    }

                    if (sceneContext.CurrentMode.Value != EditModeType.Paint)
                    {
                        return;
                    }

                    painter.Undo();
                })
                .AddTo(this);

            // Ctrl + Y でやり直す(Paintモード時のみ)
            input.OnRedo
                .Subscribe(_ =>
                {
                    if (!isActiveAndEnabled)
                    {
                        return;
                    }

                    if (sceneContext.CurrentMode.Value != EditModeType.Paint)
                    {
                        return;
                    }

                    painter.Redo();
                })
                .AddTo(this);

            // Paintモード時かつ UI 上でないときだけカーソルを表示する
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

            sceneContext.CurrentMode
                .Subscribe(mode =>
                {
                    if (mode != EditModeType.Paint)
                    {
                        colorPicker?.CancelEyedropper();
                    }
                })
                .AddTo(this);

            if (mainCamera != null && painter.RaycastAnchor != null)
            {
                raycaster.InitializeDepth(mainCamera, painter.RaycastAnchor.position);
            }
        }

        /// <inheritdoc />
        public void Tick()
        {
            if (!isActiveAndEnabled)
            {
                wasShiftPressed = false;
                EndStrokeIfNeeded();
                return;
            }

            if (mainCamera == null)
            {
                return;
            }

            // Paintモード以外では何もしない
            if (sceneContext.CurrentMode.Value != EditModeType.Paint)
            {
                wasShiftPressed = false;
                EndStrokeIfNeeded();
                return;
            }

            if (input.IsPointerOverUI)
            {
                wasShiftPressed = input.IsShiftPressed;
                EndStrokeIfNeeded();
                return;
            }

            // カーソル位置の更新(表示メッシュへのレイヒットを優先する)
            bool hasSurfaceHit = engine.TryRaycastSurface(mainCamera, input.PointerPosition, out RaycastHit surfaceHit);
            bool isShiftPressed = input.IsShiftPressed;

            // 何もないところで深度固定を開始したらEdit範囲中心の深度へ合わせる
            if (isShiftPressed && !wasShiftPressed && !hasSurfaceHit)
            {
                raycaster.InitializeDepth(mainCamera, painter.RaycastAnchor.position);
            }

            wasShiftPressed = isShiftPressed;

            Vector3 worldPos;
            if (hasSurfaceHit)
            {
                worldPos = surfaceHit.point;
                raycaster.RecordDepth(mainCamera, input.PointerPosition, surfaceHit.point);
            }
            else
            {
                worldPos = raycaster.Resolve(
                    mainCamera,
                    painter.RaycastAnchor,
                    input.PointerPosition,
                    raycastLayerMask,
                    lockDepth: isShiftPressed,
                    resetDepthOnMiss: true,
                    out _,
                    out _);
            }

            if (cursorObject != null)
            {
                cursorObject.transform.position = worldPos;
                float cursorScale = painter.BrushRadius * 2f / cursorMeshDiameter;
                cursorObject.transform.localScale = Vector3.one * cursorScale;
            }

            // Alt中(カメラ操作中)はペイントしない
            if (input.IsAltPressed)
            {
                EndStrokeIfNeeded();
                return;
            }

            if (suppressPaintUntilPrimaryRelease)
            {
                if (!input.IsPrimaryHeld)
                {
                    suppressPaintUntilPrimaryRelease = false;
                }

                EndStrokeIfNeeded();
                StopPaintSe();
                return;
            }

            if (colorPicker != null && colorPicker.IsEyedropperActive)
            {
                EndStrokeIfNeeded();
                StopPaintSe();
                return;
            }

            bool isPainting = false;
            bool isPaintingOnSurface = false;

            // ペイントはカメラ側の表面にヒットしたときのみ行う
            if (input.IsPrimaryHeld && hasSurfaceHit)
            {
                painter.PaintAtWorldPosition(surfaceHit.point, surfaceHit.normal);
                paintedInStroke = true;
                isPainting = true;
                isPaintingOnSurface = true;
            }
            else if (input.IsPrimaryHeld)
            {
                isPainting = true;
            }

            SyncPaintSe(isPaintingOnSurface);

            // 塗るのをやめた瞬間にストロークを確定する
            if (wasPainting && !isPainting)
            {
                CommitStroke();
            }

            wasPainting = isPainting;
        }

        // スポイト採取待ち中にヒット表面の色をピッカーへ反映する
        private void TryPickColorAtPointer()
        {
            if (mainCamera == null || colorPicker == null || engine == null)
            {
                return;
            }

            if (!engine.TryRaycastSurface(mainCamera, input.PointerPosition, out RaycastHit surfaceHit))
            {
                return;
            }

            if (!engine.TrySampleSurfaceDisplayColor(surfaceHit, out Color displayColor))
            {
                return;
            }

            // 採取クリックの押しっぱなしで直後に塗らない
            suppressPaintUntilPrimaryRelease = true;
            EndStrokeIfNeeded();
            StopPaintSe();

            colorPicker.ApplySampledColor(displayColor);
            ApplyCursorColor(displayColor);
            seService?.Play(SeTrackId.ClayEditSpoito);
        }

        // 塗っていた状態から外れたときにストロークを確定する
        private void EndStrokeIfNeeded()
        {
            if (wasPainting)
            {
                CommitStroke();
                wasPainting = false;
            }

            StopPaintSe();
        }

        // ストロークを確定し 実際に塗った場合のみ使用色を履歴へ追加する
        private void CommitStroke()
        {
            painter.EndStroke();
            engine.FlushPaintMesh(refreshCollider: true);

            if (paintedInStroke)
            {
                colorPicker.AddColorToHistory(painter.CurrentColor);
            }

            paintedInStroke = false;
            StopPaintSe();
        }

        private void SyncPaintSe(bool isPaintingOnSurface)
        {
            if (!isPaintingOnSurface || seService == null)
            {
                StopPaintSe();
                return;
            }

            if (!playedPaintIn)
            {
                seService.Play(SeTrackId.ClayEditPaintIn);
                playedPaintIn = true;
            }

            if (!TryResolvePaintMovePitch(out float pitch))
            {
                StopPaintLoop();
                return;
            }

            seService.PlayLoop(SeTrackId.ClayEditPaint, pitch);
            playingPaintSe = SeTrackId.ClayEditPaint;
        }

        private bool TryResolvePaintMovePitch(out float pitch)
        {
            pitch = MinPaintSePitch;
            Vector2 pointer = input.PointerPosition;
            if (!hasLastPaintPointer)
            {
                lastPaintPointer = pointer;
                hasLastPaintPointer = true;
                return false;
            }

            float deltaPixels = (pointer - lastPaintPointer).magnitude;
            lastPaintPointer = pointer;
            if (deltaPixels < MinPaintMovePixels)
            {
                return false;
            }

            float screenHeight = Mathf.Max(1f, Screen.height);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.008f);
            float speed = (deltaPixels / screenHeight) / dt;
            float t = Mathf.Clamp01(speed / SpeedForMaxPaintSePitch);
            pitch = Mathf.Lerp(MinPaintSePitch, MaxPaintSePitch, t);
            return true;
        }

        private void StopPaintLoop()
        {
            if (!playingPaintSe.HasValue)
            {
                return;
            }

            seService?.Stop(playingPaintSe.Value);
            playingPaintSe = null;
        }

        private void StopPaintSe()
        {
            StopPaintLoop();
            hasLastPaintPointer = false;
            playedPaintIn = false;
        }

        // Paint操作(ペイント 消し ブラシ変更)が有効な状態か
        private bool IsPaintActive()
        {
            if (!isActiveAndEnabled || input.IsAltPressed)
            {
                return false;
            }

            return sceneContext.CurrentMode.Value == EditModeType.Paint;
        }

        private void ApplyCursorColor(Color color)
        {
            if (cursorMaterial == null)
            {
                return;
            }

            if (cursorMaterial.HasProperty(BaseColorId))
            {
                Color materialColor = PaintColorUtility.ToMaterialColor(color);
                materialColor.a = cursorAlpha;
                cursorMaterial.SetColor(BaseColorId, materialColor);
            }
        }

        private void ApplyCursorTransparency()
        {
            if (cursorMaterial == null)
            {
                return;
            }

            cursorMaterial.SetFloat(SurfaceId, 1f);
            cursorMaterial.SetFloat(BlendId, 0f);
            cursorMaterial.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            cursorMaterial.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            cursorMaterial.SetFloat(ZWriteId, 0f);
            cursorMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            cursorMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            cursorMaterial.SetOverrideTag("RenderType", "Transparent");
        }

        private void CacheCursorMeshDiameter()
        {
            MeshFilter meshFilter = null;
            if (cursorMeshRenderer != null)
            {
                meshFilter = cursorMeshRenderer.GetComponent<MeshFilter>();
            }
            else if (cursorObject != null)
            {
                meshFilter = cursorObject.GetComponent<MeshFilter>();
            }

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
            cursorMeshDiameter = Mathf.Max(meshSize.x, Mathf.Max(meshSize.y, meshSize.z));
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