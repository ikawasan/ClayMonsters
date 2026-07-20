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
        private const float CameraHeightOffset = 1.2f;

        private static Material sharedParticleMaterial;

        private GameObject root;
        private ParticleSystem particleSystem;

        /// <summary>
        /// 指定アンカー上方から紙吹雪の降下雪を開始する
        /// </summary>
        /// <param name="anchor">基準位置</param>
        public void Play(Transform anchor)
        {
            // 前回インスタンスが残るとシーン跨ぎで残留するため先に破棄する
            Dispose();
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
        /// 紙吹雪の発生を止め残粒子ごと破棄する
        /// </summary>
        public void Stop()
        {
            Dispose();
        }

        /// <summary>
        /// 生成した紙吹雪オブジェクトを即座に破棄する
        /// </summary>
        public void Dispose()
        {
            if (particleSystem != null)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem = null;
            }

            if (root != null)
            {
                Object.Destroy(root);
                root = null;
            }
        }

        private void EnsureCreated(Transform anchor)
        {
            if (root != null)
            {
                return;
            }

            root = new GameObject("VictoryConfetti");
            // シーンオブジェクト配下に置きアンロード時に確実に破棄する
            if (anchor != null)
            {
                root.transform.SetParent(anchor, false);
            }

            AlignToAnchor(anchor);

            particleSystem = root.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 6f;
            main.startDelay = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var startColor = new ParticleSystem.MinMaxGradient(BuildConfettiGradient())
            {
                mode = ParticleSystemGradientMode.RandomColor
            };
            main.startColor = startColor;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 320;
            main.useUnscaledTime = true;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(24f);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(EmitterWidth, 0.2f, EmitterDepth);
            shape.randomDirectionAmount = 0.08f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // XYZは同一CurveMode必須(TwoConstants)
            velocity.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.12f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = particleSystem.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.separateAxes = false;
            limitVelocity.limit = 1.1f;
            limitVelocity.dampen = 0.35f;
            limitVelocity.drag = 0.15f;

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = new ParticleSystem.MinMaxCurve(0.35f);
            noise.strengthY = new ParticleSystem.MinMaxCurve(0.12f);
            noise.strengthZ = new ParticleSystem.MinMaxCurve(0.28f);
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;
            noise.octaveCount = 2;
            noise.octaveMultiplier = 0.45f;
            noise.octaveScale = 1.8f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.positionAmount = 1f;
            noise.rotationAmount = 0.25f;

            ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);
            rotation.y = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);
            rotation.z = new ParticleSystem.MinMaxCurve(-0.85f, 0.85f);

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
                    new GradientAlphaKey(1f, 0.05f),
                    new GradientAlphaKey(1f, 0.7f),
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
                center.y = camera.transform.position.y + CameraHeightOffset;
            }

            // 親付きでもワールド位置で前方やや上方に置きシーン退場で親ごと破棄する
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
