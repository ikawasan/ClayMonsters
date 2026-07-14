using UnityEngine;
using UnityEngine.Rendering;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// 勝利演出向けの紙吹雪パーティクルを生成し制御する
    /// </summary>
    public sealed class BattleVictoryConfettiEffect
    {
        private const string AlphaParticleMaterialResourcePath = "Material/Battle/M_BattleHitParticleAlpha";
        private const float EmitterHeight = 6.5f;
        private const float EmitterWidth = 10f;
        private const float EmitterDepth = 4.5f;

        private static Material sharedParticleMaterial;

        private GameObject root;
        private ParticleSystem particleSystem;

        /// <summary>
        /// 指定アンカー上方から紙吹雪の降下雪を開始する
        /// </summary>
        /// <param name="anchor">基準位置</param>
        public void Play(Transform anchor)
        {
            EnsureCreated(anchor);
            if (particleSystem == null)
            {
                return;
            }

            AlignToAnchor(anchor);
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        /// <summary>
        /// 紙吹雪の発生を止め残粒子は消えさせる
        /// </summary>
        public void Stop()
        {
            if (particleSystem == null)
            {
                return;
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>
        /// 生成した紙吹雪オブジェクトを破棄する
        /// </summary>
        public void Dispose()
        {
            Stop();
            if (root != null)
            {
                Object.Destroy(root);
                root = null;
                particleSystem = null;
            }
        }

        private void EnsureCreated(Transform anchor)
        {
            if (root != null)
            {
                return;
            }

            root = new GameObject("VictoryConfetti");
            AlignToAnchor(anchor);

            particleSystem = root.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5.5f, 8.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var startColor = new ParticleSystem.MinMaxGradient(BuildConfettiGradient())
            {
                mode = ParticleSystemGradientMode.RandomColor
            };
            main.startColor = startColor;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 280;
            main.useUnscaledTime = true;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(35f);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(EmitterWidth, 0.2f, EmitterDepth);
            shape.randomDirectionAmount = 0.08f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            rotation.y = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            rotation.z = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alphaGradient = new Gradient();
            alphaGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.08f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGradient);

            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 50;
            Material material = ResolveParticleMaterial();
            if (material != null)
            {
                renderer.material = material;
            }
        }

        private void AlignToAnchor(Transform anchor)
        {
            if (root == null)
            {
                return;
            }

            Vector3 center = anchor != null ? anchor.position : Vector3.zero;
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera != null)
            {
                Vector3 forward = camera.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-4f)
                {
                    forward = Vector3.forward;
                }

                forward.Normalize();
                center = camera.transform.position + forward * 4.5f;
                center.y = camera.transform.position.y + 1.2f;
            }

            root.transform.position = center + Vector3.up * EmitterHeight;
            root.transform.rotation = Quaternion.identity;
        }

        private static Gradient BuildConfettiGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.25f, 0.35f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.25f),
                    new GradientColorKey(new Color(0.25f, 0.85f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.45f, 1f, 0.4f), 0.75f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.9f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Material ResolveParticleMaterial()
        {
            if (sharedParticleMaterial != null)
            {
                return sharedParticleMaterial;
            }

            Material template = Resources.Load<Material>(AlphaParticleMaterialResourcePath);
            if (template != null)
            {
                sharedParticleMaterial = new Material(template)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                return sharedParticleMaterial;
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

            sharedParticleMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = Color.white
            };
            if (sharedParticleMaterial.HasProperty("_Surface"))
            {
                sharedParticleMaterial.SetFloat("_Surface", 1f);
            }

            if (sharedParticleMaterial.HasProperty("_Blend"))
            {
                sharedParticleMaterial.SetFloat("_Blend", 0f);
            }

            return sharedParticleMaterial;
        }
    }
}
