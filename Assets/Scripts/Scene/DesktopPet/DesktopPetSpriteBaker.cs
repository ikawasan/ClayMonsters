using System.Collections.Generic;
using System.IO;
using System.Threading;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData;
using SaveData.Interface;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scene.DesktopPet
{
    /// <summary>
    /// 未育成モデルから簡易2Dスプライトを焼き出す
    /// </summary>
    public sealed class DesktopPetSpriteBaker
    {
        private const int BakeSize = 160;
        private const int BakeLayer = 30;
        private const int IdleFrameCount = 6;
        private const int WalkFrameCount = 8;
        private const float WalkBakeLocomotionScale = 2.25f;
        private const int AttackFrameCount = 8;
        private const float TransparentAlphaThreshold = 0.08f;
        private const float NearBlackClearThreshold = 0.04f;
        private const float BakeBrightness = 1.12f;
        private const float OutlineDarken = 0.78f;
        private const byte SolidAlpha = 230;

        private static readonly float[] FacingYAngles = { 45f, -45f, 0f };

        private readonly IClayModelImporter importer;
        private readonly IClayModelSaveService saveService;
        private readonly LoadedModelConfigurator configurator;
        private readonly SkeletonPartAnalyzer fallbackPartAnalyzer;
        private readonly Material fallbackClayMaterial;

        /// <summary>
        /// 依存を受け取る
        /// </summary>
        public DesktopPetSpriteBaker(
            IClayModelImporter importer,
            IClayModelSaveService saveService,
            LoadedModelConfigurator configurator)
        {
            this.importer = importer;
            this.saveService = saveService;
            this.configurator = configurator;
            fallbackPartAnalyzer = configurator != null ? configurator.PartAnalyzer : null;
            fallbackClayMaterial = null;
        }

        /// <summary>
        /// LoadedModelConfigurator無しで焼き出す依存を受け取る
        /// </summary>
        public DesktopPetSpriteBaker(
            IClayModelImporter importer,
            IClayModelSaveService saveService,
            SkeletonPartAnalyzer partAnalyzer,
            Material clayMaterial)
        {
            this.importer = importer;
            this.saveService = saveService;
            configurator = null;
            fallbackPartAnalyzer = partAnalyzer;
            fallbackClayMaterial = clayMaterial;
        }

        /// <summary>
        /// キャッシュ優先でシートを用意する
        /// </summary>
        public async UniTask<DesktopPetSpriteSheet> LoadOrBakeAsync(
            int playerSlotIndex,
            CancellationToken cancellationToken)
        {
            ModelSaveSlot slot = saveService != null
                ? saveService.GetSlot(ModelSavePool.Player, playerSlotIndex)
                : null;
            string glbFileName = slot != null ? slot.glbFileName : null;
            DesktopPetSpriteSheet cached = null;
            if (!string.IsNullOrEmpty(glbFileName)
                && DesktopPetSpriteCache.TryLoad(playerSlotIndex, glbFileName, out cached)
                && cached.GetFrameCount(DesktopPetFacing.AnglePos45, DesktopPetAction.Idle) > 0
                && cached.GetFrameCount(DesktopPetFacing.Front, DesktopPetAction.Idle) > 0
                && cached.GetFrameCount(DesktopPetFacing.Front, DesktopPetAction.Walk) > 0
                && HasVisibleContent(cached))
            {
                Debug.Log("[DesktopPetSpriteBaker] ディスクキャッシュを使用します slot=" + playerSlotIndex);
                DesktopPetSpriteCache.WriteActiveMarker(DesktopPetSpriteCache.GetSlotDirectory(playerSlotIndex));
                return cached;
            }

            if (cached != null)
            {
                cached.Dispose();
                cached = null;
            }

            DesktopPetSpriteSheet sheet = await BakeAsync(playerSlotIndex, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return sheet;
            }

            if (!string.IsNullOrEmpty(glbFileName)
                && sheet.GetFrameCount(DesktopPetFacing.AnglePos45, DesktopPetAction.Idle) > 0
                && sheet.GetFrameCount(DesktopPetFacing.Front, DesktopPetAction.Idle) > 0
                && sheet.GetFrameCount(DesktopPetFacing.Front, DesktopPetAction.Walk) > 0
                && HasVisibleContent(sheet)
                && DesktopPetSpriteCache.TrySave(playerSlotIndex, glbFileName, sheet))
            {
                Debug.Log("[DesktopPetSpriteBaker] スプライトをキャッシュ保存しました slot=" + playerSlotIndex);
            }
            else if (!string.IsNullOrEmpty(glbFileName)
                     && sheet.GetFrameCount(DesktopPetFacing.AnglePos45, DesktopPetAction.Idle) > 0
                     && !HasVisibleContent(sheet))
            {
                Debug.LogError(
                    "[DesktopPetSpriteBaker] 焼き出し結果がほぼ透明なためキャッシュ保存をスキップします slot="
                    + playerSlotIndex);
            }

            return sheet;
        }

        /// <summary>
        /// 指定スロットのスプライトシートを焼き出す
        /// </summary>
        public async UniTask<DesktopPetSpriteSheet> BakeAsync(
            int playerSlotIndex,
            CancellationToken cancellationToken)
        {
            DesktopPetSpriteSheet sheet = new DesktopPetSpriteSheet();
            if (importer == null
                || saveService == null
                || (configurator == null && fallbackPartAnalyzer == null))
            {
                Debug.LogError("[DesktopPetSpriteBaker] 依存が不足しているためサムネ代替になります");
                return sheet;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, playerSlotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                Debug.LogError($"[DesktopPetSpriteBaker] スロット{playerSlotIndex}にモデルがありません");
                return sheet;
            }

            string filePath = ModelSaveStorage.ResolveReadPath(slot.glbFileName);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[DesktopPetSpriteBaker] glbがありません: {filePath}");
                return sheet;
            }

            GameObject bakeRoot = new GameObject("DesktopPetBakeRoot");
            bakeRoot.transform.position = new Vector3(0f, -500f, 0f);
            UnityEngine.Camera bakeCamera = null;
            RenderTexture renderTexture = null;
            GameObject imported = null;

            try
            {
                imported = await importer.ImportFromGlbAsync(
                    filePath,
                    bakeRoot.transform,
                    cancellationToken);
                if (imported == null)
                {
                    Debug.LogError("[DesktopPetSpriteBaker] モデル読み込みに失敗しました");
                    return sheet;
                }

                LoadedModelConfigurator.Result configured = configurator != null
                    ? configurator.Configure(imported)
                    : DesktopPetBakeModelSetup.Configure(
                        imported,
                        fallbackPartAnalyzer,
                        fallbackClayMaterial);
                ProceduralMotionCharacter motion = configured.Motion;
                if (motion == null)
                {
                    Debug.LogError("[DesktopPetSpriteBaker] モーション初期化に失敗しました");
                    return sheet;
                }

                SkeletonPartAnalyzer attackAnalyzer = configurator != null
                    ? configurator.PartAnalyzer
                    : fallbackPartAnalyzer;

                motion.SetRootTranslationEnabled(false);
                Transform modelTransform = imported.transform;
                modelTransform.localPosition = Vector3.zero;
                modelTransform.localRotation = Quaternion.identity;
                ApplyBakeLayerRecursive(bakeRoot.transform);
                PrepareBakeSkin(imported);
                motion.SetBakeSampling(true);

                // スキンとマテリアル反映を1フレーム待つ
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return sheet;
                }

                Bounds bounds = ResolveBounds(imported);
                AmbientMode previousAmbientMode = RenderSettings.ambientMode;
                Color previousAmbientLight = RenderSettings.ambientLight;
                Light bakeKeyLight = null;
                Light bakeFillLight = null;
                List<Light> disabledSceneLights = null;
                try
                {
                    disabledSceneLights = DisableSceneLightsExcept(bakeRoot.transform);
                    bakeKeyLight = CreateBakeLight(
                        bakeRoot.transform,
                        "DesktopPetBakeKeyLight",
                        new Vector3(35f, -25f, 0f),
                        1.15f);
                    bakeFillLight = CreateBakeLight(
                        bakeRoot.transform,
                        "DesktopPetBakeFillLight",
                        new Vector3(15f, 140f, 0f),
                        0.45f);
                    DesktopPetBakeModelSetup.EnsureUrpLight(bakeKeyLight);
                    DesktopPetBakeModelSetup.EnsureUrpLight(bakeFillLight);
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.65f, 1f);

                    bakeCamera = CreateBakeCamera(bakeRoot.transform, bounds);
                    DesktopPetBakeModelSetup.EnsureUrpCamera(bakeCamera);
                    renderTexture = new RenderTexture(BakeSize, BakeSize, 16, RenderTextureFormat.ARGB32);
                    renderTexture.Create();
                    bakeCamera.targetTexture = renderTexture;

                    MotionType walkMotion = ClayEditMotionPreview.ResolveRunMotion(motion);
                    MotionType attackMotion = ResolveAttackMotion(attackAnalyzer, configured.Renderer);

                    for (int facingIndex = 0; facingIndex < FacingYAngles.Length; facingIndex++)
                    {
                        DesktopPetFacing facing = (DesktopPetFacing)facingIndex;
                        modelTransform.localRotation = Quaternion.Euler(0f, FacingYAngles[facingIndex], 0f);

                        sheet.SetClip(
                            facing,
                            DesktopPetAction.Idle,
                            await CaptureClipAsync(
                                bakeCamera,
                                renderTexture,
                                motion,
                                MotionType.Idle,
                                IdleFrameCount,
                                cancellationToken));

                        motion.SetBakeLocomotionScale(WalkBakeLocomotionScale);
                        sheet.SetClip(
                            facing,
                            DesktopPetAction.Walk,
                            await CaptureClipAsync(
                                bakeCamera,
                                renderTexture,
                                motion,
                                walkMotion,
                                WalkFrameCount,
                                cancellationToken));
                        motion.SetBakeLocomotionScale(1f);

                        if (facing == DesktopPetFacing.Front)
                        {
                            continue;
                        }

                        sheet.SetClip(
                            facing,
                            DesktopPetAction.Attack,
                            await CaptureClipAsync(
                                bakeCamera,
                                renderTexture,
                                motion,
                                attackMotion,
                                AttackFrameCount,
                                cancellationToken));
                    }

                    motion.Play(MotionType.None);
                }
                finally
                {
                    motion.SetBakeSampling(false);
                    motion.SetBakeLocomotionScale(1f);
                    RenderSettings.ambientMode = previousAmbientMode;
                    RenderSettings.ambientLight = previousAmbientLight;
                    RestoreSceneLights(disabledSceneLights);
                    if (bakeKeyLight != null)
                    {
                        UnityEngine.Object.Destroy(bakeKeyLight.gameObject);
                    }

                    if (bakeFillLight != null)
                    {
                        UnityEngine.Object.Destroy(bakeFillLight.gameObject);
                    }
                }
            }
            finally
            {
                if (bakeCamera != null)
                {
                    bakeCamera.targetTexture = null;
                }

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Object.Destroy(renderTexture);
                }

                if (bakeRoot != null)
                {
                    Object.Destroy(bakeRoot);
                }
            }

            return sheet;
        }

        private static MotionType ResolveAttackMotion(
            SkeletonPartAnalyzer analyzer,
            SkinnedMeshRenderer renderer)
        {
            Transform[] bones = renderer != null ? renderer.bones : null;
            MotionType? attack = ClayEditMotionPreview.ResolveGenericAttack(analyzer, bones);
            return attack ?? MotionType.Tackle;
        }

        /// <summary>
        /// 焼き出し結果に十分な不透明画素があるか
        /// </summary>
        public static bool HasVisibleContent(DesktopPetSpriteSheet sheet)
        {
            if (sheet == null)
            {
                return false;
            }

            const int MinOpaquePixels = 200;
            Sprite frame = sheet.GetFrame(DesktopPetFacing.AnglePos45, DesktopPetAction.Idle, 0);
            if (frame == null || frame.texture == null)
            {
                return false;
            }

            Texture2D texture = frame.texture;
            Color32[] pixels = texture.GetPixels32();
            int opaque = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 20)
                {
                    opaque++;
                    if (opaque >= MinOpaquePixels)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void PrepareBakeSkin(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            SkinnedMeshRenderer[] skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i] == null)
                {
                    continue;
                }

                skins[i].updateWhenOffscreen = true;
                skins[i].forceRenderingOff = false;
            }

            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                {
                    continue;
                }

                animators[i].applyRootMotion = false;
                animators[i].enabled = false;
            }
        }

        private async UniTask<Sprite[]> CaptureClipAsync(
            UnityEngine.Camera bakeCamera,
            RenderTexture renderTexture,
            ProceduralMotionCharacter motion,
            MotionType motionType,
            int frameCount,
            CancellationToken cancellationToken)
        {
            Sprite[] sprites = new Sprite[Mathf.Max(1, frameCount)];
            motion.Play(MotionType.None);
            motion.Play(motionType);
            float clipSeconds = ResolveLoopClipSeconds(motionType);
            float stepSeconds = clipSeconds / sprites.Length;
            // 0度スイングの静止ポーズを避け半ステップから1周期を撮る
            motion.SampleForBake(stepSeconds * 0.5f);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return sprites;
                }

                if (i > 0)
                {
                    motion.SampleForBake(stepSeconds);
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return sprites;
                    }
                }

                Texture2D frame = CaptureFrame(bakeCamera, renderTexture);
                sprites[i] = Sprite.Create(
                    frame,
                    new Rect(0f, 0f, frame.width, frame.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            return sprites;
        }

        /// <summary>
        /// ループ再生向けにモーション1周期の秒数を返す
        /// </summary>
        private static float ResolveLoopClipSeconds(MotionType motionType)
        {
            const float TwoPi = Mathf.PI * 2f;
            switch (motionType)
            {
                case MotionType.Idle:
                    // ProceduralMotionSettings.idleFrequency既定1.5
                    return TwoPi / 1.5f;
                case MotionType.Run:
                case MotionType.LegRun:
                    // runFrequency / legRunFrequency既定8
                    return TwoPi / 8f;
                default:
                    // AttackDuration既定0.72(ワンショット)
                    return 0.72f;
            }
        }

        private static Texture2D CaptureFrame(UnityEngine.Camera bakeCamera, RenderTexture renderTexture)
        {
            RenderTexture previous = RenderTexture.active;
            bakeCamera.Render();
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(BakeSize, BakeSize, TextureFormat.RGBA32, false, false);
            texture.ReadPixels(new Rect(0f, 0f, BakeSize, BakeSize), 0, 0);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                ConvertLinearTextureToGamma(texture);
            }

            CleanupFramePixels(texture);
            texture.Apply(false, false);
            RenderTexture.active = previous;
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
        }

        private static void CleanupFramePixels(Texture2D texture)
        {
            Color32[] source = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;
            Color32[] dest = new Color32[source.Length];
            byte clearAlpha = (byte)Mathf.RoundToInt(TransparentAlphaThreshold * 255f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    Color32 pixel = source[index];
                    float alpha = pixel.a / 255f;
                    float luminance = (pixel.r + pixel.g + pixel.b) / (3f * 255f);
                    bool clear = pixel.a < clearAlpha
                        || (luminance < NearBlackClearThreshold && alpha < 0.35f);
                    if (clear)
                    {
                        dest[index] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    bool outline = pixel.a < SolidAlpha
                        || HasClearNeighbor(source, width, height, x, y, clearAlpha);
                    Color32 fill = pixel;
                    if (outline
                        && TryAverageSolidNeighbors(
                            source,
                            width,
                            height,
                            x,
                            y,
                            SolidAlpha,
                            out Color32 neighbor))
                    {
                        fill = neighbor;
                    }

                    float scale = outline ? BakeBrightness * OutlineDarken : BakeBrightness;
                    dest[index] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fill.r * scale), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fill.g * scale), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fill.b * scale), 0, 255),
                        255);
                }
            }

            texture.SetPixels32(dest);
        }

        private static bool HasClearNeighbor(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            byte clearAlpha)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                int neighborY = y + offsetY;
                if (neighborY < 0 || neighborY >= height)
                {
                    continue;
                }

                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    int neighborX = x + offsetX;
                    if (neighborX < 0 || neighborX >= width)
                    {
                        continue;
                    }

                    if (pixels[(neighborY * width) + neighborX].a < clearAlpha)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryAverageSolidNeighbors(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            byte solidAlpha,
            out Color32 average)
        {
            if (TryAverageSolidNeighborsRadius(pixels, width, height, x, y, solidAlpha, 1, out average)
                || TryAverageSolidNeighborsRadius(pixels, width, height, x, y, solidAlpha, 2, out average))
            {
                return true;
            }

            average = default;
            return false;
        }

        private static bool TryAverageSolidNeighborsRadius(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            byte solidAlpha,
            int radius,
            out Color32 average)
        {
            int red = 0;
            int green = 0;
            int blue = 0;
            int count = 0;
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                int neighborY = y + offsetY;
                if (neighborY < 0 || neighborY >= height)
                {
                    continue;
                }

                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    int neighborX = x + offsetX;
                    if (neighborX < 0 || neighborX >= width)
                    {
                        continue;
                    }

                    Color32 pixel = pixels[(neighborY * width) + neighborX];
                    if (pixel.a < solidAlpha)
                    {
                        continue;
                    }

                    red += pixel.r;
                    green += pixel.g;
                    blue += pixel.b;
                    count++;
                }
            }

            if (count <= 0)
            {
                average = default;
                return false;
            }

            average = new Color32(
                (byte)(red / count),
                (byte)(green / count),
                (byte)(blue / count),
                255);
            return true;
        }

        private static Light CreateBakeLight(Transform parent, string name, Vector3 euler, float intensity)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localRotation = Quaternion.Euler(euler);
            lightObject.layer = BakeLayer;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = Color.white;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << BakeLayer;
            return light;
        }

        private static UnityEngine.Camera CreateBakeCamera(Transform parent, Bounds bounds)
        {
            GameObject cameraObject = new GameObject("DesktopPetBakeCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.layer = BakeLayer;
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            camera.enabled = false;
            camera.allowHDR = false;
            camera.cullingMask = 1 << BakeLayer;

            float radius = Mathf.Max(bounds.extents.magnitude, 0.35f);
            camera.orthographicSize = radius * 1.15f;
            Vector3 center = bounds.center;
            // Zフロントモデルの正面を撮るためカメラは+Z側へ置く
            cameraObject.transform.position = center + new Vector3(0f, radius * 0.12f, radius * 2.5f);
            cameraObject.transform.LookAt(center);
            return camera;
        }

        private static void ApplyBakeLayerRecursive(Transform target)
        {
            if (target == null)
            {
                return;
            }

            target.gameObject.layer = BakeLayer;
            for (int i = 0; i < target.childCount; i++)
            {
                ApplyBakeLayerRecursive(target.GetChild(i));
            }
        }

        private static List<Light> DisableSceneLightsExcept(Transform bakeRoot)
        {
            List<Light> disabled = new List<Light>(16);
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null || !light.enabled)
                {
                    continue;
                }

                if (bakeRoot != null && light.transform.IsChildOf(bakeRoot))
                {
                    continue;
                }

                light.enabled = false;
                disabled.Add(light);
            }

            return disabled;
        }

        private static void RestoreSceneLights(List<Light> disabledLights)
        {
            if (disabledLights == null)
            {
                return;
            }

            for (int i = 0; i < disabledLights.Count; i++)
            {
                Light light = disabledLights[i];
                if (light != null)
                {
                    light.enabled = true;
                }
            }
        }

        private static Bounds ResolveBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return bounds;
        }
    }
}
