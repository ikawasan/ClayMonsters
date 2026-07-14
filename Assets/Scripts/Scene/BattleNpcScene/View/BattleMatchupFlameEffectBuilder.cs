using UnityEngine;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// 対戦紹介用のVefects炎プレハブを配置して背景演出を構築する
    /// </summary>
    public static class BattleMatchupFlameEffectBuilder
    {
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorBottomPropertyId = Shader.PropertyToID("_ColorBottom");
        private static readonly int ColorTopPropertyId = Shader.PropertyToID("_ColorTop");
        private static readonly int ColorDownPropertyId = Shader.PropertyToID("_ColorDown");
        private static readonly int ColorUpPropertyId = Shader.PropertyToID("_ColorUp");
        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionIntensityPropertyId = Shader.PropertyToID("_EmissionIntensity");
        private static readonly int EmissiveIntensityPropertyId = Shader.PropertyToID("_EmissiveIntensity");
        private static readonly int OpacityBoostPropertyId = Shader.PropertyToID("_OpacityBoost");
        private static readonly int OpacityBoostAltPropertyId = Shader.PropertyToID("_Opacity_Boost");

        /// <summary>
        /// 片側の炎エフェクト一式
        /// </summary>
        public sealed class SideFlameEffect
        {
            public Transform Root;
            private ParticleSystem[] particleSystems;

            /// <summary>
            /// 再生する
            /// </summary>
            public void Play()
            {
                PlaySystems(particleSystems);
            }

            /// <summary>
            /// 停止する
            /// </summary>
            public void Stop()
            {
                StopSystems(particleSystems);
            }

            /// <summary>
            /// 子階層のパーティクルを収集する
            /// </summary>
            /// <param name="root">収集元ルート</param>
            public void CaptureSystems(Transform root)
            {
                particleSystems = root != null
                    ? root.GetComponentsInChildren<ParticleSystem>(true)
                    : null;
            }
        }

        /// <summary>
        /// 中央衝突の炎エフェクト
        /// </summary>
        public sealed class ClashFlameEffect
        {
            public Transform Root;
            private ParticleSystem[] particleSystems;

            /// <summary>
            /// 再生する
            /// </summary>
            public void Play()
            {
                PlaySystems(particleSystems);
            }

            /// <summary>
            /// 停止する
            /// </summary>
            public void Stop()
            {
                StopSystems(particleSystems);
            }

            /// <summary>
            /// 子階層のパーティクルを収集する
            /// </summary>
            /// <param name="root">収集元ルート</param>
            public void CaptureSystems(Transform root)
            {
                particleSystems = root != null
                    ? root.GetComponentsInChildren<ParticleSystem>(true)
                    : null;
            }
        }

        /// <summary>
        /// 自分側の赤炎エフェクトを構築する
        /// </summary>
        public static SideFlameEffect BuildPlayerFlame(
            BattleMatchupFlameEffectSettings settings,
            Transform parent,
            Vector3 localPosition,
            float inwardTilt)
        {
            return BuildSideFlame(
                settings,
                settings.PlayerFlamePrefab,
                parent,
                localPosition,
                "MatchupFlamePlayer",
                inwardTilt,
                settings.PlayerFlameTint,
                settings.PlayerLightIntensity);
        }

        /// <summary>
        /// 敵側の青炎エフェクトを構築する
        /// </summary>
        public static SideFlameEffect BuildEnemyFlame(
            BattleMatchupFlameEffectSettings settings,
            Transform parent,
            Vector3 localPosition,
            float inwardTilt)
        {
            return BuildSideFlame(
                settings,
                settings.EnemyFlamePrefab,
                parent,
                localPosition,
                "MatchupFlameEnemy",
                inwardTilt,
                settings.EnemyFlameTint,
                settings.EnemyLightIntensity);
        }

        /// <summary>
        /// 中央衝突の炎エフェクトを構築する
        /// </summary>
        public static ClashFlameEffect BuildClashFlame(
            BattleMatchupFlameEffectSettings settings,
            Transform parent,
            Vector3 localPosition)
        {
            var effect = new ClashFlameEffect();
            GameObject host = CreateHost(parent, "MatchupFlameClash", localPosition);
            effect.Root = host.transform;

            if (settings.ClashFlamePrefab != null)
            {
                GameObject clashInstance = SpawnPrefab(
                    settings.ClashFlamePrefab,
                    host.transform,
                    settings.ClashFlameLocalOffset,
                    Quaternion.identity,
                    Vector3.one * settings.ClashFlameScale);
                ApplyFlameTint(clashInstance, settings, settings.ClashFlameTint, settings.ClashLightIntensity, 0.55f);
                DisableSmokeChildren(clashInstance);
            }

            effect.CaptureSystems(host.transform);
            return effect;
        }

        private static SideFlameEffect BuildSideFlame(
            BattleMatchupFlameEffectSettings settings,
            GameObject flamePrefab,
            Transform parent,
            Vector3 localPosition,
            string rootName,
            float inwardTilt,
            Color tint,
            float lightIntensity)
        {
            var effect = new SideFlameEffect();
            GameObject host = CreateHost(parent, rootName, localPosition);
            effect.Root = host.transform;
            host.transform.localRotation = Quaternion.Euler(0f, inwardTilt, 0f);

            if (flamePrefab == null)
            {
                return effect;
            }

            Vector3 flameScale = Vector3.Scale(settings.SideFlameLocalScale, Vector3.one * settings.SideFlameScale);
            GameObject flameInstance = SpawnPrefab(
                flamePrefab,
                host.transform,
                settings.SideFlameLocalOffset,
                Quaternion.identity,
                flameScale);
            DisableSmokeChildren(flameInstance);
            ApplyFlameTint(flameInstance, settings, tint, lightIntensity, 1f);

            effect.CaptureSystems(host.transform);
            return effect;
        }

        private static GameObject CreateHost(Transform parent, string name, Vector3 localPosition)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = localPosition;
            host.transform.localRotation = Quaternion.identity;
            host.transform.localScale = Vector3.one;
            return host;
        }

        private static GameObject SpawnPrefab(
            GameObject prefab,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 scale)
        {
            GameObject instance = Object.Instantiate(prefab, parent);
            instance.name = prefab.name;
            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = localPosition;
            instanceTransform.localRotation = localRotation;
            instanceTransform.localScale = scale;
            return instance;
        }

        private static void DisableSmokeChildren(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms)
            {
                if (child == instance.transform)
                {
                    continue;
                }

                if (child.name.IndexOf("Smoke", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || child.name.IndexOf("Distortion", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static void ApplyFlameTint(
            GameObject instance,
            BattleMatchupFlameEffectSettings settings,
            Color tint,
            float lightIntensityMultiplier,
            float particleColorWeight)
        {
            ResolveFlameGradient(tint, out Color colorBottom, out Color colorTop);
            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particleSystem in particleSystems)
            {
                ApplyParticleTint(particleSystem, tint, colorBottom, colorTop, particleColorWeight);
            }

            ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (ParticleSystemRenderer renderer in renderers)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.lengthScale = 1f;
                renderer.velocityScale = 0f;
                Material material = renderer.material;
                ApplyMaterialTint(material, tint, colorBottom, colorTop, settings);
            }

            Color lightColor = IsBlueDominantTint(tint) ? colorTop : tint;
            Light[] lights = instance.GetComponentsInChildren<Light>(true);
            foreach (Light light in lights)
            {
                light.color = lightColor;
                light.intensity *= lightIntensityMultiplier * settings.FlameIntensityMultiplier;
            }

            BoostParticleIntensity(instance, settings);
        }

        private static void ApplyParticleTint(
            ParticleSystem particleSystem,
            Color tint,
            Color colorBottom,
            Color colorTop,
            float particleColorWeight)
        {
            if (particleSystem == null)
            {
                return;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            if (particleColorWeight > 0f)
            {
                main.startColor = Color.Lerp(Color.white, tint, particleColorWeight);
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            if (!colorOverLifetime.enabled)
            {
                return;
            }

            Color midColor = Color.Lerp(colorBottom, colorTop, 0.42f);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(colorBottom, 0f),
                    new GradientColorKey(midColor, 0.38f),
                    new GradientColorKey(colorTop, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.82f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static bool IsBlueDominantTint(Color tint)
        {
            return tint.b > tint.r && tint.b > tint.g;
        }

        private static void ResolveFlameGradient(Color tint, out Color colorBottom, out Color colorTop)
        {
            if (IsBlueDominantTint(tint))
            {
                bool isLightBlue = tint.g > tint.r + 0.2f;
                if (isLightBlue)
                {
                    colorBottom = new Color(
                        Mathf.Clamp01(tint.r * 0.42f),
                        Mathf.Clamp01(tint.g * 0.52f),
                        Mathf.Clamp01(tint.b * 0.72f),
                        1f);
                    colorTop = new Color(
                        Mathf.Clamp01(tint.r * 0.55f + 0.1f),
                        Mathf.Clamp01(tint.g * 1.05f + 0.08f),
                        Mathf.Clamp01(Mathf.Min(tint.b * 1.08f + 0.05f, 1f)),
                        1f);
                    return;
                }

                colorBottom = new Color(
                    Mathf.Clamp01(tint.r * 0.35f),
                    Mathf.Clamp01(tint.g * 0.06f),
                    Mathf.Clamp01(tint.b * 0.62f),
                    1f);
                colorTop = new Color(
                    Mathf.Clamp01(tint.r * 0.22f + 0.04f),
                    Mathf.Clamp01(tint.g * 0.1f),
                    Mathf.Clamp01(Mathf.Max(tint.b * 1.28f, 0.58f)),
                    1f);
                return;
            }

            colorBottom = new Color(
                Mathf.Clamp01(tint.r * 0.72f),
                Mathf.Clamp01(tint.g * 0.22f),
                Mathf.Clamp01(tint.b * 0.18f),
                1f);
            colorTop = new Color(
                Mathf.Clamp01(tint.r * 1.2f),
                Mathf.Clamp01(tint.g * 0.95f + 0.1f),
                Mathf.Clamp01(tint.b * 1.05f + 0.05f),
                1f);
        }

        private static void BoostParticleIntensity(GameObject instance, BattleMatchupFlameEffectSettings settings)
        {
            ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particleSystem in particleSystems)
            {
                ParticleSystem.MainModule main = particleSystem.main;
                MultiplyMinMaxCurve(main.startSize, settings.ParticleSizeMultiplier, value => main.startSize = value);
                MultiplyMinMaxCurve(
                    main.startSpeed,
                    1f + (settings.SimulationSpeedMultiplier - 1f) * 0.35f,
                    value => main.startSpeed = value);

                ParticleSystem.MinMaxCurve simulationSpeed = main.simulationSpeed;
                if (simulationSpeed.mode == ParticleSystemCurveMode.Constant)
                {
                    main.simulationSpeed = simulationSpeed.constant * settings.SimulationSpeedMultiplier;
                }
                else
                {
                    main.simulationSpeed = simulationSpeed.constantMax > 0f
                        ? simulationSpeed.constantMax * settings.SimulationSpeedMultiplier
                        : settings.SimulationSpeedMultiplier;
                }

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.rateOverTimeMultiplier *= settings.ParticleEmissionMultiplier;
            }
        }

        private static void MultiplyMinMaxCurve(
            ParticleSystem.MinMaxCurve curve,
            float multiplier,
            System.Action<ParticleSystem.MinMaxCurve> assign)
        {
            if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
            {
                return;
            }

            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    assign(new ParticleSystem.MinMaxCurve(curve.constant * multiplier));
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    assign(new ParticleSystem.MinMaxCurve(curve.constantMin * multiplier, curve.constantMax * multiplier));
                    break;
                default:
                    break;
            }
        }

        private static void ApplyMaterialTint(
            Material material,
            Color tint,
            Color colorBottom,
            Color colorTop,
            BattleMatchupFlameEffectSettings settings)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(ColorBottomPropertyId))
            {
                material.SetColor(ColorBottomPropertyId, colorBottom);
            }

            if (material.HasProperty(ColorTopPropertyId))
            {
                material.SetColor(ColorTopPropertyId, colorTop);
            }

            if (material.HasProperty(ColorDownPropertyId))
            {
                material.SetColor(ColorDownPropertyId, colorBottom);
            }

            if (material.HasProperty(ColorUpPropertyId))
            {
                material.SetColor(ColorUpPropertyId, colorTop);
            }

            if (material.HasProperty(ColorPropertyId))
            {
                Color currentColor = material.GetColor(ColorPropertyId);
                material.SetColor(
                    ColorPropertyId,
                    new Color(tint.r, tint.g, tint.b, currentColor.a > 0.001f ? currentColor.a : tint.a));
            }

            if (material.HasProperty(BaseColorPropertyId))
            {
                material.SetColor(BaseColorPropertyId, tint);
            }

            if (material.HasProperty(EmissionColorPropertyId))
            {
                material.SetColor(EmissionColorPropertyId, new Color(tint.r, tint.g, tint.b, 1f));
            }

            if (material.HasProperty(EmissionIntensityPropertyId))
            {
                material.SetFloat(
                    EmissionIntensityPropertyId,
                    material.GetFloat(EmissionIntensityPropertyId) * settings.FlameIntensityMultiplier);
            }

            if (material.HasProperty(EmissiveIntensityPropertyId))
            {
                material.SetFloat(
                    EmissiveIntensityPropertyId,
                    material.GetFloat(EmissiveIntensityPropertyId) * settings.FlameIntensityMultiplier);
            }

            if (material.HasProperty(OpacityBoostPropertyId))
            {
                material.SetFloat(
                    OpacityBoostPropertyId,
                    material.GetFloat(OpacityBoostPropertyId) * settings.MaterialOpacityBoost);
            }

            if (material.HasProperty(OpacityBoostAltPropertyId))
            {
                material.SetFloat(
                    OpacityBoostAltPropertyId,
                    material.GetFloat(OpacityBoostAltPropertyId) * settings.MaterialOpacityBoost);
            }
        }

        private static void PlaySystems(ParticleSystem[] particleSystems)
        {
            if (particleSystems == null)
            {
                return;
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem != null)
                {
                    particleSystem.Play(true);
                }
            }
        }

        private static void StopSystems(ParticleSystem[] particleSystems)
        {
            if (particleSystems == null)
            {
                return;
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem != null)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }
}
