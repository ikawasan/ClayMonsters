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
        [SerializeField] private Color backgroundColor = new Color(0.55f, 0.53f, 0.50f, 1f);

        [Header("画角")]
        [SerializeField] private Vector3 viewEulerAngles = new Vector3(15f, -150f, 0f);
        [SerializeField] private float fitMargin = 1.2f;
        [SerializeField] private bool orthographic = true;
        [SerializeField] private int captureLayer = 31;

        private readonly Dictionary<Transform, int> layerBackup = new Dictionary<Transform, int>();
        private Material runtimeFallbackMaterial;

        private void OnDestroy()
        {
            if (runtimeFallbackMaterial != null)
            {
                Destroy(runtimeFallbackMaterial);
                runtimeFallbackMaterial = null;
            }
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
                SetCaptureLayerRecursive(targetRenderer.transform, captureLayer);

                if (materialsSwapped)
                {
                    targetRenderer.sharedMaterials = swappedMaterials;
                }

                captureCamera.cullingMask = 1 << captureLayer;
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = backgroundColor;
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

                FrameModel(targetRenderer.bounds);
                captureCamera.Render();

                if (useScenePostProcess)
                {
                    await UniTask.WaitForEndOfFrame(this, cancellationToken);
                    captureCamera.Render();
                }

                Texture2D texture = ReadTextureFromRenderTarget(renderTarget, size);
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
                        urpCameraData.SetRenderer(-1);
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

        private void SetCaptureLayerRecursive(Transform root, int layer)
        {
            layerBackup.Clear();
            SetCaptureLayerRecursiveInternal(root, layer);
        }

        private void SetCaptureLayerRecursiveInternal(Transform target, int layer)
        {
            if (!layerBackup.ContainsKey(target))
            {
                layerBackup[target] = target.gameObject.layer;
            }

            target.gameObject.layer = layer;

            for (int i = 0; i < target.childCount; i++)
            {
                SetCaptureLayerRecursiveInternal(target.GetChild(i), layer);
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
