using ClayEditor.Interface;
using GameData;
using R3;
using UnityEngine;
using VContainer;

namespace ClayEditor
{
    /// <summary>
    /// ClayEditの造形範囲をグミシップエディタ風の格子キューブで表示する
    /// 造形・ペイント・モーション確認の各モードで表示する
    /// </summary>
    public class ClayEditorRangeVisualizer : MonoBehaviour
    {
        private static readonly int GridColorId = Shader.PropertyToID("_GridColor");
        private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
        private static readonly int DivisionsId = Shader.PropertyToID("_Divisions");
        private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
        private static readonly int DotSizeId = Shader.PropertyToID("_DotSize");
        private static readonly int HideCameraFacingFaceId = Shader.PropertyToID("_HideCameraFacingFace");
        private static readonly int ArrowFillColorId = Shader.PropertyToID("_ArrowFillColor");
        private static readonly int ArrowOutlineColorId = Shader.PropertyToID("_ArrowOutlineColor");
        private static readonly int ArrowOutlineWidthId = Shader.PropertyToID("_ArrowOutlineWidth");
        private static readonly int ShowFrontArrowsId = Shader.PropertyToID("_ShowFrontArrows");

        [Inject] private readonly IClaySceneContext sceneContext;
        [Inject] private readonly ClayVoxelEngine engine;

        [Header("Grid Style")]
        [ColorUsage(true, true)]
        [SerializeField] private Color gridColor = new(0.2f, 1.6f, 2.2f, 1f);
        [SerializeField] private Color faceTintColor = new(0.02f, 0.06f, 0.18f, 0.12f);
        [SerializeField] private int displayDivisions = 10;
        [SerializeField] private float lineWidth = 0.018f;
        [SerializeField] private float dotSize = 0.028f;
        [SerializeField] private bool hideCameraFacingFace = true;
        [SerializeField] private Color frontArrowFillColor = new(0.82f, 0.92f, 1f, 0.32f);
        [ColorUsage(true, true)]
        [SerializeField] private Color frontArrowOutlineColor = new(1f, 0.88f, 0.2f, 1f);
        [SerializeField] private float frontArrowOutlineWidth = 0.007f;
        [SerializeField] private bool showFrontArrows = true;

        private Transform gridRoot;
        private MeshRenderer gridRenderer;
        private Material runtimeMaterial;
        private MaterialPropertyBlock propertyBlock;
        private float currentBoundsSize;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            EnsureGridObject();
        }

        private void Start()
        {
            RefreshWireframe(engine.size, engine.Scale);

            sceneContext.CurrentMode
                .Subscribe(UpdateVisibility)
                .AddTo(this);

            UpdateVisibility(sceneContext.CurrentMode.Value);
        }

        /// <summary>
        /// ボクセル設定に合わせて造形範囲の格子を再構築する
        /// </summary>
        /// <param name="voxelCount">1辺のボクセル数</param>
        /// <param name="cellScale">1ボクセルのワールドスケール</param>
        public void RefreshWireframe(int voxelCount, float cellScale)
        {
            float boundsSize = voxelCount * cellScale;
            if (Mathf.Approximately(boundsSize, currentBoundsSize))
            {
                ApplyMaterialProperties();
                return;
            }

            currentBoundsSize = boundsSize;
            EnsureGridObject();

            gridRoot.localPosition = Vector3.zero;
            gridRoot.localRotation = Quaternion.identity;
            gridRoot.localScale = Vector3.one * boundsSize;

            ApplyMaterialProperties();
        }

        private void EnsureGridObject()
        {
            if (gridRoot != null)
            {
                return;
            }

            var gridObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gridObject.name = "ClayEditRangeGrid";
            gridRoot = gridObject.transform;
            gridRoot.SetParent(transform, false);

            if (gridObject.TryGetComponent(out Collider collider))
            {
                Destroy(collider);
            }

            gridRenderer = gridObject.GetComponent<MeshRenderer>();
            gridRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gridRenderer.receiveShadows = false;
            gridRenderer.sharedMaterial = ResolveMaterial();
        }

        private Material ResolveMaterial()
        {
            if (runtimeMaterial != null)
            {
                return runtimeMaterial;
            }

            Shader shader = Shader.Find("ClayEditor/ClayEditGrid");
            if (shader == null)
            {
                Debug.LogError("[ClayEditorRangeVisualizer] ClayEditGrid shader not found.");
                return null;
            }

            runtimeMaterial = new Material(shader);
            return runtimeMaterial;
        }

        private void ApplyMaterialProperties()
        {
            if (gridRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();

            propertyBlock.SetColor(GridColorId, gridColor);
            propertyBlock.SetColor(FaceColorId, faceTintColor);
            propertyBlock.SetFloat(DivisionsId, Mathf.Max(displayDivisions, 1));
            propertyBlock.SetFloat(LineWidthId, lineWidth);
            propertyBlock.SetFloat(DotSizeId, dotSize);
            propertyBlock.SetFloat(HideCameraFacingFaceId, hideCameraFacingFace ? 1f : 0f);
            propertyBlock.SetColor(ArrowFillColorId, frontArrowFillColor);
            propertyBlock.SetColor(ArrowOutlineColorId, frontArrowOutlineColor);
            propertyBlock.SetFloat(ArrowOutlineWidthId, frontArrowOutlineWidth);
            propertyBlock.SetFloat(ShowFrontArrowsId, showFrontArrows ? 1f : 0f);
            gridRenderer.SetPropertyBlock(propertyBlock);
        }

        private void UpdateVisibility(EditModeType mode)
        {
            if (gridRoot == null)
            {
                return;
            }

            bool visible = mode is EditModeType.Clay or EditModeType.Paint or EditModeType.Animation;
            gridRoot.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }
    }
}
