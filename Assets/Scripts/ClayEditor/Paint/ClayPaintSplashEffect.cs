using ClayEditor.Paint.Interface;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ClayEditor.Paint
{
    /// <summary>
    /// 塗った周囲へインク飛沫パーティクルを出す
    /// </summary>
    public sealed class ClayPaintSplashEffect : IClayPaintSplashEffect, IDisposable
    {
        private const string AlphaParticleMaterialResourcePath = "Material/Battle/M_BattleHitParticleAlpha";
        private const int PoolCapacity = 8;

        private readonly Stack<GameObject> free = new Stack<GameObject>(PoolCapacity);
        private readonly CancellationTokenSource lifetimeCts = new CancellationTokenSource();
        private readonly GameObject poolRoot;
        private Material inkMaterial;
        private bool disposed;

        /// <summary>
        /// 飛沫プールを用意する
        /// </summary>
        public ClayPaintSplashEffect()
        {
            poolRoot = new GameObject("ClayPaintSplash_Root")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            poolRoot.transform.position = new Vector3(0f, -10000f, 0f);
            for (int i = 0; i < PoolCapacity; i++)
            {
                free.Push(CreateHost());
            }
        }

        /// <inheritdoc/>
        public void Play(Vector3 worldPosition, Vector3 worldNormal, Color color, float brushRadius)
        {
            if (disposed)
            {
                return;
            }

            Vector3 normal = worldNormal.sqrMagnitude > 1e-6f ? worldNormal.normalized : Vector3.up;
            float radius = Mathf.Max(0.02f, brushRadius);
            Color ink = PaintColorUtility.ToMaterialColor(color);
            ink.a = 1f;

            GameObject host = Rent(worldPosition + normal * (radius * 0.08f), normal);
            if (host == null)
            {
                return;
            }

            float lifetime = 0.2f;
            lifetime = Mathf.Max(lifetime, PlayDroplets(host.transform, ink, radius));
            lifetime = Mathf.Max(lifetime, PlaySpecks(host.transform, ink, radius));
            lifetime = Mathf.Max(lifetime, PlayRing(host.transform, ink, radius));
            ReleaseAfterAsync(host, lifetime + 0.12f, lifetimeCts.Token).Forget();
        }

        /// <summary>
        /// 飛沫プールを破棄する
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            lifetimeCts.Cancel();
            lifetimeCts.Dispose();
            if (poolRoot != null)
            {
                UnityEngine.Object.Destroy(poolRoot);
            }

            if (inkMaterial != null)
            {
                UnityEngine.Object.Destroy(inkMaterial);
                inkMaterial = null;
            }
        }

        private GameObject Rent(Vector3 worldPosition, Vector3 worldNormal)
        {
            GameObject host = free.Count > 0 ? free.Pop() : CreateHost();
            if (host == null)
            {
                return null;
            }

            Transform transform = host.transform;
            transform.SetParent(null, false);
            transform.SetPositionAndRotation(worldPosition, Quaternion.LookRotation(worldNormal));
            host.SetActive(true);
            return host;
        }

        private void Release(GameObject host)
        {
            if (host == null || disposed)
            {
                if (host != null)
                {
                    UnityEngine.Object.Destroy(host);
                }

                return;
            }

            ParticleSystem[] systems = host.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            host.SetActive(false);
            host.transform.SetParent(poolRoot.transform, false);
            free.Push(host);
        }

        private async UniTaskVoid ReleaseAfterAsync(GameObject host, float delaySeconds, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0.05f, delaySeconds)), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                Release(host);
                return;
            }

            Release(host);
        }

        private GameObject CreateHost()
        {
            var host = new GameObject("ClayPaintSplash");
            host.transform.SetParent(poolRoot.transform, false);
            host.SetActive(false);
            return host;
        }

        private float PlayDroplets(Transform parent, Color ink, float radius)
        {
            ParticleSystem particleSystem = GetOrCreateChild(parent, "Droplets", ParticleSystemRenderMode.Billboard);
            Color dark = Color.Lerp(ink, Color.black, 0.22f);
            dark.a = 1f;

            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.4f, radius * 3.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.06f, radius * 0.28f),
                Mathf.Max(0.12f, radius * 0.55f));
            main.gravityModifier = 1.2f;
            main.maxParticles = 20;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(ink, dark);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            short count = (short)Mathf.Clamp(6 + radius * 16f, 8, 14);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius * 0.45f;
            shape.radiusThickness = 0.7f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            SetVelocityTwoConstants(velocity, 0f, 0f, 0f, 0f, radius * 0.6f, radius * 1.8f);

            ApplyFade(particleSystem);
            ApplyShrink(particleSystem);
            particleSystem.Play(true);
            return main.duration + main.startLifetime.constantMax;
        }

        private float PlaySpecks(Transform parent, Color ink, float radius)
        {
            ParticleSystem particleSystem = GetOrCreateChild(parent, "Specks", ParticleSystemRenderMode.Billboard);
            Color bright = Color.Lerp(ink, Color.white, 0.18f);
            bright.a = 1f;

            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.7f, radius * 2.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.035f, radius * 0.12f),
                Mathf.Max(0.07f, radius * 0.28f));
            main.gravityModifier = 0.9f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(ink, bright);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            short count = (short)Mathf.Clamp(8 + radius * 20f, 10, 18);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.95f;
            shape.radiusThickness = 0.55f;
            shape.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            SetVelocityTwoConstants(velocity, 0f, 0f, 0f, 0f, radius * 0.2f, radius * 0.9f);
            velocity.radial = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 2.2f);

            ApplyFade(particleSystem);
            particleSystem.Play(true);
            return main.duration + main.startLifetime.constantMax;
        }

        private float PlayRing(Transform parent, Color ink, float radius)
        {
            ParticleSystem particleSystem = GetOrCreateChild(parent, "Ring", ParticleSystemRenderMode.Billboard);
            Color splash = ink;
            splash.a = 0.85f;

            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.08f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.5f, radius * 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.05f, radius * 0.18f),
                Mathf.Max(0.1f, radius * 0.38f));
            main.gravityModifier = 0.2f;
            main.maxParticles = 18;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = splash;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            short count = (short)Mathf.Clamp(6 + radius * 12f, 7, 12);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.7f;
            shape.radiusThickness = 0.2f;
            shape.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            SetVelocityTwoConstants(velocity, 0f, 0f, 0f, 0f, 0f, 0f);
            velocity.radial = new ParticleSystem.MinMaxCurve(radius * 1.2f, radius * 2.6f);

            ApplyFade(particleSystem);
            ApplyExpand(particleSystem, 1.6f);
            particleSystem.Play(true);
            return main.duration + main.startLifetime.constantMax;
        }

        private ParticleSystem GetOrCreateChild(Transform parent, string name, ParticleSystemRenderMode renderMode)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                ParticleSystem existingSystem = existing.GetComponent<ParticleSystem>();
                if (existingSystem != null)
                {
                    existingSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    return existingSystem;
                }
            }

            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = Vector3.zero;
            ParticleSystem particleSystem = host.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = renderMode;
            Material material = ResolveInkMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return particleSystem;
        }

        private Material ResolveInkMaterial()
        {
            if (inkMaterial != null)
            {
                return inkMaterial;
            }

            Material template = Resources.Load<Material>(AlphaParticleMaterialResourcePath);
            if (template == null)
            {
                Debug.LogError("[ClayPaintSplashEffect] Material/Battle/M_BattleHitParticleAlphaがありません");
                return null;
            }

            inkMaterial = new Material(template)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return inkMaterial;
        }

        /// <summary>
        /// VelocityOverLifetimeのXYZを同じTwoConstantsモードで設定する
        /// </summary>
        private static void SetVelocityTwoConstants(
            ParticleSystem.VelocityOverLifetimeModule velocity,
            float xMin,
            float xMax,
            float yMin,
            float yMax,
            float zMin,
            float zMax)
        {
            velocity.x = new ParticleSystem.MinMaxCurve(xMin, xMax);
            velocity.y = new ParticleSystem.MinMaxCurve(yMin, yMax);
            velocity.z = new ParticleSystem.MinMaxCurve(zMin, zMax);
        }

        private static void ApplyFade(ParticleSystem particleSystem)
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
                    new GradientAlphaKey(0.75f, 0.28f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;
        }

        private static void ApplyShrink(ParticleSystem particleSystem)
        {
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.45f));
        }

        private static void ApplyExpand(ParticleSystem particleSystem, float endScale)
        {
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, endScale));
        }
    }
}
