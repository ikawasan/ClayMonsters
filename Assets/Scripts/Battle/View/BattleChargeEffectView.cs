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
        private const int ParticleBufferSize = 512;
        private const float FadeOutDuration = 0.28f;
        // 攻撃開始前に発生を止めフェードへ入る余裕
        private const float StopBeforeWindUpEnd = 0.42f;

        /// <summary>
        /// 溜めエフェクト発生を攻撃直前に止める秒数
        /// </summary>
        public static float ChargeEmissionLeadOutSeconds => StopBeforeWindUpEnd;

        [SerializeField] private float heightFallback = 1.1f;
        [SerializeField] private float baseAbsorbRadius = 1.2f;
        [SerializeField] private float maxAbsorbRadius = 1.6f;
        [SerializeField] private Color chargeColor = new Color(1f, 0.72f, 0.28f, 1f);
        [SerializeField] private Color chargeHotColor = new Color(1f, 0.92f, 0.55f, 1f);

        private static Material additiveParticleMaterial;
        private static ParticleSystem.Particle[] particleBuffer;

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
            ApplyEmission(absorbParticles, 90f + intensity * 160f, 0.2f + intensity * 0.18f);
            ApplyEmission(sparkParticles, 60f + intensity * 140f, 0.08f + intensity * 0.1f);
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
            intensity = 0f;
            StopEmitting(absorbParticles);
            StopEmitting(sparkParticles);
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
                float stopAt = Mathf.Max(0f, playDuration - StopBeforeWindUpEnd);
                if (playElapsed >= stopAt)
                {
                    BeginFadeOut();
                }
            }

            if (!isPlaying && !isFadingOut)
            {
                return;
            }

            float pullScale = isFadingOut ? 0.65f : 1f;
            PullParticlesToCenter(absorbParticles, pullSpeed * pullScale, orbitSpeed * pullScale, deltaTime);
            PullParticlesToCenter(sparkParticles, pullSpeed * 1.25f * pullScale, orbitSpeed * 1.2f * pullScale, deltaTime);

            if (!isFadingOut)
            {
                return;
            }

            fadeOutElapsed += deltaTime;
            float fade = 1f - Mathf.Clamp01(fadeOutElapsed / FadeOutDuration);
            float previousFade = 1f - Mathf.Clamp01((fadeOutElapsed - deltaTime) / FadeOutDuration);
            ApplyFadeAlpha(absorbParticles, fade, previousFade);
            ApplyFadeAlpha(sparkParticles, fade, previousFade);

            int remaining =
                (absorbParticles != null ? absorbParticles.particleCount : 0) +
                (sparkParticles != null ? sparkParticles.particleCount : 0);
            if (fade <= 0f || remaining <= 0)
            {
                DestroyEffectImmediate();
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

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = false;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = false;
        }

        private static void ApplyFadeAlpha(ParticleSystem system, float alphaScale, float previousAlphaScale)
        {
            if (system == null)
            {
                return;
            }

            int count = system.particleCount;
            if (count <= 0)
            {
                return;
            }

            if (particleBuffer == null || particleBuffer.Length < count)
            {
                particleBuffer = new ParticleSystem.Particle[Mathf.Max(ParticleBufferSize, count)];
            }

            int read = system.GetParticles(particleBuffer);
            float clamped = Mathf.Clamp01(alphaScale);
            float previous = Mathf.Max(0.0001f, previousAlphaScale);
            float ratio = Mathf.Clamp01(clamped / previous);
            float sizeRatio = Mathf.Lerp(0.92f, 1f, ratio);
            for (int i = 0; i < read; i++)
            {
                Color32 color = particleBuffer[i].startColor;
                color.a = (byte)Mathf.RoundToInt(color.a * ratio);
                particleBuffer[i].startColor = color;
                particleBuffer[i].startSize *= sizeRatio;
            }

            system.SetParticles(particleBuffer, read);
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

        private static void PullParticlesToCenter(
            ParticleSystem system,
            float inwardSpeed,
            float swirl,
            float deltaTime)
        {
            if (system == null || !system.isPlaying)
            {
                return;
            }

            int count = system.particleCount;
            if (count <= 0)
            {
                return;
            }

            if (particleBuffer == null || particleBuffer.Length < count)
            {
                particleBuffer = new ParticleSystem.Particle[Mathf.Max(ParticleBufferSize, count)];
            }

            int read = system.GetParticles(particleBuffer);
            float dt = Mathf.Max(0.0001f, deltaTime);
            for (int i = 0; i < read; i++)
            {
                Vector3 position = particleBuffer[i].position;
                float distance = position.magnitude;
                if (distance < 0.02f)
                {
                    // 中心到達後は残寿命を短くして消す
                    particleBuffer[i].remainingLifetime = Mathf.Min(particleBuffer[i].remainingLifetime, 0.05f);
                    particleBuffer[i].velocity = Vector3.zero;
                    continue;
                }

                Vector3 inward = -position / distance;
                Vector3 tangent = Vector3.Cross(Vector3.up, inward);
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = Vector3.Cross(Vector3.right, inward);
                }

                tangent.Normalize();

                // 残り寿命で中心付近へ届く速度をベースに強度を足す
                float arriveSpeed = distance / Mathf.Max(0.08f, particleBuffer[i].remainingLifetime);
                float speed = Mathf.Max(inwardSpeed, arriveSpeed);
                Vector3 velocity = inward * speed + tangent * (swirl * Mathf.Clamp01(distance));
                particleBuffer[i].velocity = velocity;

                // 1フレ分も寄せて収束をはっきり見せる
                particleBuffer[i].position = position + inward * (speed * dt * 0.35f);
            }

            system.SetParticles(particleBuffer, read);
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
            main.maxParticles = 400;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 90f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0f;

            // 速度はLateUpdateで中心方向へ上書きする
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = false;

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
            main.maxParticles = 280;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 65f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0.05f;

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = false;

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
            Bounds? bounds = null;
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!bounds.HasValue)
                {
                    bounds = renderer.bounds;
                    continue;
                }

                Bounds combined = bounds.Value;
                combined.Encapsulate(renderer.bounds);
                bounds = combined;
            }

            return bounds ?? new Bounds(modelRoot.position + Vector3.up * 1.1f, Vector3.one);
        }
    }
}
