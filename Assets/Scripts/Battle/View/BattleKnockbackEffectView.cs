using Extensions;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Battle.View
{
    /// <summary>
    /// ふきとばし時に衝撃波エフェクトをキャラ位置へ再生する
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleKnockbackEffectView : MonoBehaviour
    {
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
        private static readonly int DistortionDepthFadeId = Shader.PropertyToID("_DistortionDepthFade");
        private static readonly int DistortionNormalStrengthId = Shader.PropertyToID("_DistortionNormalStrength");
        private static readonly int TilingId = Shader.PropertyToID("_Tiling");
        private static readonly int ParticleAnimationId = Shader.PropertyToID("_ParticleAnimation");
        private static readonly int OuterRemapMinId = Shader.PropertyToID("_OuterRadialDistortionMaskRemapMin");
        private static readonly int OuterRemapMaxId = Shader.PropertyToID("_OuterRadialDistortionMaskRemapMax");
        private static readonly int OuterPowerId = Shader.PropertyToID("_OuterRadialDistortionMaskPower");
        private static readonly int InnerRadiusId = Shader.PropertyToID("_InnerRadialDistortionMaskRadius");
        private static readonly int InnerHardnessId = Shader.PropertyToID("_InnerRadialDistortionMaskHardness");
        private static readonly int InnerPowerId = Shader.PropertyToID("_InnerRadialDistortionMaskPower");
        private static readonly int RadialAlphaFeatherId = Shader.PropertyToID("_RadialAlphaMaskFeather");
        private static readonly int RadialAlphaPowerId = Shader.PropertyToID("_RadialAlphaMaskPower");
        private static readonly int ColourId = Shader.PropertyToID("_Colour");
        private static readonly int DebugId = Shader.PropertyToID("_Debug");

        private KnockbackEffectSettings settings;
        private bool hasLoggedMissingSettings;

        private void Awake()
        {
            settings = KnockbackEffectSettings.Load();
            if (settings == null)
            {
                Debug.LogError(
                    "[BattleKnockbackEffectView] Resources/Battle/KnockbackEffectSettingsがありません",
                    this);
                hasLoggedMissingSettings = true;
                return;
            }

            if (!settings.Validate(out string error))
            {
                Debug.LogError($"[BattleKnockbackEffectView] {error}", this);
            }
        }

        /// <summary>
        /// 指定キャラ位置でふきとばし衝撃波を再生する
        /// </summary>
        /// <param name="sourceRoot">発動側モデル</param>
        /// <param name="targetRoot">押し出される側モデル</param>
        public void Play(Transform sourceRoot, Transform targetRoot = null)
        {
            if (settings == null)
            {
                settings = KnockbackEffectSettings.Load();
            }

            if (settings == null)
            {
                if (!hasLoggedMissingSettings)
                {
                    Debug.LogError(
                        "[BattleKnockbackEffectView] Resources/Battle/KnockbackEffectSettingsがありません",
                        this);
                    hasLoggedMissingSettings = true;
                }

                return;
            }

            if (settings.ShockwavePrefab == null)
            {
                Debug.LogError(
                    "[BattleKnockbackEffectView] shockwavePrefabが未配線です",
                    this);
                return;
            }

            EnsureCameraSupportsDistortion();

            Vector3 origin = BattleHitEffectView.ResolveWorldHitPoint(
                sourceRoot,
                settings.HeightFallback);
            Vector3 pushDirection = ResolvePushDirection(sourceRoot, targetRoot, origin);
            Vector3 position = origin + pushDirection * settings.PushForwardOffset;

            int ringCount = settings.RingCount;
            for (int ringIndex = 0; ringIndex < ringCount; ringIndex++)
            {
                float ringScale = settings.WorldScale * (1f + ringIndex * 0.28f);
                float ringSpeed = settings.SimulationSpeed * (1f + ringIndex * 0.2f);
                float ringStartSize = settings.StartSize * (0.78f + ringIndex * 0.35f);
                float ringLifetime = settings.LifetimeSeconds * (1f + ringIndex * 0.12f);
                float ringDelay = ringIndex * 0.05f;

                SpawnRing(
                    position,
                    ringScale,
                    ringSpeed,
                    ringStartSize,
                    ringLifetime,
                    ringDelay);
            }
        }

        private void SpawnRing(
            Vector3 position,
            float scale,
            float simulationSpeed,
            float startSize,
            float lifetimeSeconds,
            float startDelay)
        {
            GameObject instance = Instantiate(settings.ShockwavePrefab, position, Quaternion.identity);
            if (instance == null)
            {
                Debug.LogError(
                    "[BattleKnockbackEffectView] ふきとばしエフェクトの生成に失敗しました",
                    this);
                return;
            }

            instance.transform.localScale = Vector3.one * scale;

            float destroyDelay = lifetimeSeconds + startDelay + 0.15f;
            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = particleSystem.main;
                main.playOnAwake = false;
                main.loop = false;
                main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
                main.simulationSpeed = simulationSpeed;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startDelay = startDelay;
                main.startLifetime = lifetimeSeconds;
                main.startSize = startSize;
                main.startSpeed = 0f;
                main.duration = Mathf.Max(0.05f, lifetimeSeconds);

                ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, CreateExpandCurve());

                ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
                colorOverLifetime.enabled = true;
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateFadeGradient());

                ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.alignment = ParticleSystemRenderSpace.View;
                    renderer.sortingFudge = -12f;
                    ApplyShockwaveMaterial(renderer.material);
                }

                particleSystem.Play(true);
                destroyDelay = Mathf.Max(
                    destroyDelay,
                    (main.duration + main.startLifetime.constantMax + startDelay) / Mathf.Max(0.01f, simulationSpeed));
            }

            ScheduleUnscaledDestroy(instance, destroyDelay);
        }

        private void ApplyShockwaveMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat(DistortionId, settings.Distortion);
            material.SetFloat(DistortionDepthFadeId, settings.DistortionDepthFade);
            material.SetFloat(DistortionNormalStrengthId, 2.4f);
            material.SetFloat(TilingId, 4.5f);
            material.SetFloat(ParticleAnimationId, 1.8f);
            material.SetFloat(OuterRemapMinId, 0.08f);
            material.SetFloat(OuterRemapMaxId, 0.92f);
            material.SetFloat(OuterPowerId, 1.35f);
            material.SetFloat(InnerRadiusId, 0.9f);
            material.SetFloat(InnerHardnessId, 0.62f);
            material.SetFloat(InnerPowerId, 1.8f);
            material.SetFloat(RadialAlphaFeatherId, 0.22f);
            material.SetFloat(RadialAlphaPowerId, 0.35f);
            material.SetFloat(DebugId, 0.18f);
            material.SetColor(ColourId, new Color(1.35f, 1.4f, 1.55f, 1f));
        }

        private static AnimationCurve CreateExpandCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.08f, 0f, 4.5f),
                new Keyframe(0.35f, 0.72f, 1.2f, 1.2f),
                new Keyframe(1f, 1f, 0.2f, 0f));
        }

        private static Gradient CreateFadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        private static Vector3 ResolvePushDirection(
            Transform sourceRoot,
            Transform targetRoot,
            Vector3 origin)
        {
            if (targetRoot != null)
            {
                Vector3 toTarget = targetRoot.position - origin;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 1e-4f)
                {
                    return toTarget.normalized;
                }
            }

            if (sourceRoot != null)
            {
                Vector3 forward = sourceRoot.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 1e-4f)
                {
                    return forward.normalized;
                }
            }

            return Vector3.forward;
        }

        private static void EnsureCameraSupportsDistortion()
        {
            UnityEngine.Camera[] cameras = UnityEngine.Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                UnityEngine.Camera camera = cameras[i];
                if (camera == null || !camera.isActiveAndEnabled)
                {
                    continue;
                }

                if (!camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                {
                    continue;
                }

                cameraData.requiresColorOption = CameraOverrideOption.On;
                cameraData.requiresDepthOption = CameraOverrideOption.On;
            }
        }

        private static void ScheduleUnscaledDestroy(GameObject target, float delaySeconds)
        {
            if (target == null)
            {
                return;
            }

            var destroyer = target.AddComponent<UnscaledTimedDestroy>();
            destroyer.Begin(Mathf.Max(0.1f, delaySeconds));
        }

        private sealed class UnscaledTimedDestroy : MonoBehaviour
        {
            private float remaining;

            public void Begin(float delaySeconds)
            {
                remaining = delaySeconds;
            }

            private void Update()
            {
                remaining -= Time.unscaledDeltaTime;
                if (remaining > 0f)
                {
                    return;
                }

                Destroy(gameObject);
            }
        }
    }
}
