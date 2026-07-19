using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 指定した画角からモデルを撮影してPNGバイト列を生成する
    /// 編集画面と同じClayMonsterマテリアルと教室ライティングで撮影する
    /// </summary>
    public class ModelThumbnailCapturer : MonoBehaviour
    {
        private const string ThumbnailShaderName = "Custom/ClayMonsterThumbnail";
        private const int DefaultCaptureRendererIndex = 1;
        private const float BackdropViewPadding = 1.15f;
        private const float BackdropDepthPadding = 0.5f;
        private const float BoardClearKeyThreshold = 0.2f;
        private static readonly Color BoardClearKey = new Color(1f, 0f, 1f, 1f);

        [Header("撮影カメラ")]
        [Tooltip("サムネイル撮影専用のカメラ。GameObjectはアクティブのままCameraコンポーネントのみ無効にしておく")]
        [SerializeField] private Camera captureCamera;

        [Header("撮影設定")]
        [SerializeField] private bool useSourceMaterials = true;
        [SerializeField] private bool useScenePostProcess = true;
        [SerializeField] private bool renderShadowsDuringCapture = true;
        [SerializeField] private int captureRendererIndex = DefaultCaptureRendererIndex;
        [SerializeField] private Shader thumbnailShader;

        [Header("出力設定")]
        [SerializeField] private int textureSize = 256;
        [Tooltip("背景板の隙間を埋めるクリア色")]
        [SerializeField] private Color backgroundColor = Color.black;

        [Header("背景")]
        [Tooltip("撮影カメラの子として粘土板風の格子背景を置く")]
        [SerializeField] private bool includeBoardBackground = true;
        [SerializeField] private Color boardColor = new Color(0.08f, 0.28f, 0.14f, 1f);
        [SerializeField] private Color gridLineColor = new Color(0.92f, 0.95f, 0.90f, 1f);
        [SerializeField] private int gridDivisions = 8;
        [SerializeField] private int boardTextureSize = 128;
        [SerializeField] private int gridLineThickness = 2;

        [Header("画角")]
        [SerializeField] private Vector3 viewEulerAngles = new Vector3(15f, -150f, 0f);
        [SerializeField] private float fitMargin = 1.2f;
        [SerializeField] private bool orthographic = true;
        [SerializeField] private int captureLayer = 31;

        private readonly Dictionary<Transform, int> layerBackup = new Dictionary<Transform, int>();
        private Material runtimeFallbackMaterial;
        private Material runtimeBackdropMaterial;
        private Texture2D runtimeBoardTexture;
        private GameObject runtimeBackdrop;
        private Color cachedBoardColor;
        private Color cachedGridLineColor;
        private int cachedGridDivisions;
        private int cachedBoardTextureSize;
        private int cachedGridLineThickness;

        private void OnDestroy()
        {
            DestroyRuntimeBackdrop();
            DestroyRuntimeMaterial(ref runtimeFallbackMaterial);
            DestroyRuntimeMaterial(ref runtimeBackdropMaterial);
            DestroyRuntimeTexture(ref runtimeBoardTexture);
        }

        /// <summary>
        /// 対象モデルを指定画角で撮影しPNGのバイト列を返す。失敗時はnull
        /// </summary>
        public async UniTask<byte[]> CaptureToPngAsync(Renderer targetRenderer, CancellationToken cancellationToken)
        {
            if (captureCamera == null || targetRenderer == null)
            {
                Debug.LogWarning("[ModelThumbnailCapturer] カメラまたは対象が未設定のため撮影をスキップします");
                return null;
            }

            if (!captureCamera.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[ModelThumbnailCapturer] 撮影カメラのGameObjectが非アクティブです");
                return null;
            }

            int size = Mathf.Max(8, textureSize);

            var renderTarget = new RenderTexture(
                size,
                size,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 1
            };
            renderTarget.Create();

            RenderTexture originalTarget = captureCamera.targetTexture;
            bool originalEnabled = captureCamera.enabled;
            CameraClearFlags originalClear = captureCamera.clearFlags;
            Color originalBackground = captureCamera.backgroundColor;
            int originalCullingMask = captureCamera.cullingMask;
            bool originalOrthographic = captureCamera.orthographic;
            bool originalAllowHdr = captureCamera.allowHDR;

            UniversalAdditionalCameraData urpCameraData = captureCamera.GetComponent<UniversalAdditionalCameraData>();
            bool originalPostProcessing = false;
            bool originalRenderShadows = true;
            LayerMask originalVolumeLayerMask = default;
            int originalRendererIndex = -1;
            bool hasUrpCameraData = urpCameraData != null;
            if (hasUrpCameraData)
            {
                originalPostProcessing = urpCameraData.renderPostProcessing;
                originalRenderShadows = urpCameraData.renderShadows;
                originalVolumeLayerMask = urpCameraData.volumeLayerMask;
            }

            var skinned = targetRenderer as SkinnedMeshRenderer;
            bool originalUpdateWhenOffscreen = skinned != null && skinned.updateWhenOffscreen;

            Material[] originalMaterials = targetRenderer.sharedMaterials;
            Material[] swappedMaterials = null;
            bool materialsSwapped = false;
            bool rendererIndexChanged = false;
            if (!useSourceMaterials)
            {
                Material fallbackMaterial = GetFallbackThumbnailMaterial();
                swappedMaterials = CreateSwappedMaterials(originalMaterials, fallbackMaterial);
                materialsSwapped = swappedMaterials != null;
            }

            try
            {
                layerBackup.Clear();
                ApplyCaptureLayerRecursive(targetRenderer.transform, captureLayer);

                if (materialsSwapped)
                {
                    targetRenderer.sharedMaterials = swappedMaterials;
                }

                captureCamera.cullingMask = 1 << captureLayer;
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                // 市松合成用のキー色。背景板が欠けた隙間を後段で市松に置換する
                captureCamera.backgroundColor = includeBoardBackground
                    ? BoardClearKey
                    : backgroundColor;
                captureCamera.orthographic = orthographic;
                captureCamera.allowHDR = false;
                captureCamera.targetTexture = renderTarget;

                if (hasUrpCameraData)
                {
                    urpCameraData.renderPostProcessing = useScenePostProcess;
                    urpCameraData.renderShadows = renderShadowsDuringCapture;
                    urpCameraData.volumeLayerMask = useScenePostProcess ? (LayerMask)(-1) : 0;
                    if (useScenePostProcess && captureRendererIndex >= 0)
                    {
                        originalRendererIndex = -1;
                        urpCameraData.SetRenderer(captureRendererIndex);
                        rendererIndexChanged = true;
                    }
                }

                if (skinned != null)
                {
                    skinned.updateWhenOffscreen = true;
                }

                captureCamera.enabled = true;
                await UniTask.WaitForEndOfFrame(this, cancellationToken);

                Bounds captureBounds = ResolveCaptureBounds(targetRenderer);
                FrameModel(captureBounds);

                if (includeBoardBackground)
                {
                    CreateCameraAlignedBackdrop(captureBounds);
                }

                captureCamera.Render();

                if (useScenePostProcess)
                {
                    await UniTask.WaitForEndOfFrame(this, cancellationToken);
                    captureCamera.Render();
                }

                Texture2D texture = ReadTextureFromRenderTarget(renderTarget, size);
                if (includeBoardBackground)
                {
                    CompositeBoardOverClearKey(texture);
                }

                byte[] png = texture.EncodeToPNG();
                Destroy(texture);
                return png;
            }
            finally
            {
                if (materialsSwapped)
                {
                    targetRenderer.sharedMaterials = originalMaterials;
                }

                DestroyRuntimeBackdrop();
                RestoreCaptureLayers();

                captureCamera.targetTexture = originalTarget;
                captureCamera.enabled = originalEnabled;
                captureCamera.clearFlags = originalClear;
                captureCamera.backgroundColor = originalBackground;
                captureCamera.cullingMask = originalCullingMask;
                captureCamera.orthographic = originalOrthographic;
                captureCamera.allowHDR = originalAllowHdr;

                if (hasUrpCameraData)
                {
                    urpCameraData.renderPostProcessing = originalPostProcessing;
                    urpCameraData.renderShadows = originalRenderShadows;
                    urpCameraData.volumeLayerMask = originalVolumeLayerMask;
                    if (rendererIndexChanged)
                    {
                        urpCameraData.SetRenderer(originalRendererIndex);
                    }
                }

                if (skinned != null)
                {
                    skinned.updateWhenOffscreen = originalUpdateWhenOffscreen;
                }

                renderTarget.Release();
                Destroy(renderTarget);
            }
        }

        private void CreateCameraAlignedBackdrop(Bounds modelBounds)
        {
            DestroyRuntimeBackdrop();

            Vector3 cameraForward = captureCamera.transform.forward;
            float centerDistance = Vector3.Dot(
                modelBounds.center - captureCamera.transform.position,
                cameraForward);
            float halfExtentAlongView =
                (modelBounds.extents.x * Mathf.Abs(cameraForward.x))
                + (modelBounds.extents.y * Mathf.Abs(cameraForward.y))
                + (modelBounds.extents.z * Mathf.Abs(cameraForward.z));
            float backdropDistance = centerDistance + halfExtentAlongView + BackdropDepthPadding;
            backdropDistance = Mathf.Max(captureCamera.nearClipPlane + 0.05f, backdropDistance);

            if (backdropDistance >= captureCamera.farClipPlane - 0.05f)
            {
                captureCamera.farClipPlane = backdropDistance + 1f;
            }

            float height;
            if (captureCamera.orthographic)
            {
                height = captureCamera.orthographicSize * 2f * BackdropViewPadding;
            }
            else
            {
                float halfFov = captureCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                height = 2f * Mathf.Tan(halfFov) * backdropDistance * BackdropViewPadding;
            }

            float width = height;

            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "ThumbnailBoardBackdrop";
            Destroy(backdrop.GetComponent<Collider>());

            backdrop.layer = captureLayer;
            // Quadは+Z正面なので180度回してカメラ側を向ける
            backdrop.transform.SetParent(captureCamera.transform, false);
            backdrop.transform.localPosition = new Vector3(0f, 0f, backdropDistance);
            backdrop.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            backdrop.transform.localScale = new Vector3(width, height, 1f);

            MeshRenderer meshRenderer = backdrop.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetBackdropMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            runtimeBackdrop = backdrop;
        }

        private static Bounds ResolveCaptureBounds(Renderer targetRenderer)
        {
            Bounds bounds = targetRenderer.bounds;
            if (bounds.size.sqrMagnitude > 0.000001f)
            {
                return bounds;
            }

            Bounds localBounds = targetRenderer.localBounds;
            Vector3 worldCenter = targetRenderer.transform.TransformPoint(localBounds.center);
            Vector3 worldSize = Vector3.Scale(localBounds.size, Abs(targetRenderer.transform.lossyScale));
            return new Bounds(worldCenter, worldSize);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private Material GetFallbackThumbnailMaterial()
        {
            if (runtimeFallbackMaterial != null)
            {
                return runtimeFallbackMaterial;
            }

            Shader shader = thumbnailShader != null
                ? thumbnailShader
                : Shader.Find(ThumbnailShaderName);
            if (shader == null)
            {
                return null;
            }

            runtimeFallbackMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return runtimeFallbackMaterial;
        }

        private Material GetBackdropMaterial()
        {
            Texture2D boardTexture = GetOrCreateBoardTexture();
            if (runtimeBackdropMaterial != null)
            {
                ApplyUnlitTexture(runtimeBackdropMaterial, boardTexture);
                runtimeBackdropMaterial.renderQueue = (int)RenderQueue.Background;
                return runtimeBackdropMaterial;
            }

            runtimeBackdropMaterial = CreateUnlitTextureMaterial(boardTexture);
            if (runtimeBackdropMaterial != null)
            {
                runtimeBackdropMaterial.renderQueue = (int)RenderQueue.Background;
            }

            return runtimeBackdropMaterial;
        }

        private Texture2D GetOrCreateBoardTexture()
        {
            int divisions = Mathf.Max(2, gridDivisions);
            int size = Mathf.Max(32, boardTextureSize);
            int lineThickness = Mathf.Max(1, gridLineThickness);
            if (runtimeBoardTexture != null
                && cachedGridDivisions == divisions
                && cachedBoardTextureSize == size
                && cachedGridLineThickness == lineThickness
                && cachedBoardColor == boardColor
                && cachedGridLineColor == gridLineColor)
            {
                return runtimeBoardTexture;
            }

            DestroyRuntimeTexture(ref runtimeBoardTexture);
            runtimeBoardTexture = CreateClayBoardTexture(
                size,
                divisions,
                lineThickness,
                boardColor,
                gridLineColor);
            cachedGridDivisions = divisions;
            cachedBoardTextureSize = size;
            cachedGridLineThickness = lineThickness;
            cachedBoardColor = boardColor;
            cachedGridLineColor = gridLineColor;
            return runtimeBoardTexture;
        }

        private static Texture2D CreateClayBoardTexture(
            int size,
            int divisions,
            int lineThickness,
            Color board,
            Color line)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ThumbnailClayBoard"
            };

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = board;
            }

            float cell = size / (float)divisions;
            int halfLine = lineThickness / 2;

            for (int i = 0; i <= divisions; i++)
            {
                int center = Mathf.Clamp(Mathf.RoundToInt(i * cell), 0, size - 1);
                int lineStart = Mathf.Max(0, center - halfLine);
                int lineEnd = Mathf.Min(size - 1, center + halfLine + ((lineThickness + 1) % 2));

                for (int y = 0; y < size; y++)
                {
                    for (int x = lineStart; x <= lineEnd; x++)
                    {
                        pixels[(y * size) + x] = line;
                    }
                }

                for (int x = 0; x < size; x++)
                {
                    for (int y = lineStart; y <= lineEnd; y++)
                    {
                        pixels[(y * size) + x] = line;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Material CreateUnlitTextureMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            ApplyUnlitTexture(material, texture);
            return material;
        }

        private static void ApplyUnlitTexture(Material material, Texture2D texture)
        {
            if (material == null)
            {
                return;
            }

            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }

            material.color = Color.white;
        }

        private static Material[] CreateSwappedMaterials(Material[] sourceMaterials, Material captureMaterial)
        {
            if (sourceMaterials == null || sourceMaterials.Length == 0 || captureMaterial == null)
            {
                return null;
            }

            var swapped = new Material[sourceMaterials.Length];
            for (int i = 0; i < swapped.Length; i++)
            {
                swapped[i] = captureMaterial;
            }

            return swapped;
        }

        private void CompositeBoardOverClearKey(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            Texture2D boardTexture = GetOrCreateBoardTexture();
            if (boardTexture == null)
            {
                return;
            }

            int width = texture.width;
            int height = texture.height;
            int boardSize = boardTexture.width;
            Color[] pixels = texture.GetPixels();

            for (int y = 0; y < height; y++)
            {
                int boardY = (y * boardSize) / height;
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    if (!IsNearColor(pixels[index], BoardClearKey, BoardClearKeyThreshold))
                    {
                        continue;
                    }

                    int boardX = (x * boardSize) / width;
                    pixels[index] = boardTexture.GetPixel(boardX, boardY);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
        }

        private static bool IsNearColor(Color value, Color target, float threshold)
        {
            return Mathf.Abs(value.r - target.r) <= threshold
                && Mathf.Abs(value.g - target.g) <= threshold
                && Mathf.Abs(value.b - target.b) <= threshold;
        }

        private static Texture2D ReadTextureFromRenderTarget(RenderTexture source, int size)
        {
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = source;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            texture.Apply(false, false);
            RenderTexture.active = previousActive;

            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                ConvertLinearTextureToGamma(texture);
            }

            return texture;
        }

        private static void ConvertLinearTextureToGamma(Texture2D texture)
        {
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                pixel.r = Mathf.LinearToGammaSpace(pixel.r);
                pixel.g = Mathf.LinearToGammaSpace(pixel.g);
                pixel.b = Mathf.LinearToGammaSpace(pixel.b);
                pixels[i] = pixel;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
        }

        private void ApplyCaptureLayerRecursive(Transform target, int layer)
        {
            if (!layerBackup.ContainsKey(target))
            {
                layerBackup[target] = target.gameObject.layer;
            }

            target.gameObject.layer = layer;

            for (int i = 0; i < target.childCount; i++)
            {
                ApplyCaptureLayerRecursive(target.GetChild(i), layer);
            }
        }

        private void RestoreCaptureLayers()
        {
            foreach (KeyValuePair<Transform, int> pair in layerBackup)
            {
                if (pair.Key != null)
                {
                    pair.Key.gameObject.layer = pair.Value;
                }
            }

            layerBackup.Clear();
        }

        private void DestroyRuntimeBackdrop()
        {
            if (runtimeBackdrop == null)
            {
                return;
            }

            Destroy(runtimeBackdrop);
            runtimeBackdrop = null;
        }

        private static void DestroyRuntimeMaterial(ref Material material)
        {
            if (material == null)
            {
                return;
            }

            Destroy(material);
            material = null;
        }

        private static void DestroyRuntimeTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            Destroy(texture);
            texture = null;
        }

        private void FrameModel(Bounds bounds)
        {
            Quaternion rotation = Quaternion.Euler(viewEulerAngles);
            Vector3 direction = rotation * Vector3.forward;
            float radius = Mathf.Max(0.0001f, bounds.extents.magnitude);
            float margin = Mathf.Max(0.01f, fitMargin);

            captureCamera.transform.rotation = rotation;

            if (captureCamera.orthographic)
            {
                captureCamera.orthographicSize = radius * margin;
                float distance = radius * 2f + 1f;
                captureCamera.transform.position = bounds.center - direction * distance;
                captureCamera.nearClipPlane = 0.01f;
                captureCamera.farClipPlane = distance + radius * 2f + 1f;
            }
            else
            {
                float halfFov = captureCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float distance = (radius * margin) / Mathf.Sin(halfFov);
                captureCamera.transform.position = bounds.center - direction * distance;
                captureCamera.nearClipPlane = 0.01f;
                captureCamera.farClipPlane = distance + radius * 2f + 1f;
            }
        }
    }
}
