using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Scene.Rendering
{
    /// <summary>
    /// Fieldレイヤーの法線差から背景輪郭線を合成するURPポストプロセス
    /// ClayEdit以外のシーンで有効
    /// </summary>
    public sealed class BackgroundOutlineRendererFeature : ScriptableRendererFeature
    {
        private const string ResourcesSettingsName = "BackgroundOutlineSettings";

        [Serializable]
        public sealed class Settings
        {
            // 不透明描画後透明UI前に実行しCameraSpace UIの上に乗らないようにする
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            public LayerMask fieldLayerMask = 1 << 7;
            [Range(0f, 1f)] public float intensity = 0.85f;
            [Range(0.25f, 8f)] public float thickness = 1.1f;
            [FormerlySerializedAs("depthSensitivity")]
            [Range(0.1f, 40f)] public float normalSensitivity = 4f;
            public Color color = new Color(0.12f, 0.08f, 0.06f, 1f);
            [FormerlySerializedAs("fieldDepthShader")]
            public Shader fieldNormalsShader;
            public Shader transparentMaskShader;
            public Shader outlineShader;
        }

        [SerializeField] private BackgroundOutlineSettings sharedSettings;
        [SerializeField] private Settings settings = new Settings();

        private Material fieldNormalsMaterial;
        private Material transparentMaskMaterial;
        private Material outlineMaterial;
        private BackgroundOutlinePass outlinePass;

        /// <summary>
        /// 設定へのアクセス
        /// </summary>
        public Settings FeatureSettings => settings;

        /// <summary>
        /// 共有設定アセット
        /// </summary>
        public BackgroundOutlineSettings SharedSettings
        {
            get => sharedSettings;
            set => sharedSettings = value;
        }

        /// <inheritdoc/>
        public override void Create()
        {
            EnsureShaders();
            EnsureSharedSettings();

            if (fieldNormalsMaterial == null && settings.fieldNormalsShader != null)
            {
                fieldNormalsMaterial = CoreUtils.CreateEngineMaterial(settings.fieldNormalsShader);
            }

            if (transparentMaskMaterial == null && settings.transparentMaskShader != null)
            {
                transparentMaskMaterial = CoreUtils.CreateEngineMaterial(settings.transparentMaskShader);
            }

            if (outlineMaterial == null && settings.outlineShader != null)
            {
                outlineMaterial = CoreUtils.CreateEngineMaterial(settings.outlineShader);
            }

            if (fieldNormalsMaterial == null || transparentMaskMaterial == null || outlineMaterial == null)
            {
                outlinePass = null;
                return;
            }

            outlinePass = new BackgroundOutlinePass(
                this,
                fieldNormalsMaterial,
                transparentMaskMaterial,
                outlineMaterial)
            {
                renderPassEvent = settings.renderPassEvent
            };
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!BackgroundOutlineActivation.IsEnabled())
            {
                return;
            }

            if (outlinePass == null
                || fieldNormalsMaterial == null
                || transparentMaskMaterial == null
                || outlineMaterial == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
            {
                return;
            }

            // Overlayカメラ(UIスタック等)には掛けない
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
            {
                return;
            }

            EnsureSharedSettings();
            outlinePass.Setup();
            renderer.EnqueuePass(outlinePass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            outlinePass = null;
            CoreUtils.Destroy(fieldNormalsMaterial);
            CoreUtils.Destroy(transparentMaskMaterial);
            CoreUtils.Destroy(outlineMaterial);
            fieldNormalsMaterial = null;
            transparentMaskMaterial = null;
            outlineMaterial = null;
        }

        private void EnsureShaders()
        {
            if (settings.fieldNormalsShader == null)
            {
                settings.fieldNormalsShader = Shader.Find("Hidden/ClayMonsters/FieldDepthOnly");
            }

            if (settings.transparentMaskShader == null)
            {
                settings.transparentMaskShader = Shader.Find("Hidden/ClayMonsters/TransparentCoverageMask");
            }

            if (settings.outlineShader == null)
            {
                settings.outlineShader = Shader.Find("Hidden/ClayMonsters/BackgroundOutline");
            }
        }

        private void EnsureSharedSettings()
        {
            if (sharedSettings == null)
            {
                sharedSettings = Resources.Load<BackgroundOutlineSettings>(ResourcesSettingsName);
            }
        }

        internal BackgroundOutlineSettings ResolveSettings()
        {
            EnsureSharedSettings();
            return sharedSettings;
        }

        internal Settings ResolveFallbackSettings()
        {
            return settings;
        }

        internal LayerMask ResolveFieldLayerMask()
        {
            return settings.fieldLayerMask;
        }

        internal LayerMask ResolveTransparentMaskLayers()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            LayerMask mask = ~0;
            if (uiLayer >= 0)
            {
                mask &= ~(1 << uiLayer);
            }

            return mask;
        }

        private sealed class BackgroundOutlinePass : ScriptableRenderPass
        {
            private static readonly int FieldNormalTextureId = Shader.PropertyToID("_FieldNormalTexture");
            private static readonly int TransparentMaskTextureId = Shader.PropertyToID("_TransparentMaskTexture");
            private static readonly int OutlineIntensityId = Shader.PropertyToID("_OutlineIntensity");
            private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
            private static readonly int NormalSensitivityId = Shader.PropertyToID("_NormalSensitivity");
            private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            private readonly BackgroundOutlineRendererFeature owner;
            private readonly Material fieldNormalsMaterial;
            private readonly Material transparentMaskMaterial;
            private readonly Material outlineMaterial;

            public BackgroundOutlinePass(
                BackgroundOutlineRendererFeature owner,
                Material fieldNormalsMaterial,
                Material transparentMaskMaterial,
                Material outlineMaterial)
            {
                this.owner = owner;
                this.fieldNormalsMaterial = fieldNormalsMaterial;
                this.transparentMaskMaterial = transparentMaskMaterial;
                this.outlineMaterial = outlineMaterial;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup()
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!BackgroundOutlineActivation.IsEnabled())
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                BackgroundOutlineSettings shared = owner.ResolveSettings();
                Settings fallback = owner.ResolveFallbackSettings();
                float intensity = shared != null ? shared.Intensity : fallback.intensity;
                float thickness = shared != null ? shared.Thickness : fallback.thickness;
                float normalSensitivity = shared != null ? shared.NormalSensitivity : fallback.normalSensitivity;
                Color outlineColor = shared != null ? shared.Color : fallback.color;

                if (intensity <= 0.001f)
                {
                    return;
                }

                TextureHandle cameraColor = resourceData.activeColorTexture;
                if (!cameraColor.IsValid())
                {
                    return;
                }

                TextureDesc fieldNormalDesc = cameraColor.GetDescriptor(renderGraph);
                fieldNormalDesc.name = "_FieldOutlineNormals";
                fieldNormalDesc.depthBufferBits = 0;
                // 中間RTはMSAA不要カメラカラーとは別扱い
                fieldNormalDesc.msaaSamples = MSAASamples.None;
                fieldNormalDesc.format = GraphicsFormat.R16G16B16A16_SFloat;
                fieldNormalDesc.clearBuffer = true;
                fieldNormalDesc.clearColor = Color.clear;
                TextureHandle fieldNormals = renderGraph.CreateTexture(fieldNormalDesc);

                TextureDesc depthDesc = fieldNormalDesc;
                depthDesc.name = "_FieldOutlineDepthBuffer";
                depthDesc.format = GraphicsFormat.None;
                depthDesc.depthBufferBits = DepthBits.Depth32;
                depthDesc.clearBuffer = true;
                TextureHandle fieldDepthBuffer = renderGraph.CreateTexture(depthDesc);

                TextureDesc transparentMaskDesc = fieldNormalDesc;
                transparentMaskDesc.name = "_TransparentOutlineMask";
                transparentMaskDesc.format = GraphicsFormat.R8_UNorm;
                transparentMaskDesc.clearColor = Color.black;
                TextureHandle transparentMask = renderGraph.CreateTexture(transparentMaskDesc);

                DrawFieldNormals(
                    renderGraph,
                    renderingData,
                    cameraData,
                    lightData,
                    fieldNormals,
                    fieldDepthBuffer,
                    owner.ResolveFieldLayerMask());

                DrawTransparentMask(
                    renderGraph,
                    renderingData,
                    cameraData,
                    lightData,
                    transparentMask,
                    owner.ResolveTransparentMaskLayers());

                outlineMaterial.SetFloat(OutlineIntensityId, intensity);
                outlineMaterial.SetFloat(OutlineThicknessId, thickness);
                outlineMaterial.SetFloat(NormalSensitivityId, normalSensitivity);
                outlineMaterial.SetColor(OutlineColorId, outlineColor);

                // 後段パスとカメラカラーのMSAAを一致させる(Noneに落とさない)
                TextureDesc destinationDesc = cameraColor.GetDescriptor(renderGraph);
                destinationDesc.name = "CameraColor-BackgroundOutline";
                destinationDesc.depthBufferBits = 0;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                RenderGraphUtils.BlitMaterialParameters blitParams =
                    new RenderGraphUtils.BlitMaterialParameters(cameraColor, destination, outlineMaterial, 0);
                renderGraph.AddBlitPass(blitParams, "Background Outline");
                resourceData.cameraColor = destination;
            }

            private void DrawFieldNormals(
                RenderGraph renderGraph,
                UniversalRenderingData renderingData,
                UniversalCameraData cameraData,
                UniversalLightData lightData,
                TextureHandle fieldNormalColor,
                TextureHandle fieldDepthBuffer,
                LayerMask fieldLayerMask)
            {
                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                    ShaderTags,
                    renderingData,
                    cameraData,
                    lightData,
                    cameraData.defaultOpaqueSortFlags);
                drawingSettings.overrideMaterial = fieldNormalsMaterial;
                drawingSettings.overrideMaterialPassIndex = 0;

                FilteringSettings filteringSettings = new FilteringSettings(
                    RenderQueueRange.opaque,
                    fieldLayerMask);

                RendererListParams rendererListParams = new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    filteringSettings);
                RendererListHandle rendererListHandle = renderGraph.CreateRendererList(rendererListParams);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<FieldNormalsPassData>(
                           "Field Normals For Outline",
                           out FieldNormalsPassData passData))
                {
                    passData.RendererListHandle = rendererListHandle;
                    builder.UseRendererList(rendererListHandle);
                    builder.SetRenderAttachment(fieldNormalColor, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(fieldDepthBuffer, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(fieldNormalColor, FieldNormalTextureId);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (FieldNormalsPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(RTClearFlags.Color | RTClearFlags.Depth, Color.clear, 1f, 0);
                        context.cmd.DrawRendererList(data.RendererListHandle);
                    });
                }
            }

            private void DrawTransparentMask(
                RenderGraph renderGraph,
                UniversalRenderingData renderingData,
                UniversalCameraData cameraData,
                UniversalLightData lightData,
                TextureHandle transparentMask,
                LayerMask transparentLayers)
            {
                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                    ShaderTags,
                    renderingData,
                    cameraData,
                    lightData,
                    SortingCriteria.CommonTransparent);
                drawingSettings.overrideMaterial = transparentMaskMaterial;
                drawingSettings.overrideMaterialPassIndex = 0;

                FilteringSettings filteringSettings = new FilteringSettings(
                    RenderQueueRange.transparent,
                    transparentLayers);

                RendererListParams rendererListParams = new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    filteringSettings);
                RendererListHandle rendererListHandle = renderGraph.CreateRendererList(rendererListParams);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<TransparentMaskPassData>(
                           "Transparent Mask For Outline",
                           out TransparentMaskPassData passData))
                {
                    passData.RendererListHandle = rendererListHandle;
                    builder.UseRendererList(rendererListHandle);
                    builder.SetRenderAttachment(transparentMask, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(transparentMask, TransparentMaskTextureId);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (TransparentMaskPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(RTClearFlags.Color, Color.black, 1f, 0);
                        context.cmd.DrawRendererList(data.RendererListHandle);
                    });
                }
            }

            private sealed class FieldNormalsPassData
            {
                public RendererListHandle RendererListHandle;
            }

            private sealed class TransparentMaskPassData
            {
                public RendererListHandle RendererListHandle;
            }
        }
    }
}
