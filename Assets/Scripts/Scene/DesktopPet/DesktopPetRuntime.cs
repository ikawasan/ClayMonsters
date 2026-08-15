using System;
using System.Collections.Generic;
using System.Threading;
using Audio.Interface;
using Cysharp.Threading.Tasks;
using Extensions;
using SaveData;
using SaveData.Interface;
using Scene.DesktopPet.Interface;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scene.DesktopPet
{
    /// <summary>
    /// デスクトップペットの実行時ホスト
    /// セーブ時キャッシュを読み低FPS再生と窓移動を行う
    /// </summary>
    public sealed class DesktopPetRuntime : MonoBehaviour
    {
        private const int PetWindowSize = 330;
        private const int PetTargetFps = 8;
        private const int MaxPetCount = 5;
        private const int PetVisualLayer = 30;

        private IClayModelSaveService saveService;
        private IBgmService bgmService;
        private Action onStopped;
        private readonly List<int> slotIndices = new List<int>(MaxPetCount);
        private readonly List<PetActor> actors = new List<PetActor>(MaxPetCount);
        private IDesktopPetWindow window;
        private DesktopPetContextMenu contextMenu;
        private DesktopPetRandomMover mover;
        private CancellationTokenSource loopCts;
        private UnityEngine.Camera petCamera;
        private int previousTargetFrameRate;
        private bool previousRunInBackground;
        private int previousQualityLevel;
        private int previousVSyncCount;
        private bool isShuttingDown;
        private bool stayOnTop = true;
        private readonly List<UnityEngine.Camera> disabledCameras = new List<UnityEngine.Camera>();
        private readonly List<AudioListener> disabledListeners = new List<AudioListener>();
        private readonly List<Canvas> disabledCanvases = new List<Canvas>();

        /// <summary>
        /// ペット表示を開始する
        /// </summary>
        public void Begin(
            IReadOnlyList<int> playerSlotIndices,
            IClayModelSaveService clayModelSaveService,
            IBgmService clayBgmService,
            bool stayOnTop,
            Action stoppedCallback)
        {
            slotIndices.Clear();
            if (playerSlotIndices != null)
            {
                for (int i = 0; i < playerSlotIndices.Count && slotIndices.Count < MaxPetCount; i++)
                {
                    int slotIndex = playerSlotIndices[i];
                    if (slotIndex >= 0 && !slotIndices.Contains(slotIndex))
                    {
                        slotIndices.Add(slotIndex);
                    }
                }
            }

            saveService = clayModelSaveService;
            bgmService = clayBgmService;
            this.stayOnTop = stayOnTop;
            onStopped = stoppedCallback;
            StartFromSaveCache(this.GetCancellationTokenOnDestroy());
        }

        private void StartFromSaveCache(CancellationToken cancellationToken)
        {
            if (slotIndices.Count == 0)
            {
                Debug.LogError("[DesktopPetRuntime] 表示するスロットがありません");
                ShutdownInternal(quitApplication: false, restoreTitle: true);
                return;
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (TryCollectReadyCacheDirectories(out List<string> readyDirectories)
                && readyDirectories.Count == slotIndices.Count
                && DesktopPetExternalProcess.TryStart(readyDirectories, stayOnTop))
            {
                Debug.Log(
                    "[DesktopPetRuntime] キャッシュ済みのため即外部ビューアへ引き継ぎます count="
                    + readyDirectories.Count);
                ShutdownInternal(quitApplication: true, restoreTitle: false, immediate: true);
                return;
            }
#endif

            PrepareEnvironment();
            List<string> cacheDirectories = new List<string>(slotIndices.Count);
            for (int i = 0; i < slotIndices.Count; i++)
            {
                int slotIndex = slotIndices[i];
                ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
                DesktopPetSpriteCache.TryLoad(slotIndex, slot.glbFileName, out DesktopPetSpriteSheet sheet);
                actors.Add(new PetActor(slotIndex, sheet));
                cacheDirectories.Add(DesktopPetSpriteCache.GetSlotDirectory(slotIndex));
            }

            DesktopPetSpriteCache.WriteActiveMarker(cacheDirectories);
            EnterInProcessPetMode(cancellationToken);
        }

        private bool TryCollectReadyCacheDirectories(out List<string> cacheDirectories)
        {
            cacheDirectories = new List<string>(slotIndices.Count);
            if (saveService == null)
            {
                return false;
            }

            for (int i = 0; i < slotIndices.Count; i++)
            {
                int slotIndex = slotIndices[i];
                ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
                string glbFileName = slot != null ? slot.glbFileName : null;
                if (string.IsNullOrEmpty(glbFileName)
                    || !DesktopPetSpriteCache.IsReady(slotIndex, glbFileName))
                {
                    cacheDirectories.Clear();
                    return false;
                }

                cacheDirectories.Add(DesktopPetSpriteCache.GetSlotDirectory(slotIndex));
            }

            return cacheDirectories.Count > 0;
        }

        private void EnterInProcessPetMode(CancellationToken cancellationToken)
        {
            HideSceneCamerasAndListeners();
            ApplyRestQuality();
            BuildPetVisual();
            window = DesktopPetWindowFactory.Create(stayOnTop);
            contextMenu = new DesktopPetContextMenu();
            window.EnterPetMode(PetWindowSize, PetWindowSize);
            if (petCamera != null)
            {
#if UNITY_EDITOR
                petCamera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
#else
                petCamera.backgroundColor = window.ChromaKeyColor;
#endif
                petCamera.allowHDR = false;
                petCamera.allowMSAA = false;
            }

            loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ApplyNativeChromeWhenReadyAsync(loopCts.Token).Forget();
            StartInProcessFlock(loopCts.Token);
        }

        private async UniTaskVoid ApplyNativeChromeWhenReadyAsync(CancellationToken cancellationToken)
        {
            for (int i = 0; i < 12; i++)
            {
                await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
                window?.ApplyNativeChrome();
                if (Screen.width == PetWindowSize && Screen.height == PetWindowSize)
                {
                    break;
                }
            }

            await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
            window?.ApplyNativeChrome();
        }

        private void StartInProcessFlock(CancellationToken cancellationToken)
        {
            var transforms = new List<Transform>(actors.Count);
            var animators = new List<DesktopPetSpriteAnimator>(actors.Count);
            var zzzLabels = new List<TextMesh>(actors.Count);
            for (int i = 0; i < actors.Count; i++)
            {
                transforms.Add(actors[i].Renderer != null ? actors[i].Renderer.transform : null);
                animators.Add(actors[i].Animator);
                zzzLabels.Add(actors[i].ZzzLabel);
                RunIdleAttackLoopAsync(actors[i], cancellationToken).Forget();
            }

            DesktopPetInProcessFlock flock = new DesktopPetInProcessFlock(
                transforms,
                animators,
                zzzLabels,
                viewHalfExtent: Mathf.Max(0.45f, 0.85f - actors.Count * 0.05f));
            flock.RunAsync(cancellationToken).Forget();
        }

        private async UniTaskVoid RunSoloSleepLoopAsync(PetActor actor, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(UnityEngine.Random.Range(10f, 22f)),
                        cancellationToken: cancellationToken);
                    if (actor == null || actor.IsMoving || actor.Animator == null)
                    {
                        continue;
                    }

                    actor.Animator.SetFacing(DesktopPetFacing.Front);
                    actor.Animator.SetAction(DesktopPetAction.Idle);
                    if (actor.ZzzLabel != null)
                    {
                        actor.ZzzLabel.gameObject.SetActive(true);
                    }

                    float sleepSeconds = UnityEngine.Random.Range(4f, 8f);
                    float elapsed = 0f;
                    while (elapsed < sleepSeconds)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        elapsed += Time.unscaledDeltaTime;
                        if (actor.ZzzLabel != null)
                        {
                            int phase = Mathf.FloorToInt(Time.unscaledTime * 2.5f) % 3;
                            actor.ZzzLabel.text = phase == 0 ? "z" : phase == 1 ? "zz" : "zzz";
                            Vector3 pos = actor.ZzzLabel.transform.localPosition;
                            pos.y = 0.55f + 0.05f * Mathf.Sin(Time.unscaledTime * 3f);
                            actor.ZzzLabel.transform.localPosition = pos;
                        }

                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    }

                    if (actor.ZzzLabel != null)
                    {
                        actor.ZzzLabel.gameObject.SetActive(false);
                    }
                }
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        private void Update()
        {
            for (int i = 0; i < actors.Count; i++)
            {
                actors[i].Animator?.Tick(Time.unscaledDeltaTime);
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ReturnToTitle();
                return;
            }

            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame && window != null)
            {
                window.ToggleClickThrough();
            }

            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                HandleContextMenu();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                window?.ApplyNativeChrome();
            }
        }

        private void OnDestroy()
        {
            if (!isShuttingDown)
            {
                CleanupResources(restoreTitle: false);
            }

            onStopped?.Invoke();
            onStopped = null;
        }

        private void HandleContextMenu()
        {
            if (window == null)
            {
                return;
            }

#if UNITY_EDITOR
            Keyboard keyboard = Keyboard.current;
            bool shiftHeld = keyboard != null
                && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            if (shiftHeld)
            {
                ShutdownAndQuit();
            }
            else
            {
                ReturnToTitle();
            }

            return;
#else
            int command = contextMenu != null
                ? contextMenu.Show(window.Handle)
                : 0;
            switch (command)
            {
                case DesktopPetContextMenu.CommandQuitDesktopPet:
                    ShutdownAndQuit();
                    break;
                case DesktopPetContextMenu.CommandLaunchClayMonsters:
                    ReturnToTitle();
                    break;
            }
#endif
        }

        private void OnMoveDirection(PetActor actor, Vector2 direction)
        {
            if (actor == null)
            {
                return;
            }

            actor.IsMoving = true;
            actor.Animator?.SetFacing(ResolveFacing(direction.x));
            actor.Animator?.SetAction(DesktopPetAction.Walk);
        }

        private void OnIdle(PetActor actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.IsMoving = false;
            if (actor.Animator != null && actor.Animator.CurrentAction != DesktopPetAction.Attack)
            {
                actor.Animator.SetFacing(DesktopPetFacing.AnglePos45);
                actor.Animator.SetAction(DesktopPetAction.Idle);
            }
        }

        private static DesktopPetFacing ResolveFacing(float deltaX)
        {
            if (deltaX > 14f)
            {
                return DesktopPetFacing.AngleNeg45;
            }

            if (deltaX < -14f)
            {
                return DesktopPetFacing.AnglePos45;
            }

            return DesktopPetFacing.AnglePos45;
        }

        private async UniTaskVoid RunIdleAttackLoopAsync(PetActor actor, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(UnityEngine.Random.Range(8f, 16f)),
                        cancellationToken: cancellationToken);
                    if (actor == null || actor.IsMoving || actor.Animator == null || actor.Sheet == null)
                    {
                        continue;
                    }

                    if (actor.Animator.CurrentAction == DesktopPetAction.Attack)
                    {
                        continue;
                    }

                    if (actor.Sheet.GetFrameCount(actor.Animator.CurrentFacing, DesktopPetAction.Attack) <= 0)
                    {
                        continue;
                    }

                    actor.Animator.SetAction(DesktopPetAction.Attack);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        private async UniTaskVoid RunEditorSpriteWanderAsync(PetActor actor, CancellationToken cancellationToken)
        {
            if (actor?.Renderer == null)
            {
                return;
            }

            try
            {
                OnIdle(actor);
                while (!cancellationToken.IsCancellationRequested)
                {
                    Vector3 from = actor.Renderer.transform.localPosition;
                    Vector3 to = new Vector3(
                        UnityEngine.Random.Range(-0.7f, 0.7f),
                        UnityEngine.Random.Range(-0.7f, 0.7f),
                        from.z);
                    OnMoveDirection(actor, new Vector2(to.x - from.x, to.y - from.y) * 40f);
                    float elapsed = 0f;
                    const float duration = 2f;
                    while (elapsed < duration)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        elapsed += Time.unscaledDeltaTime;
                        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                        actor.Renderer.transform.localPosition = Vector3.Lerp(from, to, t);
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    }

                    OnIdle(actor);
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(UnityEngine.Random.Range(0.8f, 2f)),
                        cancellationToken: cancellationToken);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        private void PrepareEnvironment()
        {
            bgmService?.Stop();
            previousTargetFrameRate = Application.targetFrameRate;
            previousRunInBackground = Application.runInBackground;
            previousQualityLevel = QualitySettings.GetQualityLevel();
            previousVSyncCount = QualitySettings.vSyncCount;
            Application.targetFrameRate = PetTargetFps;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
        }

        private void ApplyRestQuality()
        {
            QualitySettings.SetQualityLevel(0, true);
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.antiAliasing = 0;
            QualitySettings.realtimeReflectionProbes = false;
        }

        private void HideSceneCamerasAndListeners()
        {
            disabledCameras.Clear();
            disabledListeners.Clear();
            disabledCanvases.Clear();

            UnityEngine.Camera[] cameras = UnityEngine.Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                UnityEngine.Camera camera = cameras[i];
                if (camera != null && camera.enabled)
                {
                    camera.enabled = false;
                    disabledCameras.Add(camera);
                }
            }

            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener != null && listener.enabled)
                {
                    listener.enabled = false;
                    disabledListeners.Add(listener);
                }
            }

            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && canvas.enabled)
                {
                    CanvasVisibilityUtility.SetCanvasEnabled(canvas, false);
                    disabledCanvases.Add(canvas);
                }
            }
        }

        private void RestoreScenePresentation()
        {
            for (int i = 0; i < disabledCameras.Count; i++)
            {
                if (disabledCameras[i] != null)
                {
                    disabledCameras[i].enabled = true;
                }
            }

            for (int i = 0; i < disabledListeners.Count; i++)
            {
                if (disabledListeners[i] != null)
                {
                    disabledListeners[i].enabled = true;
                }
            }

            for (int i = 0; i < disabledCanvases.Count; i++)
            {
                if (disabledCanvases[i] != null)
                {
                    CanvasVisibilityUtility.SetCanvasEnabled(disabledCanvases[i], true);
                }
            }

            disabledCameras.Clear();
            disabledListeners.Clear();
            disabledCanvases.Clear();
            QualitySettings.SetQualityLevel(previousQualityLevel, true);
            QualitySettings.vSyncCount = previousVSyncCount;
            Application.targetFrameRate = previousTargetFrameRate;
            Application.runInBackground = previousRunInBackground;
        }

        private void BuildPetVisual()
        {
            GameObject cameraObject = new GameObject("DesktopPetCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.layer = PetVisualLayer;
            petCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            petCamera.clearFlags = CameraClearFlags.SolidColor;
#if UNITY_EDITOR
            petCamera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
#elif UNITY_STANDALONE_OSX
            petCamera.backgroundColor = Color.clear;
#else
            petCamera.backgroundColor = Color.magenta;
#endif
            petCamera.allowHDR = false;
            petCamera.allowMSAA = false;
            petCamera.orthographic = true;
            petCamera.orthographicSize = 1f;
            petCamera.nearClipPlane = 0.1f;
            petCamera.farClipPlane = 10f;
            petCamera.depth = 100f;
            // タイトルの3Dモデルや白いUIジオメトリを写さない
            petCamera.cullingMask = 1 << PetVisualLayer;
            cameraObject.AddComponent<AudioListener>();

            for (int i = 0; i < actors.Count; i++)
            {
                PetActor actor = actors[i];
                GameObject spriteObject = new GameObject("DesktopPetSprite_" + actor.SlotIndex);
                spriteObject.transform.SetParent(transform, false);
                spriteObject.layer = PetVisualLayer;
                float offsetX = (i - (actors.Count - 1) * 0.5f) * 0.35f;
                spriteObject.transform.localPosition = new Vector3(offsetX, 0f, 1f);
                SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 100 + i;
                Sprite first = actor.Sheet.GetFrame(DesktopPetFacing.AnglePos45, DesktopPetAction.Idle, 0);
                renderer.sprite = first;
                FitSpriteToView(renderer, petCamera, actors.Count);
                TextMesh zzz = CreateZzzLabel(spriteObject.transform);
                if (zzz != null)
                {
                    zzz.gameObject.layer = PetVisualLayer;
                }

                actor.BindVisual(
                    renderer,
                    new DesktopPetSpriteAnimator(renderer, actor.Sheet, PetTargetFps),
                    zzz);
            }
        }

        private static TextMesh CreateZzzLabel(Transform parent)
        {
            GameObject zzzObject = new GameObject("Zzz");
            zzzObject.transform.SetParent(parent, false);
            zzzObject.transform.localPosition = new Vector3(0.28f, 0.55f, -0.1f);
            zzzObject.transform.localScale = Vector3.one * 0.08f;
            TextMesh textMesh = zzzObject.AddComponent<TextMesh>();
            textMesh.text = "zzz";
            textMesh.fontSize = 48;
            textMesh.color = new Color(0.25f, 0.45f, 0.85f, 1f);
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.5f;
            zzzObject.SetActive(false);
            return textMesh;
        }

        private static void FitSpriteToView(SpriteRenderer renderer, UnityEngine.Camera camera, int petCount)
        {
            if (renderer == null || renderer.sprite == null || camera == null)
            {
                return;
            }

            Bounds bounds = renderer.sprite.bounds;
            float spriteHeight = Mathf.Max(0.01f, bounds.size.y);
            float spriteWidth = Mathf.Max(0.01f, bounds.size.x);
            float targetSize = petCount > 1 ? 1.2f : 1.7f;
            float scale = targetSize / Mathf.Max(spriteWidth, spriteHeight);
            renderer.transform.localScale = Vector3.one * scale;
            camera.orthographicSize = targetSize * 0.55f + (petCount > 1 ? 0.35f : 0f);
        }

        private void ReturnToTitle()
        {
            ShutdownInternal(quitApplication: false, restoreTitle: true);
        }

        private void ShutdownAndQuit()
        {
            ShutdownInternal(quitApplication: true, restoreTitle: false, immediate: false);
        }

        private void ShutdownInternal(bool quitApplication, bool restoreTitle, bool immediate = false)
        {
            if (isShuttingDown)
            {
                return;
            }

            isShuttingDown = true;
            CleanupResources(restoreTitle);
            if (quitApplication)
            {
                if (immediate)
                {
                    ApplicationQuitGuard.RequestImmediateQuit();
                }
                else
                {
                    ApplicationQuitGuard.RequestQuit();
                }
            }

            Destroy(gameObject);
        }

        private void CleanupResources(bool restoreTitle)
        {
            loopCts?.Cancel();
            loopCts?.Dispose();
            loopCts = null;
            window?.Restore();
            window = null;
            for (int i = 0; i < actors.Count; i++)
            {
                actors[i]?.Dispose();
            }

            actors.Clear();
            petCamera = null;
            if (restoreTitle)
            {
                RestoreScenePresentation();
            }
            else
            {
                Application.targetFrameRate = previousTargetFrameRate;
                Application.runInBackground = previousRunInBackground;
                QualitySettings.vSyncCount = previousVSyncCount;
            }
        }

        private sealed class PetActor
        {
            public PetActor(int slotIndex, DesktopPetSpriteSheet sheet)
            {
                SlotIndex = slotIndex;
                Sheet = sheet;
            }

            public int SlotIndex { get; }

            public DesktopPetSpriteSheet Sheet { get; private set; }

            public SpriteRenderer Renderer { get; private set; }

            public DesktopPetSpriteAnimator Animator { get; private set; }

            public TextMesh ZzzLabel { get; private set; }

            public bool IsMoving { get; set; }

            public void BindVisual(
                SpriteRenderer renderer,
                DesktopPetSpriteAnimator animator,
                TextMesh zzzLabel)
            {
                Renderer = renderer;
                Animator = animator;
                ZzzLabel = zzzLabel;
            }

            public void Dispose()
            {
                Animator = null;
                Renderer = null;
                ZzzLabel = null;
                Sheet?.Dispose();
                Sheet = null;
            }
        }
    }
}
