using Audio;
using Audio.Interface;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using Extensions;
using SaveData;
using SaveData.Interface;
using Scene.TrainingScene.Domain;
using Scene.TrainingScene.Interface;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using VContainer;

using Localization;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成開始時の継承演出
    /// 黒背景に継承先を中央継承元を左右へ配置し光の合流後に継承文字を出す
    /// </summary>
    public sealed class TrainingInheritancePresentationView :
        MonoBehaviour,
        ITrainingInheritancePresentation,
        ILanguageAwareUi
    {
        private static readonly Quaternion FacingRotation = Quaternion.Euler(0f, 180f, 0f);

        [Header("Stage")]
        [SerializeField] private GameObject stageRoot;
        [SerializeField] private Transform centerAnchor;
        [SerializeField] private Transform leftAnchor;
        [SerializeField] private Transform rightAnchor;
        [SerializeField] private Transform importParent;

        [Header("Effects")]
        [Tooltip("継承元消去に使うTeleportエフェクト")]
        [SerializeField] private GameObject parentDisappearEffectPrefab;
        [Tooltip("継承先へ降ってくる光に使うCrystalエフェクト")]
        [SerializeField] private GameObject fallingLightEffectPrefab;
        [Tooltip("光到達時に継承先へ出すMagic shieldエフェクト")]
        [SerializeField] private GameObject traineeAppearEffectPrefab;
        [Tooltip("光到達時に継承先へ出すMagic circleエフェクト")]
        [SerializeField] private GameObject traineeMagicCircleEffectPrefab;

        [Header("Title")]
        [SerializeField] private Canvas inheritanceTitleCanvas;
        [SerializeField] private TMP_Text inheritanceTitleText;

        [Inject] private readonly IClayModelImporter importer;
        [Inject] private readonly IClayModelSaveService saveService;
        [Inject] private readonly LoadedModelConfigurator configurator;
        [Inject] private readonly ITrainingBackgroundView backgroundView;
        [Inject] private readonly ITrainingLocationCameraView locationCameraView;
        [Inject] private readonly ISeService seService;

        private GameObject leftParentModel;
        private GameObject rightParentModel;
        private GameObject preparedTraineeModel;
        private readonly List<GameObject> activeDisappearEffects = new List<GameObject>();
        private Transform leftFallingLightRoot;
        private Transform rightFallingLightRoot;
        private Transform traineeOriginalParent;
        private Vector3 traineeOriginalPosition;
        private Quaternion traineeOriginalRotation;
        private Vector3 traineeOriginalScale;
        private bool hasTraineeSnapshot;
        private bool isPrepared;

        private void Awake()
        {
            ValidateSerializedReferences();
            EnsureFallingLightRoots();
            SetStageActive(false);
            SetFallingLightRootActive(leftFallingLightRoot, false);
            SetFallingLightRootActive(rightFallingLightRoot, false);
            SetTitleVisible(false);
        }

        /// <inheritdoc/>
        public async UniTask PrepareAsync(
            GameObject traineeModel,
            int parentSlotA,
            int parentSlotB,
            CancellationToken cancellationToken)
        {
            isPrepared = false;
            preparedTraineeModel = null;
            if (!ValidatePlay(traineeModel))
            {
                return;
            }

            backgroundView?.ShowInheritanceBackground();
            SetStageActive(true);
            SetTitleVisible(false);
            ClearFallingLightEffects();
            SetFallingLightRootActive(leftFallingLightRoot, false);
            SetFallingLightRootActive(rightFallingLightRoot, false);

            CaptureTrainee(traineeModel);
            PlaceModel(traineeModel, centerAnchor);
            preparedTraineeModel = traineeModel;

            leftParentModel = await LoadParentAsync(parentSlotA, leftAnchor, cancellationToken);
            rightParentModel = await LoadParentAsync(parentSlotB, rightAnchor, cancellationToken);
            EnsureModelsVisible();
            locationCameraView?.ApplyInheritanceView();
            isPrepared = true;
        }

        /// <inheritdoc/>
        public async UniTask PlayAsync(CancellationToken cancellationToken)
        {
            GameObject traineeModel = preparedTraineeModel;
            if (!isPrepared || traineeModel == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] PrepareAsyncが未完了です先に配置してください",
                    this);
                return;
            }

            EnsureModelsVisible();
            locationCameraView?.ApplyInheritanceView();

            bool reachedTitle = false;
            try
            {
                await WaitSeconds(
                    TrainingSettings.InheritancePresentationPoseSeconds,
                    cancellationToken);

                PlayTimedSe(
                    SeTrackId.InheritanceGlow,
                    TrainingSettings.InheritancePresentationGlowSeconds);
                await WaitSeconds(
                    TrainingSettings.InheritancePresentationGlowSeconds,
                    cancellationToken);

                Vector3 leftStart = ResolveModelWorldPosition(leftParentModel, leftAnchor);
                Vector3 rightStart = ResolveModelWorldPosition(rightParentModel, rightAnchor);
                PlayParentDisappearEffects(leftParentModel, leftAnchor, rightParentModel, rightAnchor);
                HideParentModels();

                PrepareFallingLight(leftFallingLightRoot, leftStart);
                PrepareFallingLight(rightFallingLightRoot, rightStart);

                float peakY = Mathf.Max(leftStart.y, rightStart.y)
                    + TrainingSettings.InheritancePresentationLightRiseHeight;
                Vector3 leftRisePeak = new Vector3(leftStart.x, peakY, leftStart.z);
                Vector3 rightRisePeak = new Vector3(rightStart.x, peakY, rightStart.z);
                PlayTimedSe(
                    SeTrackId.InheritanceLightRise,
                    TrainingSettings.InheritancePresentationLightRiseSeconds);
                await MoveFallingLightsAsync(
                    leftRisePeak,
                    rightRisePeak,
                    TrainingSettings.InheritancePresentationLightRiseSeconds,
                    cancellationToken);

                Vector3 centerTarget = ResolveModelWorldPosition(traineeModel, centerAnchor)
                    + Vector3.up * 1.2f;
                Vector3 overhead = new Vector3(centerTarget.x, peakY, centerTarget.z);
                await MoveFallingLightsAsync(
                    overhead,
                    overhead,
                    TrainingSettings.InheritancePresentationLightConvergeSeconds,
                    cancellationToken);

                UniTask fallTask = MoveFallingLightsAsync(
                    centerTarget,
                    centerTarget,
                    TrainingSettings.InheritancePresentationLightFallSeconds,
                    cancellationToken);
                float shieldLeadIn = Mathf.Clamp(
                    TrainingSettings.InheritancePresentationAppearEffectLeadInSeconds,
                    0f,
                    TrainingSettings.InheritancePresentationLightFallSeconds);
                float delayBeforeShield =
                    TrainingSettings.InheritancePresentationLightFallSeconds - shieldLeadIn;
                if (delayBeforeShield > 0f)
                {
                    await WaitSeconds(delayBeforeShield, cancellationToken);
                }

                UniTask shieldTask = PlayTraineeAppearEffectAsync(
                    traineeModel,
                    centerAnchor,
                    cancellationToken);
                StartPersistentMagicCircleEffect(traineeModel, centerAnchor);
                seService?.Play(SeTrackId.InheritanceLightGlow);
                await fallTask;

                UniTask settleTask = SettleFallingLightsOnTraineeAsync(
                    traineeModel,
                    centerAnchor,
                    cancellationToken);
                await UniTask.WhenAll(shieldTask, settleTask);
                await ShowTitleAnimatedAsync(cancellationToken);

                await WaitSeconds(
                    TrainingSettings.InheritancePresentationTitleSeconds,
                    cancellationToken);
                SetTitleVisible(false);
                reachedTitle = true;
            }
            finally
            {
                // タイトル到達後は結果UI表示のため終了状態を維持する
                if (!reachedTitle)
                {
                    await CleanupAsync(traineeModel, cancellationToken);
                }
            }
        }

        /// <inheritdoc/>
        public async UniTask FinishAsync(GameObject traineeModel, CancellationToken cancellationToken)
        {
            await CleanupAsync(traineeModel, cancellationToken);
        }

        /// <inheritdoc/>
        public void HideForLeave()
        {
            StopInheritanceSe();
            DestroyParents();
            ClearDisappearEffects();
            ClearFallingLightEffects();
            SetFallingLightRootActive(leftFallingLightRoot, false);
            SetFallingLightRootActive(rightFallingLightRoot, false);
            SetTitleVisible(false);
            SetStageActive(false);
            hasTraineeSnapshot = false;
            isPrepared = false;
            preparedTraineeModel = null;
            backgroundView?.ShowDefaultBackground();
            locationCameraView?.ApplyDefaultView();
        }

        private bool ValidatePlay(GameObject traineeModel)
        {
            if (traineeModel == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] traineeModelがnullです",
                    this);
                return false;
            }

            if (centerAnchor == null || leftAnchor == null || rightAnchor == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] Anchorが未配線ですHierarchyで接続してください",
                    this);
                return false;
            }

            if (importer == null || saveService == null || configurator == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] importer/saveService/configuratorが未設定です",
                    this);
                return false;
            }

            return true;
        }

        private async UniTask<GameObject> LoadParentAsync(
            int slotIndex,
            Transform anchor,
            CancellationToken cancellationToken)
        {
            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.TrainedPlayer, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                Debug.LogError(
                    $"[TrainingInheritancePresentationView] 継承元スロット{slotIndex}が無効です",
                    this);
                return null;
            }

            string path = ModelSaveStorage.ResolveReadPath(slot.glbFileName);
            Transform parent = importParent != null ? importParent : transform;
            GameObject imported = await importer.ImportFromGlbAsync(path, parent, cancellationToken);
            if (imported == null)
            {
                Debug.LogError(
                    $"[TrainingInheritancePresentationView] 継承元の読込に失敗しました slot={slotIndex}",
                    this);
                return null;
            }

            LoadedModelConfigurator.Result configured = configurator.Configure(imported);
            configured.Motion?.SetRootTranslationEnabled(false);
            configured.Motion?.Play(MotionType.Idle);
            PlaceParentModel(imported, anchor);
            return imported;
        }

        private void CaptureTrainee(GameObject traineeModel)
        {
            traineeOriginalParent = traineeModel.transform.parent;
            traineeOriginalPosition = traineeModel.transform.position;
            traineeOriginalRotation = traineeModel.transform.rotation;
            traineeOriginalScale = traineeModel.transform.localScale;
            hasTraineeSnapshot = true;
        }

        private void PlaceModel(GameObject model, Transform anchor)
        {
            if (model == null || anchor == null)
            {
                return;
            }

            model.transform.SetParent(anchor, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = FacingRotation;
            model.transform.localScale = Vector3.one;
            model.SetActive(true);
        }

        private void PlaceParentModel(GameObject model, Transform anchor)
        {
            if (model == null || anchor == null)
            {
                return;
            }

            float sideSign = Mathf.Sign(anchor.localPosition.x);
            if (Mathf.Approximately(sideSign, 0f))
            {
                sideSign = 1f;
            }

            // 左側は中央向きに右へ右側は中央向きに左へヨーを傾ける
            float yaw = 180f
                + sideSign * TrainingSettings.InheritancePresentationParentInwardYawDegrees;
            model.transform.SetParent(anchor, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            model.transform.localScale = Vector3.one;
            model.SetActive(true);
        }

        private void EnsureModelsVisible()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (preparedTraineeModel != null)
            {
                preparedTraineeModel.SetActive(true);
            }

            if (leftParentModel != null)
            {
                leftParentModel.SetActive(true);
            }

            if (rightParentModel != null)
            {
                rightParentModel.SetActive(true);
            }
        }

        private void HideParentModels()
        {
            if (leftParentModel != null)
            {
                leftParentModel.SetActive(false);
            }

            if (rightParentModel != null)
            {
                rightParentModel.SetActive(false);
            }
        }

        private void PlayParentDisappearEffects(
            GameObject leftModel,
            Transform leftFallback,
            GameObject rightModel,
            Transform rightFallback)
        {
            SpawnAndPlayEffectOnce(
                parentDisappearEffectPrefab,
                ResolveEffectWorldPosition(leftModel, leftFallback),
                "[TrainingInheritancePresentationView] parentDisappearEffectPrefabが未配線ですTeleport.prefabを接続してください",
                TrainingSettings.InheritancePresentationDisappearEffectSeconds);
            SpawnAndPlayEffectOnce(
                parentDisappearEffectPrefab,
                ResolveEffectWorldPosition(rightModel, rightFallback),
                "[TrainingInheritancePresentationView] parentDisappearEffectPrefabが未配線ですTeleport.prefabを接続してください",
                TrainingSettings.InheritancePresentationDisappearEffectSeconds);
        }

        private async UniTask PlayTraineeAppearEffectAsync(
            GameObject traineeModel,
            Transform fallback,
            CancellationToken cancellationToken)
        {
            await PlayFittedImpactEffectAsync(
                traineeAppearEffectPrefab,
                traineeModel,
                fallback,
                TrainingSettings.InheritancePresentationShieldRingLocalY,
                TrainingSettings.InheritancePresentationAppearEffectSeconds,
                TrainingSettings.InheritancePresentationAppearEffectSimulationSpeed,
                "[TrainingInheritancePresentationView] traineeAppearEffectPrefabが未配線ですMagic shield blue.prefabを接続してください",
                cancellationToken);
        }

        private void StartPersistentMagicCircleEffect(GameObject traineeModel, Transform fallback)
        {
            if (traineeMagicCircleEffectPrefab == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] traineeMagicCircleEffectPrefabが未配線ですMagic circle.prefabを接続してください",
                    this);
                return;
            }

            UnityEngine.Object spawned = Instantiate((UnityEngine.Object)traineeMagicCircleEffectPrefab);
            GameObject instance = spawned as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] Magic circle生成に失敗しました参照がGameObjectではありません",
                    this);
                if (spawned != null)
                {
                    Destroy(spawned);
                }

                return;
            }

            activeDisappearEffects.Add(instance);
            instance.transform.SetParent(transform, true);
            FitEffectToTrainee(instance, traineeModel, fallback, 0f);
            PlayEffectLooping(instance);
        }

        private async UniTask SettleFallingLightsOnTraineeAsync(
            GameObject traineeModel,
            Transform fallback,
            CancellationToken cancellationToken)
        {
            Vector3 stayPosition = ResolveCrystalStayPosition(traineeModel, fallback);
            float targetScale = ResolveTraineeEffectScale(traineeModel)
                * TrainingSettings.InheritancePresentationCrystalSettleScaleMultiplier;

            // 二重表示を避け片側のCrystalを残してキャラ位置で拡大継続
            ClearFallingLightEffect(rightFallingLightRoot);
            SetFallingLightRootActive(rightFallingLightRoot, false);

            if (leftFallingLightRoot == null)
            {
                return;
            }

            float startScale = leftFallingLightRoot.localScale.x;
            if (startScale <= 0.001f)
            {
                startScale = 1f;
            }

            leftFallingLightRoot.position = stayPosition;
            leftFallingLightRoot.localScale = Vector3.one * startScale;
            SetFallingLightRootActive(leftFallingLightRoot, true);
            EnsureFallingLightLooping(leftFallingLightRoot);

            float duration = Mathf.Max(
                0.01f,
                TrainingSettings.InheritancePresentationCrystalSettleSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (leftFallingLightRoot == null)
                {
                    return;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float scale = Mathf.Lerp(startScale, targetScale, eased);
                leftFallingLightRoot.localScale = Vector3.one * scale;
                leftFallingLightRoot.position = stayPosition;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (leftFallingLightRoot != null)
            {
                leftFallingLightRoot.localScale = Vector3.one * targetScale;
                leftFallingLightRoot.position = stayPosition;
            }
        }

        private static Vector3 ResolveCrystalStayPosition(GameObject traineeModel, Transform fallback)
        {
            if (TryGetRendererBounds(traineeModel, out Bounds bounds))
            {
                return bounds.center;
            }

            return ResolveModelWorldPosition(traineeModel, fallback);
        }

        private static float ResolveTraineeEffectScale(GameObject traineeModel)
        {
            if (!TryGetRendererBounds(traineeModel, out Bounds bounds))
            {
                return 1f;
            }

            float height = Mathf.Max(0.01f, bounds.size.y);
            float width = Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z));
            float scaleByHeight = height / TrainingSettings.InheritancePresentationShieldReferenceHeight;
            float scaleByWidth = width / TrainingSettings.InheritancePresentationShieldReferenceDiameter;
            return Mathf.Clamp(Mathf.Max(scaleByHeight, scaleByWidth), 0.25f, 5f);
        }

        private async UniTask PlayFittedImpactEffectAsync(
            GameObject prefab,
            GameObject traineeModel,
            Transform fallback,
            float ringLocalY,
            float fallbackDuration,
            float simulationSpeed,
            string missingPrefabError,
            CancellationToken cancellationToken)
        {
            if (prefab == null)
            {
                Debug.LogError(missingPrefabError, this);
                return;
            }

            UnityEngine.Object spawned = Instantiate((UnityEngine.Object)prefab);
            GameObject instance = spawned as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] 出現エフェクト生成に失敗しました参照がGameObjectではありません",
                    this);
                if (spawned != null)
                {
                    Destroy(spawned);
                }

                return;
            }

            activeDisappearEffects.Add(instance);
            instance.transform.SetParent(transform, true);
            FitEffectToTrainee(instance, traineeModel, fallback, ringLocalY);
            float duration = ConfigureAppearEffect(
                instance,
                fallbackDuration,
                simulationSpeed);
            try
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(Mathf.Max(0.1f, duration)),
                    ignoreTimeScale: false,
                    cancellationToken: cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                RemoveAndDestroyEffect(instance);
                throw;
            }

            RemoveAndDestroyEffect(instance);
        }

        private void FitEffectToTrainee(
            GameObject instance,
            GameObject traineeModel,
            Transform fallback,
            float ringLocalY)
        {
            if (instance == null)
            {
                return;
            }

            Transform effectTransform = instance.transform;
            if (!TryGetRendererBounds(traineeModel, out Bounds bounds))
            {
                effectTransform.SetPositionAndRotation(
                    ResolveModelWorldPosition(traineeModel, fallback),
                    FacingRotation);
                effectTransform.localScale = Vector3.one;
                return;
            }

            float scale = ResolveTraineeEffectScale(traineeModel);

            Vector3 rootPosition;
            if (ringLocalY <= 0f)
            {
                // 地面円は足元中央
                rootPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            else
            {
                // リング(local Y)が胴体中心に来るようルートを足元側へずらす
                rootPosition = bounds.center - Vector3.up * (ringLocalY * scale);
            }

            effectTransform.SetPositionAndRotation(rootPosition, FacingRotation);
            effectTransform.localScale = Vector3.one * scale;
        }

        private static void EnsureFallingLightLooping(Transform root)
        {
            if (root == null || root.childCount <= 0)
            {
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null)
                {
                    PlayEffectLooping(child.gameObject);
                }
            }
        }

        private void AttachFallingLightEffect(Transform root)
        {
            if (root == null)
            {
                return;
            }

            if (fallingLightEffectPrefab == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] fallingLightEffectPrefabが未配線ですCrystal effect blue.prefabを接続してください",
                    this);
                return;
            }

            UnityEngine.Object spawned = Instantiate(
                (UnityEngine.Object)fallingLightEffectPrefab,
                root);
            GameObject instance = spawned as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] 下降光エフェクト生成に失敗しました参照がGameObjectではありません",
                    this);
                if (spawned != null)
                {
                    Destroy(spawned);
                }

                return;
            }

            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;
            PlayEffectLooping(instance);
        }

        private void ClearFallingLightEffects()
        {
            ClearFallingLightEffect(leftFallingLightRoot);
            ClearFallingLightEffect(rightFallingLightRoot);
        }

        private void ClearFallingLightEffect(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                GameObject childObject = child.gameObject;
                ParticleSystem[] systems = childObject.GetComponentsInChildren<ParticleSystem>(true);
                for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
                {
                    if (systems[systemIndex] != null)
                    {
                        systems[systemIndex].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }

                Destroy(childObject);
            }
        }

        private static void PlayEffectLooping(GameObject instance)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            if (systems == null)
            {
                return;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }

                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                system.Play(false);
            }
        }

        private void EnsureFallingLightRoots()
        {
            leftFallingLightRoot = CreateFallingLightRoot(leftFallingLightRoot, "LeftFallingLight");
            rightFallingLightRoot = CreateFallingLightRoot(rightFallingLightRoot, "RightFallingLight");
        }

        private Transform CreateFallingLightRoot(Transform existing, string objectName)
        {
            if (existing != null)
            {
                return existing;
            }

            Transform parent = stageRoot != null ? stageRoot.transform : transform;
            GameObject rootObject = new GameObject(objectName);
            Transform root = rootObject.transform;
            root.SetParent(parent, false);
            rootObject.SetActive(false);
            return root;
        }

        private GameObject SpawnAndPlayEffectOnce(
            GameObject prefab,
            Vector3 worldPosition,
            string missingPrefabError,
            float fallbackDuration,
            bool autoDestroy = true)
        {
            if (prefab == null)
            {
                Debug.LogError(missingPrefabError, this);
                return null;
            }

            UnityEngine.Object spawned = Instantiate(
                (UnityEngine.Object)prefab,
                worldPosition,
                Quaternion.identity);
            GameObject instance = spawned as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] エフェクト生成に失敗しました参照がGameObjectではありません",
                    this);
                if (spawned != null)
                {
                    Destroy(spawned);
                }

                return null;
            }

            activeDisappearEffects.Add(instance);
            instance.transform.SetParent(transform, true);
            float duration = ConfigureEffectForwardOnce(instance, fallbackDuration);
            if (autoDestroy)
            {
                DestroyEffectDelayedAsync(instance, duration).Forget();
            }

            return instance;
        }

        private static float ConfigureEffectForwardOnce(GameObject instance, float fallbackDuration)
        {
            return ConfigureAppearEffect(instance, fallbackDuration, 1f);
        }

        private static float ConfigureAppearEffect(
            GameObject instance,
            float fallbackDuration,
            float simulationSpeed)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            float speed = Mathf.Max(0.01f, simulationSpeed);
            float maxDuration = fallbackDuration / speed;
            if (systems == null || systems.Length == 0)
            {
                return maxDuration;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }

                // playOnAwakeのループ開始を止め1回だけ再生する
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                main.loop = false;
                main.simulationSpeed = speed;
                maxDuration = Mathf.Max(maxDuration, main.duration / speed);
                system.Play(false);
            }

            return maxDuration;
        }

        private async UniTaskVoid DestroyEffectDelayedAsync(
            GameObject instance,
            float durationSeconds)
        {
            try
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(Mathf.Max(0.1f, durationSeconds)),
                    ignoreTimeScale: false,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            RemoveAndDestroyEffect(instance);
        }

        private void RemoveAndDestroyEffect(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            activeDisappearEffects.Remove(instance);
            Destroy(instance);
        }

        private void ClearDisappearEffects()
        {
            for (int i = 0; i < activeDisappearEffects.Count; i++)
            {
                if (activeDisappearEffects[i] != null)
                {
                    Destroy(activeDisappearEffects[i]);
                }
            }

            activeDisappearEffects.Clear();
        }

        private static bool TryGetRendererBounds(GameObject model, out Bounds bounds)
        {
            bounds = default;
            if (model == null)
            {
                return false;
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                // パーティクル等を除外しモデル本体の大きさだけ使う
                if (renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private static Vector3 ResolveEffectWorldPosition(GameObject model, Transform fallback)
        {
            if (TryGetRendererBounds(model, out Bounds bounds))
            {
                return bounds.center;
            }

            return ResolveModelWorldPosition(model, fallback);
        }

        private static Vector3 ResolveModelWorldPosition(GameObject model, Transform fallback)
        {
            if (model != null)
            {
                return model.transform.position;
            }

            return fallback != null ? fallback.position : Vector3.zero;
        }

        private void PrepareFallingLight(Transform root, Vector3 worldPosition)
        {
            if (root == null)
            {
                return;
            }

            ClearFallingLightEffect(root);
            root.SetParent(stageRoot != null ? stageRoot.transform : transform, true);
            root.position = worldPosition;
            root.localScale = Vector3.one;
            AttachFallingLightEffect(root);
            SetFallingLightRootActive(root, true);
        }

        private async UniTask MoveFallingLightsAsync(
            Vector3 leftTarget,
            Vector3 rightTarget,
            float durationSeconds,
            CancellationToken cancellationToken)
        {
            Vector3 leftStart = leftFallingLightRoot != null ? leftFallingLightRoot.position : leftTarget;
            Vector3 rightStart = rightFallingLightRoot != null ? rightFallingLightRoot.position : rightTarget;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, durationSeconds);
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                if (leftFallingLightRoot != null)
                {
                    leftFallingLightRoot.position = Vector3.Lerp(leftStart, leftTarget, eased);
                }

                if (rightFallingLightRoot != null)
                {
                    rightFallingLightRoot.position = Vector3.Lerp(rightStart, rightTarget, eased);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (leftFallingLightRoot != null)
            {
                leftFallingLightRoot.position = leftTarget;
            }

            if (rightFallingLightRoot != null)
            {
                rightFallingLightRoot.position = rightTarget;
            }
        }

        private async UniTask CleanupAsync(GameObject traineeModel, CancellationToken cancellationToken)
        {
            StopInheritanceSe();
            SetTitleVisible(false);
            ClearFallingLightEffects();
            SetFallingLightRootActive(leftFallingLightRoot, false);
            SetFallingLightRootActive(rightFallingLightRoot, false);
            DestroyParents();
            ClearDisappearEffects();
            RestoreTrainee(traineeModel);
            SetStageActive(false);
            backgroundView?.ShowDefaultBackground();
            locationCameraView?.ApplyDefaultView();
            isPrepared = false;
            preparedTraineeModel = null;
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
        }

        private void RestoreTrainee(GameObject traineeModel)
        {
            if (traineeModel == null || !hasTraineeSnapshot)
            {
                return;
            }

            traineeModel.transform.SetParent(traineeOriginalParent, true);
            traineeModel.transform.SetPositionAndRotation(
                traineeOriginalPosition,
                traineeOriginalRotation);
            traineeModel.transform.localScale = traineeOriginalScale;
            traineeModel.SetActive(true);
            hasTraineeSnapshot = false;
        }

        private void DestroyParents()
        {
            if (leftParentModel != null)
            {
                Destroy(leftParentModel);
                leftParentModel = null;
            }

            if (rightParentModel != null)
            {
                Destroy(rightParentModel);
                rightParentModel = null;
            }
        }

        private void SetStageActive(bool active)
        {
            // stageRootが自身だとコンポーネントごと落ちるため子オブジェクトだけ切替する
            if (stageRoot != null && stageRoot != gameObject)
            {
                stageRoot.SetActive(active);
                return;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null)
                {
                    child.gameObject.SetActive(active);
                }
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        private static void SetFallingLightRootActive(Transform root, bool active)
        {
            if (root != null)
            {
                root.gameObject.SetActive(active);
            }
        }

        private async UniTask ShowTitleAnimatedAsync(CancellationToken cancellationToken)
        {
            if (inheritanceTitleText == null || inheritanceTitleCanvas == null)
            {
                SetTitleVisible(true);
                return;
            }

            inheritanceTitleText.text = LocalizedText.Get(GameTextKeys.TrainingInheritance);
            Color baseColor = inheritanceTitleText.color;
            baseColor.a = 1f;
            inheritanceTitleText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            inheritanceTitleText.alpha = 0f;

            RectTransform titleRect = inheritanceTitleText.rectTransform;
            float startScale = TrainingSettings.InheritancePresentationTitleAppearStartScale;
            float impactScale = TrainingSettings.InheritancePresentationTitleImpactScale;
            float spinDegrees = TrainingSettings.InheritancePresentationTitleSpinDegrees;
            if (titleRect != null)
            {
                titleRect.localScale = Vector3.one * startScale;
                titleRect.localRotation = Quaternion.Euler(0f, 0f, spinDegrees);
            }

            CanvasVisibilityUtility.SetCanvasEnabled(inheritanceTitleCanvas, true);

            float duration = Mathf.Max(
                0.01f,
                TrainingSettings.InheritancePresentationTitleAppearSeconds);
            float impactEnd = duration * 0.28f;
            float bounceEnd = duration * 0.72f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (elapsed <= impactEnd)
                {
                    float impactT = Mathf.Clamp01(elapsed / impactEnd);
                    float impactEased = 1f - Mathf.Pow(1f - impactT, 4f);
                    inheritanceTitleText.alpha = impactEased;
                    inheritanceTitleText.color = Color.Lerp(
                        new Color(1f, 1f, 1f, impactEased),
                        new Color(1f, 0.98f, 0.75f, impactEased),
                        impactEased);
                    if (titleRect != null)
                    {
                        float scale = Mathf.Lerp(startScale, impactScale, impactEased);
                        float z = Mathf.Lerp(spinDegrees, -spinDegrees * 0.35f, impactEased);
                        titleRect.localScale = Vector3.one * scale;
                        titleRect.localRotation = Quaternion.Euler(0f, 0f, z);
                    }
                }
                else if (elapsed <= bounceEnd)
                {
                    float bounceT = Mathf.Clamp01((elapsed - impactEnd) / (bounceEnd - impactEnd));
                    float bounceEased = ElasticOut(bounceT);
                    inheritanceTitleText.alpha = 1f;
                    inheritanceTitleText.color = Color.Lerp(
                        new Color(1f, 0.98f, 0.75f, 1f),
                        baseColor,
                        bounceEased);
                    if (titleRect != null)
                    {
                        float scale = Mathf.Lerp(impactScale, 1f, bounceEased);
                        float z = Mathf.Lerp(-spinDegrees * 0.35f, 0f, bounceEased);
                        titleRect.localScale = Vector3.one * scale;
                        titleRect.localRotation = Quaternion.Euler(0f, 0f, z);
                    }
                }
                else
                {
                    float flashT = Mathf.Clamp01((elapsed - bounceEnd) / (duration - bounceEnd));
                    float pulse = 1f + Mathf.Sin(flashT * Mathf.PI) * 0.12f;
                    inheritanceTitleText.alpha = 1f;
                    inheritanceTitleText.color = Color.Lerp(
                        new Color(1f, 1f, 0.85f, 1f),
                        baseColor,
                        flashT);
                    if (titleRect != null)
                    {
                        titleRect.localScale = Vector3.one * pulse;
                        titleRect.localRotation = Quaternion.identity;
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            inheritanceTitleText.alpha = 1f;
            inheritanceTitleText.color = baseColor;
            if (titleRect != null)
            {
                titleRect.localScale = Vector3.one;
                titleRect.localRotation = Quaternion.identity;
            }
        }

        private static float ElasticOut(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f)
            {
                return 0f;
            }

            if (t >= 1f)
            {
                return 1f;
            }

            const float period = 0.35f;
            float s = period / 4f;
            return Mathf.Pow(2f, -10f * t)
                * Mathf.Sin((t - s) * (2f * Mathf.PI) / period)
                + 1f;
        }

        private void SetTitleVisible(bool visible)
        {
            if (inheritanceTitleText != null)
            {
                if (visible)
                {
                    inheritanceTitleText.text = LocalizedText.Get(GameTextKeys.TrainingInheritance);
                    inheritanceTitleText.alpha = 1f;
                }
                else
                {
                    inheritanceTitleText.alpha = 0f;
                }

                RectTransform titleRect = inheritanceTitleText.rectTransform;
                if (titleRect != null)
                {
                    titleRect.localScale = Vector3.one;
                    titleRect.localRotation = Quaternion.identity;
                }
            }

            CanvasVisibilityUtility.SetCanvasEnabled(inheritanceTitleCanvas, visible);
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            if (inheritanceTitleText == null)
            {
                return;
            }

            if (inheritanceTitleText.alpha > 0.01f)
            {
                inheritanceTitleText.text = LocalizedText.Get(GameTextKeys.TrainingInheritance);
            }
        }

        private void PlayTimedSe(SeTrackId trackId, float durationSeconds)
        {
            if (seService == null)
            {
                return;
            }

            seService.PlayTimed(trackId, durationSeconds);
        }

        private void StopInheritanceSe()
        {
            if (seService == null)
            {
                return;
            }

            seService.Stop(SeTrackId.InheritanceGlow);
            seService.Stop(SeTrackId.InheritanceLightRise);
            seService.Stop(SeTrackId.InheritanceLightFall);
        }

        private static async UniTask WaitSeconds(float seconds, CancellationToken cancellationToken)
        {
            if (seconds <= 0f)
            {
                return;
            }

            await UniTask.Delay(
                System.TimeSpan.FromSeconds(seconds),
                ignoreTimeScale: false,
                cancellationToken: cancellationToken);
        }

        private void ValidateSerializedReferences()
        {
            if (centerAnchor == null || leftAnchor == null || rightAnchor == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] Anchorが未配線ですHierarchyで接続してください",
                    this);
            }

            if (parentDisappearEffectPrefab == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] parentDisappearEffectPrefabが未配線ですTeleport.prefabを接続してください",
                    this);
            }

            if (fallingLightEffectPrefab == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] fallingLightEffectPrefabが未配線ですCrystal effect blue.prefabを接続してください",
                    this);
            }

            if (traineeAppearEffectPrefab == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] traineeAppearEffectPrefabが未配線ですMagic shield blue.prefabを接続してください",
                    this);
            }

            if (traineeMagicCircleEffectPrefab == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] traineeMagicCircleEffectPrefabが未配線ですMagic circle.prefabを接続してください",
                    this);
            }

            if (inheritanceTitleCanvas == null)
            {
                Debug.LogError(
                    "[TrainingInheritancePresentationView] inheritanceTitleCanvasが未配線ですHierarchyで接続してください",
                    this);
            }
        }
    }
}
