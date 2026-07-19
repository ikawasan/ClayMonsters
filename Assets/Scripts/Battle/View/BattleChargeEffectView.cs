using Battle.Interface;
using Extensions;
using UnityEngine;

namespace Battle.View
{
    /// <summary>
    /// 攻撃溜め中にキャラ周囲へ力が溜まるオーラと光を表示する
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleChargeEffectView : MonoBehaviour, IBattleChargeEffect
    {
        private const string AdditiveParticleMaterialResourcePath = "Material/Battle/M_BattleHitParticleAdditive";

        [SerializeField] private float heightFallback = 1.1f;
        [SerializeField] private float baseRadius = 0.35f;
        [SerializeField] private Color chargeColor = new Color(1f, 0.72f, 0.28f, 1f);
        [SerializeField] private Color chargeHotColor = new Color(1f, 0.92f, 0.55f, 1f);

        private static Material additiveParticleMaterial;

        private Transform followRoot;
        private GameObject effectHost;
        private ParticleSystem auraParticles;
        private ParticleSystem sparkParticles;
        private ParticleSystem coreParticles;
        private Light chargeLight;
        private float playDuration = 1f;
        private float intensity;
        private bool isPlaying;

        /// <inheritdoc/>
        public void Play(Transform modelRoot, float duration)
        {
            Stop();
            if (modelRoot == null)
            {
                return;
            }

            followRoot = modelRoot;
            playDuration = Mathf.Max(0.05f, duration);
            intensity = 0f;
            EnsureEffectHost(modelRoot);
            SyncHostTransform();
            isPlaying = true;
            SetIntensity(0f);
            auraParticles?.Play(true);
            sparkParticles?.Play(true);
            coreParticles?.Play(true);
        }

        /// <inheritdoc/>
        public void SetIntensity(float value)
        {
            intensity = Mathf.Clamp01(value);
            if (!isPlaying || effectHost == null)
            {
                return;
            }

            ApplyAuraRate(auraParticles, 18f + intensity * 70f, 0.55f + intensity * 0.9f);
            ApplyAuraRate(sparkParticles, 8f + intensity * 42f, 0.35f + intensity * 0.75f);
            ApplyAuraRate(coreParticles, 6f + intensity * 28f, 0.45f + intensity * 0.8f);

            if (chargeLight != null)
            {
                chargeLight.intensity = 0.35f + intensity * 2.4f;
                chargeLight.range = 1.4f + intensity * 1.8f;
                chargeLight.color = Color.Lerp(chargeColor, chargeHotColor, intensity);
            }
        }

        /// <inheritdoc/>
        public void Stop()
        {
            isPlaying = false;
            followRoot = null;
            intensity = 0f;

            if (auraParticles != null)
            {
                auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (sparkParticles != null)
            {
                sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (coreParticles != null)
            {
                coreParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (chargeLight != null)
            {
                chargeLight.intensity = 0f;
            }

            if (effectHost != null)
            {
                Destroy(effectHost);
                effectHost = null;
                auraParticles = null;
                sparkParticles = null;
                coreParticles = null;
                chargeLight = null;
            }
        }

        private void LateUpdate()
        {
            if (!isPlaying)
            {
                return;
            }

            SyncHostTransform();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void EnsureEffectHost(Transform modelRoot)
        {
            effectHost = new GameObject("BattleChargeAura");
            effectHost.transform.SetParent(null, true);

            auraParticles = CreateChildSystem(effectHost.transform, "Aura");
            sparkParticles = CreateChildSystem(effectHost.transform, "Sparks");
            coreParticles = CreateChildSystem(effectHost.transform, "Core");

            var lightObject = new GameObject("ChargeLight");
            lightObject.transform.SetParent(effectHost.transform, false);
            lightObject.transform.localPosition = Vector3.up * 0.2f;
            chargeLight = lightObject.AddComponent<Light>();
            chargeLight.type = LightType.Point;
            chargeLight.shadows = LightShadows.None;
            chargeLight.intensity = 0f;
            chargeLight.range = 1.5f;
            chargeLight.color = chargeColor;

            Bounds bounds = ResolveBounds(modelRoot);
            float height = Mathf.Max(0.6f, bounds.size.y);
            float radius = Mathf.Max(baseRadius, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.85f);
            ConfigureAura(auraParticles, radius, height);
            ConfigureSparks(sparkParticles, radius);
            ConfigureCore(coreParticles, radius * 0.45f);
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

        private static void ApplyAuraRate(ParticleSystem system, float rate, float size)
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

        private void ConfigureAura(ParticleSystem system, float radius, float height)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.4f, playDuration);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(chargeColor, chargeHotColor);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.gravityModifier = -0.15f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 20f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = radius;
            shape.length = height * 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

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
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.75f, 0.25f),
                    new GradientAlphaKey(0.35f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.25f));

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-0.2f, 0.15f);
        }

        private void ConfigureSparks(ParticleSystem system, float radius)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = chargeHotColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.gravityModifier = -0.35f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 10f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius * 0.65f;

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
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;
        }

        private void ConfigureCore(ParticleSystem system, float radius)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
            main.startColor = new Color(chargeHotColor.r, chargeHotColor.g, chargeHotColor.b, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 8f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.35f));

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(chargeColor, 0f),
                    new GradientColorKey(chargeHotColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.55f, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;
        }

        private static ParticleSystem CreateChildSystem(Transform parent, string name)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = Vector3.zero;

            ParticleSystem particleSystem = host.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Material material = GetAdditiveMaterial();
            if (material != null)
            {
                renderer.material = material;
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
