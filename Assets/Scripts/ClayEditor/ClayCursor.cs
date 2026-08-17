using Audio;
using Audio.Interface;
using ClayEditor.Input.Interface;
using ClayEditor.Interface;
using GameData;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ClayEditor
{
    public class ClayCursor : MonoBehaviour, ITickable
    {
        [Inject] private readonly ClayEditor editor;
        [Inject] private readonly IClayInputProvider input;
        [Inject] private readonly IClaySceneContext sceneContext;
        [Inject] private readonly ISeService seService;

        [SerializeField] private GameObject cursorObject;
        [SerializeField] private MeshRenderer cursorMeshRenderer;
        [SerializeField] private LayerMask raycastLayerMask = -1;
        [SerializeField] private float brushSizeChangeSpeed = 0.1f;
        [SerializeField] [Range(0.05f, 1f)] private float cursorAlpha = 0.4f;

        private UnityEngine.Camera mainCamera;
        private readonly CursorRaycaster raycaster = new();
        private float cursorMeshDiameter = 1f;
        private const float MinSculptSePitch = 0.05f;
        private const float MaxSculptSePitch = 0.5f;

        // 直前フレームに造形していたか(ストローク終了の検知に使う)
        private bool wasModifying;
        private SeTrackId? playingSculptSe;

        void Awake()
        {
            ApplyCursorTransparency();
        }

        void Start()
        {
            mainCamera = UnityEngine.Camera.main;
            if (cursorObject == null)
            {
                cursorObject = gameObject;
            }

            CacheCursorMeshDiameter();

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
                    // 非表示中 / Alt中（カメラ操作中）/ Clayモード以外ではブラシサイズを変更しない
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

        void OnDisable()
        {
            StopSculptSe();
        }

        /// <inheritdoc />
        public void Tick()
        {
            // 非表示中（モード切替で GameObject が非アクティブ時）では何もしない
            if (!isActiveAndEnabled)
            {
                FlushIfStrokeEnded();
                return;
            }

            // Clay モード以外では造形しない
            if (sceneContext.CurrentMode.Value != EditModeType.Clay)
            {
                FlushIfStrokeEnded();
                return;
            }

            if (mainCamera == null)
            {
                FlushIfStrokeEnded();
                return;
            }

            if (input.IsPointerOverUI)
            {
                FlushIfStrokeEnded();
                return;
            }

            // カーソル位置の更新
            Vector3 worldPos = raycaster.Resolve(
                mainCamera,
                editor.RaycastAnchor,
                input.PointerPosition,
                raycastLayerMask,
                lockDepth: input.IsShiftPressed,
                resetDepthOnMiss: true);

            if (cursorObject != null)
            {
                cursorObject.transform.position = worldPos;
                // 見た目の半径がBrushRadiusと一致するようメッシュ直径で正規化する
                float cursorScale = editor.BrushRadius * 2f / cursorMeshDiameter;
                cursorObject.transform.localScale = Vector3.one * cursorScale;
            }

            // Alt中（カメラ操作中）は造形しない
            if (input.IsAltPressed)
            {
                FlushIfStrokeEnded();
                return;
            }

            bool isModifying = input.IsPrimaryHeld || input.IsSecondaryHeld;

            // 左ドラッグ:Ctrlで削りそれ以外は盛る
            if (input.IsPrimaryHeld)
            {
                editor.ModifyAtWorldPosition(worldPos, input.IsCtrlPressed);
            }

            // 右ドラッグ:常に削る
            if (input.IsSecondaryHeld)
            {
                editor.ModifyAtWorldPosition(worldPos, true);
            }

            // 造形をやめた瞬間に間引きで未反映の最終形状を反映する
            if (wasModifying && !isModifying)
            {
                editor.FlushShape();
                StopSculptSe();
            }
            else
            {
                SyncSculptSe(isModifying);
            }

            wasModifying = isModifying;
        }

        // 造形中だった状態から外れたときに最終形状を反映する
        private void FlushIfStrokeEnded()
        {
            if (wasModifying)
            {
                editor.FlushShape();
                wasModifying = false;
            }

            StopSculptSe();
        }

        private void SyncSculptSe(bool isSculpting)
        {
            if (!isSculpting || seService == null)
            {
                StopSculptSe();
                return;
            }

            seService.PlayLoop(SeTrackId.ClayEditGenerate, ResolveSculptSePitch());
            playingSculptSe = SeTrackId.ClayEditGenerate;
        }

        private float ResolveSculptSePitch()
        {
            int maxVertices = Mathf.Max(1, editor.DenseSculptVertexThreshold);
            float t = Mathf.Clamp01(editor.CachedVertexCount / (float)maxVertices);
            return Mathf.Lerp(MinSculptSePitch, MaxSculptSePitch, t);
        }

        private void StopSculptSe()
        {
            if (!playingSculptSe.HasValue)
            {
                return;
            }

            seService?.Stop(playingSculptSe.Value);
            playingSculptSe = null;
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

        private void ApplyCursorTransparency()
        {
            if (cursorMeshRenderer == null)
            {
                return;
            }

            Material material = cursorMeshRenderer.material;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            Color baseColor = material.GetColor("_BaseColor");
            baseColor.a = cursorAlpha;
            material.SetColor("_BaseColor", baseColor);
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
            if (cursorMeshDiameter < 1e-4f)
            {
                cursorMeshDiameter = 1f;
            }
        }

        // 非表示中では操作を無視
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
