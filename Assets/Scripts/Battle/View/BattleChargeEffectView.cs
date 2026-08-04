using Battle.Interface;
using Extensions;
using UnityEngine;

namespace Battle.View
{
    /// <summary>
    /// 攻撃溜め中に周囲から粒子を中心へ取り込む演出を表示する
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleChargeEffectView : MonoBehaviour, IBattleChargeEffect
    {
        private const string AdditiveParticleMaterialResourcePath = "Material/Battle/M_BattleHitParticleAdditive";
        private const float FadeOutDuration = 0.28f;
        // 攻撃直前に発生を止める最大秒数
        private const float MaxStopBeforeWindUpEnd = 0.42f;
        // 溜め尺に対する発生停止の割合上限
        private const float MaxLeadOutRatio = 0.35f;

        /// <summary>
        /// 溜めエフェクト発生を攻撃直前に止める秒数
        /// 短い溜めでは尺の一定割合に抑えて即消えを防ぐ
        /// </summary>
        /// <param name="windUpDuration">攻撃前溜め秒数</param>
        public static float ResolveEmissionLeadOutSeconds(float windUpDuration)
        {
            float duration = Mathf.Max(0.05f, windUpDuration);
            return Mathf.Min(MaxStopBeforeWindUpEnd, duration * MaxLeadOutRatio);
        }

        /// <summary>
        /// 粒子発生を続ける秒数を返す
        /// </summary>
        /// <param name="windUpDuration">攻撃前溜め秒数</param>
        public static float ResolveEmissionDuration(float windUpDuration)
        {
            float duration = Mathf.Max(0.05f, windUpDuration);
            return Mathf.Max(0.05f, duration - ResolveEmissionLeadOutSeconds(duration));
        }

        /// <summary>
        /// 互換用の発生停止秒数
        /// </summary>
        public static float ChargeEmissionLeadOutSeconds => MaxStopBeforeWindUpEnd;

        [SerializeField] private float heightFallback = 1.1f;
        [SerializeField] private float baseAbsorbRadius = 1.2f;
        [SerializeField] private float maxAbsorbRadius = 1.6f;
        [SerializeField] private Color chargeColor = new Color(1f, 0.72f, 0.28f, 1f);
        [SerializeField] private Color chargeHotColor = new Color(1f, 0.92f, 0.55f, 1f);

        private static Material additiveParticleMaterial;

        private Transform followRoot;
        private GameObject effectHost;
        private ParticleSystem absorbParticles;
        private ParticleSystem sparkParticles;
        private float absorbRadius = 1.2f;
        private float intensity;
        private float pullSpeed = 2.2f;
        private float orbitSpeed = 0.8f;
        private float fadeOutElapsed;
        private float playDuration;
        private float playElapsed;
        private bool isPlaying;
        private bool isFadingOut;
        private float fadeModuleElapsed;
        private int quietFrames;

        /// <inheritdoc/>
        public void Play(Transform modelRoot, float duration)
        {
            DestroyEffectImmediate();
            if (modelRoot == null)
            {
                return;
            }

            followRoot = modelRoot;
            intensity = 0f;
            playDuration = Mathf.Max(0.05f, duration);
            playElapsed = 0f;
            EnsureEffectHost(modelRoot);
            SyncHostTransform();
            isPlaying = true;
            isFadingOut = false;
            fadeOutElapsed = 0f;
            SetIntensity(0f);
            absorbParticles?.Play(true);
            sparkParticles?.Play(true);
        }

        /// <inheritdoc/>
        public void SetIntensity(float value)
        {
            intensity = Mathf.Clamp01(value);
            if (!isPlaying || isFadingOut || effectHost == null)
            {
                return;
            }

            pullSpeed = 1.8f + intensity * 3.2f;
            orbitSpeed = 0.55f + intensity * 0.9f;
            ApplyEmission(absorbParticles, 55f + intensity * 90f, 0.2f + intensity * 0.18f);
            ApplyEmission(sparkParticles, 35f + intensity * 80f, 0.08f + intensity * 0.1f);
            ApplyInwardMotion(absorbParticles, pullSpeed, orbitSpeed);
            ApplyInwardMotion(sparkParticles, pullSpeed * 1.25f, orbitSpeed * 1.2f);
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (effectHost == null || isFadingOut)
            {
                return;
            }

            if (!isPlaying)
            {
                DestroyEffectImmediate();
                return;
            }

            BeginFadeOut();
        }

        private void BeginFadeOut()
        {
            isPlaying = false;
            isFadingOut = true;
            fadeOutElapsed = 0f;
            fadeModuleElapsed = 0f;
            quietFrames = 0;
            intensity = 0f;
            StopEmitting(absorbParticles);
            StopEmitting(sparkParticles);
            ApplyInwardMotion(absorbParticles, pullSpeed * 0.65f, orbitSpeed * 0.65f);
            ApplyInwardMotion(sparkParticles, pullSpeed * 0.8f, orbitSpeed * 0.75f);
            BeginFadeModules(absorbParticles);
            BeginFadeModules(sparkParticles);
        }

        private void LateUpdate()
        {
            if (!isPlaying && !isFadingOut)
            {
                return;
            }

            float deltaTime = GameplayTime.PresentationDeltaTime;
            SyncHostTransform();

            if (isPlaying)
            {
                playElapsed += deltaTime;
                float stopAt = ResolveEmissionDuration(playDuration);
                if (playElapsed >= stopAt)
                {
                    BeginFadeOut();
                }

                return;
            }

            if (!isFadingOut)
            {
                return;
            }

            fadeOutElapsed += deltaTime;
            if (fadeOutElapsed >= FadeOutDuration)
            {
                DestroyEffectImmediate();
                return;
            }

            // 発生停止後に粒子が尽きたら早めに破棄
            int remaining =
                (absorbParticles != null ? absorbParticles.particleCount : 0) +
                (sparkParticles != null ? sparkParticles.particleCount : 0);
            if (remaining <= 0)
            {
                quietFrames++;
                if (quietFrames >= 2)
                {
                    DestroyEffectImmediate();
                }
            }
            else
            {
                quietFrames = 0;
            }
        }

        private void OnDisable()
        {
            DestroyEffectImmediate();
        }

        private void OnDestroy()
        {
            DestroyEffectImmediate();
        }

        private void DestroyEffectImmediate()
        {
            isPlaying = false;
            isFadingOut = false;
            followRoot = null;
            intensity = 0f;
            fadeOutElapsed = 0f;
            playDuration = 0f;
            playElapsed = 0f;

            if (effectHost != null)
            {
                Destroy(effectHost);
                effectHost = null;
            }

            absorbParticles = null;
            sparkParticles = null;
        }

        private void EnsureEffectHost(Transform modelRoot)
        {
            effectHost = new GameObject("BattleChargeAbsorb");
            effectHost.transform.SetParent(null, true);

            absorbParticles = CreateChildSystem(effectHost.transform, "Absorb", ParticleSystemRenderMode.Billboard);
            sparkParticles = CreateChildSystem(effectHost.transform, "Sparks", ParticleSystemRenderMode.Stretch);

            Bounds bounds = ResolveBounds(modelRoot);
            float bodyRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            absorbRadius = Mathf.Clamp(
                Mathf.Max(baseAbsorbRadius, bodyRadius * 1.7f),
                baseAbsorbRadius,
                maxAbsorbRadius);
            ConfigureAbsorb(absorbParticles, absorbRadius);
            ConfigureAbsorbSparks(sparkParticles, absorbRadius * 1.1f);
        }

        private void SyncHostTransform()
        {
            if (effectHost == null || followRoot == null)
            {
                return;
            }

            Vector3 center = BattleHitEffectView.ResolveWorldHitPoint(followRoot, heightFallback);
            effectHost.transform.position = center;
            effectHost.transform.rotation = Quaternion.identity;
        }

        private static void StopEmitting(ParticleSystem system)
        {
            if (system == null)
            {
                return;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void BeginFadeModules(ParticleSystem system)
        {
            if (system == null)
            {
                return;
            }

            // 残り寿命で自然消滅するよう発生だけ止めモジュールはそのまま
            ParticleSystem.MainModule main = system.main;
            main.simulationSpeed = Mathf.Max(main.simulationSpeed, 1.35f);
        }

        private static void ApplyEmission(ParticleSystem system, float rate, float size)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;

            ParticleSystem.MainModule main = system.main;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size);
        }

        private static void ApplyInwardMotion(ParticleSystem system, float inwardSpeed, float swirl)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = new ParticleSystem.MinMaxCurve(-Mathf.Max(0.2f, inwardSpeed));
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(swirl);
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = 0f;
        }

        private void ConfigureAbsorb(ParticleSystem system, float radius)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.85f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(chargeColor, chargeHotColor);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 180;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpeed = 1f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 55f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0f;

            ApplyInwardMotion(system, pullSpeed, orbitSpeed);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(chargeColor, 0f),
                    new GradientColorKey(chargeHotColor, 0.55f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0.8f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
        }

        private void ConfigureAbsorbSparks(ParticleSystem system, float radius)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            main.startColor = chargeHotColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 120;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpeed = 1f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 35f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0.05f;

            ApplyInwardMotion(system, pullSpeed * 1.25f, orbitSpeed * 1.2f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(chargeHotColor, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.12f;
            renderer.lengthScale = 2.8f;
        }

        private static ParticleSystem CreateChildSystem(
            Transform parent,
            string name,
            ParticleSystemRenderMode renderMode)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = Vector3.zero;
            host.transform.localScale = Vector3.one;

            ParticleSystem particleSystem = host.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = renderMode;
            renderer.minParticleSize = 0.01f;
            renderer.maxParticleSize = 2f;
            Material material = GetAdditiveMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return particleSystem;
        }

        private static Material GetAdditiveMaterial()
        {
            if (additiveParticleMaterial != null)
            {
                return additiveParticleMaterial;
            }

            Material template = Resources.Load<Material>(AdditiveParticleMaterialResourcePath);
            if (template != null)
            {
                additiveParticleMaterial = new Material(template)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                return additiveParticleMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                return null;
            }

            additiveParticleMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            additiveParticleMaterial.SetFloat("_Surface", 1f);
            additiveParticleMaterial.SetFloat("_Blend", 1f);
            additiveParticleMaterial.SetFloat("_SrcBlend", 1f);
            additiveParticleMaterial.SetFloat("_DstBlend", 1f);
            additiveParticleMaterial.SetFloat("_ZWrite", 0f);
            additiveParticleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            additiveParticleMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return additiveParticleMaterial;
        }

        private static Bounds ResolveBounds(Transform modelRoot)
        {
            if (BattleModelBoundsCache.TryResolveBounds(modelRoot, out Bounds bounds))
            {
                return bounds;
            }

            return new Bounds(modelRoot.position + Vector3.up * 1.1f, Vector3.one);
        }
    }
}
