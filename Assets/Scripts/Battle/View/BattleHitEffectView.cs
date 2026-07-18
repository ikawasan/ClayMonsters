using Battle.Interface;
using Extensions;
using UnityEngine;

namespace Battle.View
{
    /// <summary>
    /// 攻撃命中時にパーティクルバーストをワールド座標へ表示する
    /// ヒットストップ中だけ非スケール時間で再生しスローモーション時はゲーム側と同じ倍率で進める
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleHitEffectView : MonoBehaviour, IBattleHitEffect
    {
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private GameObject partBreakEffectPrefab;
        [SerializeField] private Material additiveParticleMaterialSource;
        [SerializeField] private Material alphaParticleMaterialSource;
        [SerializeField] private float heightFallback = 1.2f;

        private const string AdditiveParticleMaterialResourcePath = "Material/Battle/M_BattleHitParticleAdditive";
        private const string AlphaParticleMaterialResourcePath = "Material/Battle/M_BattleHitParticleAlpha";

        private static Material additiveParticleMaterial;
        private static Material alphaParticleMaterial;

        /// <inheritdoc/>
        public void PlayHit(Vector3 worldPosition, bool isPartBreak)
        {
            GameObject prefab = isPartBreak && partBreakEffectPrefab != null
                ? partBreakEffectPrefab
                : hitEffectPrefab;

            if (prefab != null)
            {
                SpawnPrefab(prefab, worldPosition);
            }

            SpawnProceduralBurst(worldPosition, isPartBreak);
        }

        /// <summary>
        /// モデルルートから命中演出の出し位置を求める
        /// </summary>
        public Vector3 ResolveWorldHitPoint(Transform modelRoot)
        {
            return ResolveWorldHitPoint(modelRoot, heightFallback);
        }

        /// <summary>
        /// モデルルートから命中演出の出し位置を求める
        /// </summary>
        public static Vector3 ResolveWorldHitPoint(Transform modelRoot, float fallbackHeight)
        {
            if (modelRoot == null)
            {
                return Vector3.zero;
            }

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

            return bounds.HasValue
                ? bounds.Value.center
                : modelRoot.position + Vector3.up * fallbackHeight;
        }

        private static void SpawnPrefab(GameObject prefab, Vector3 position)
        {
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            ParticleSystem particleSystem = instance.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
                particleSystem.Play(true);
                float lifetime = main.duration + main.startLifetime.constantMax;
                ScheduleUnscaledDestroy(instance, Mathf.Max(0.5f, lifetime));
                return;
            }

            ScheduleUnscaledDestroy(instance, 2f);
        }

        private static void SpawnProceduralBurst(Vector3 position, bool isPartBreak)
        {
            var host = new GameObject(isPartBreak ? "BattlePartBreakBurst" : "BattleHitBurst");
            host.transform.position = position;

            float destroyDelay = 0.2f;
            destroyDelay = Mathf.Max(destroyDelay, PlayCoreBloom(host.transform, isPartBreak));
            destroyDelay = Mathf.Max(destroyDelay, PlayImpactFlash(host.transform, isPartBreak));
            destroyDelay = Mathf.Max(destroyDelay, PlayShockRing(host.transform, isPartBreak));
            destroyDelay = Mathf.Max(destroyDelay, PlaySparkBurst(host.transform, isPartBreak));
            PlayHitLightFlash(host.transform, isPartBreak);

            if (isPartBreak)
            {
                destroyDelay = Mathf.Max(destroyDelay, PlayPartBreakDebris(host.transform));
            }

            ScheduleUnscaledDestroy(host, destroyDelay + 0.15f);
        }

        private static void ScheduleUnscaledDestroy(GameObject target, float delaySeconds)
        {
            if (target == null)
            {
                return;
            }

            var destroyer = target.AddComponent<UnscaledTimedDestroy>();
            destroyer.Schedule(delaySeconds);
        }

        private static float PlayCoreBloom(Transform parent, bool isPartBreak)
        {
            ParticleSystem particleSystem = CreateChildSystem(
                parent,
                "CoreBloom",
                ParticleSystemRenderMode.Billboard,
                useAdditive: true);
            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(isPartBreak ? 0.9f : 0.65f, isPartBreak ? 1.4f : 1f);
            main.maxParticles = isPartBreak ? 8 : 5;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.95f),
                new Color(1f, isPartBreak ? 0.5f : 0.85f, isPartBreak ? 0.15f : 0.35f, 0.75f));

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(isPartBreak ? 6 : 4)) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.04f;

            ApplyExpandSizeOverLifetime(particleSystem, isPartBreak ? 2.2f : 1.7f);
            ApplyQuickFadeColorOverLifetime(particleSystem);

            particleSystem.Play(true);
            return main.duration + main.startLifetime.constantMax;
        }

        private static float PlayImpactFlash(Transform parent, bool isPartBreak)
        {
            ParticleSystem particleSystem = CreateChildSystem(
                parent,
                "ImpactFlash",
                useAdditive: true);
            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(isPartBreak ? 11f : 8f, isPartBreak ? 18f : 13f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, isPartBreak ? 0.62f : 0.48f);
            main.maxParticles = isPartBreak ? 72 : 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.startColor = isPartBreak
                ? new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.55f, 0.2f, 1f))
                : new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.95f, 0.45f, 1f));

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(isPartBreak ? 56 : 36)) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = isPartBreak ? 0.26f : 0.18f;

            ApplyQuickFadeColorOverLifetime(particleSystem);
            ApplyShrinkSizeOverLifetime(particleSystem);

            particleSystem.Play(true);
            return main.duration + main.startLifetime.constantMax;
        }

        private static float PlayShockRing(Transform parent, bool isPartBreak)
        {
            ParticleSystem particleSystem = CreateChildSystem(
                parent,
                "ShockRing",
                useAdditive: true);
            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.18f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(isPartBreak ? 6.5f : 4.8f, isPartBreak ? 10.5f : 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, isPartBreak ? 0.4f : 0.3f);
            main.maxParticles = isPartBreak ? 48 : 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.startColor = isPartBreak
                ? new ParticleSystem.MinMaxGradient(new Color(1f, 0.42f, 0.12f, 1f), new Color(1f, 0.72f, 0.18f, 1f))
                : new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.2f, 1f), new Color(1f, 0.95f, 0.45f, 1f));

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(isPartBreak ? 38 : 24)) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = isPartBreak ? 0.32f : 0.24f;
            shape.radiusThickness = 1f;
            shape.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = new ParticleSystem.MinMaxCurve(isPartBreak ? 5.5f : 4f);

            ApplyExpandSizeOverLifetime(particleSystem, isPartBreak ? 1.8f : 1.45f);
            ApplyQuickFadeColorOverLifetime(particleSystem);

            particleSystem.Play(true);
            return main.duration + main.startLifetime.constantMax;
        }

        private static float PlaySparkBurst(Transform parent, bool isPartBreak)
        {
            ParticleSystem particleSystem = CreateChildSystem(
                parent,
                "Sparks",
                ParticleSystemRenderMode.Stretch,
                useAdditive: true);
            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.velocityScale = 0.1f;
            renderer.lengthScale = isPartBreak ? 2.8f : 2.1f;

            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.14f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(isPartBreak ? 9f : 7f, isPartBreak ? 16f : 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, isPartBreak ? 0.16f : 0.12f);
            main.gravityModifier = 1.1f;
            main.maxParticles = isPartBreak ? 44 : 28;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.95f, 0.65f, 1f),
                new Color(1f, 0.45f, 0.1f, 1f));

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(isPartBreak ? 34 : 22)) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.12f;

            ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

            ApplyQuickFadeColorOverLifetime(particleSystem);

            particleSystem.Play(true);
            return main.duration + main.startLifetime.constantMax;
        }

        private static void PlayHitLightFlash(Transform parent, bool isPartBreak)
        {
            var lightObject = new GameObject("HitLightFlash");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = Vector3.zero;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = isPartBreak ? 5.5f : 4f;
            light.intensity = isPartBreak ? 3.8f : 2.6f;
            light.color = isPartBreak
                ? new Color(1f, 0.45f, 0.15f)
                : new Color(1f, 0.82f, 0.35f);
            light.shadows = LightShadows.None;

            var flicker = lightObject.AddComponent<HitLightFlicker>();
            flicker.Configure(isPartBreak ? 0.14f : 0.1f, light.intensity);
        }

        private static float PlayPartBreakDebris(Transform parent)
        {
            ParticleSystem particleSystem = CreateChildSystem(parent, "PartBreakDebris");
            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.36f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = GameplayTime.UseUnscaledParticleTime;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.28f, 0.1f, 1f),
                new Color(0.85f, 0.12f, 0.08f, 1f));

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24), new ParticleSystem.Burst(0.04f, 12) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.24f;

            ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-240f, 240f);

            ApplyFadeColorOverLifetime(particleSystem);
            ApplyShrinkSizeOverLifetime(particleSystem);

            particleSystem.Play(true);
            return main.duration + main.startLifetime.constantMax;
        }

        private static ParticleSystem CreateChildSystem(
            Transform parent,
            string name,
            ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard,
            bool useAdditive = false)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = Vector3.zero;

            ParticleSystem particleSystem = host.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = renderMode;
            Material particleMaterial = GetParticleMaterial(useAdditive);
            if (particleMaterial != null)
            {
                renderer.material = particleMaterial;
            }

            return particleSystem;
        }

        private static Material GetParticleMaterial(bool useAdditive)
        {
            if (useAdditive)
            {
                if (additiveParticleMaterial == null)
                {
                    additiveParticleMaterial = CreateParticleMaterial(true);
                }

                return additiveParticleMaterial;
            }

            if (alphaParticleMaterial == null)
            {
                alphaParticleMaterial = CreateParticleMaterial(false);
            }

            return alphaParticleMaterial;
        }

        private static Material CreateParticleMaterial(bool additive)
        {
            Material template = ResolveParticleMaterialTemplate(additive);
            if (template != null)
            {
                var instance = new Material(template)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                return instance;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                Debug.LogWarning("[BattleHitEffectView] パーティクル用シェーダーが見つかりません");
                return null;
            }

            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 1f : 0f);
            material.SetFloat("_SrcBlend", additive ? 1f : 5f);
            material.SetFloat("_DstBlend", additive ? 1f : 10f);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        private static Material ResolveParticleMaterialTemplate(bool additive)
        {
            BattleHitEffectView view = Object.FindFirstObjectByType<BattleHitEffectView>(FindObjectsInactive.Include);
            if (view != null)
            {
                Material serialized = additive
                    ? view.additiveParticleMaterialSource
                    : view.alphaParticleMaterialSource;
                if (serialized != null)
                {
                    return serialized;
                }
            }

            string resourcePath = additive
                ? AdditiveParticleMaterialResourcePath
                : AlphaParticleMaterialResourcePath;
            return Resources.Load<Material>(resourcePath);
        }

        private static void ApplyQuickFadeColorOverLifetime(ParticleSystem particleSystem)
        {
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.65f, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;
        }

        private static void ApplyExpandSizeOverLifetime(ParticleSystem particleSystem, float endScale)
        {
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = AnimationCurve.EaseInOut(0f, 1f, 1f, endScale);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void ApplyFadeColorOverLifetime(ParticleSystem particleSystem)
        {
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;
        }

        private static void ApplyShrinkSizeOverLifetime(ParticleSystem particleSystem)
        {
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private sealed class HitLightFlicker : MonoBehaviour
        {
            private float duration;
            private float peakIntensity;
            private Light pointLight;
            private float elapsed;

            public void Configure(float lifetime, float intensity)
            {
                duration = lifetime;
                peakIntensity = intensity;
                pointLight = GetComponent<Light>();
            }

            private void Update()
            {
                elapsed += GameplayTime.PresentationDeltaTime;
                if (pointLight == null)
                {
                    return;
                }

                float normalized = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                float falloff = 1f - normalized;
                pointLight.intensity = peakIntensity * falloff * falloff;

                if (normalized >= 1f)
                {
                    Destroy(gameObject);
                }
            }
        }

        private sealed class UnscaledTimedDestroy : MonoBehaviour
        {
            private float remainingSeconds;

            public void Schedule(float delaySeconds)
            {
                remainingSeconds = Mathf.Max(0f, delaySeconds);
            }

            private void Update()
            {
                remainingSeconds -= GameplayTime.PresentationDeltaTime;
                if (remainingSeconds <= 0f)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
