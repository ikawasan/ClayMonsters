using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// 勝利演出向けの紙吹雪パーティクルを生成し制御する
    /// 薄い紙片メッシュを翻転させながら降らせる
    /// </summary>
    public sealed class BattleVictoryConfettiEffect
    {
        private const string AlphaParticleMaterialResourcePath = "Material/Battle/M_BattleHitParticleAlpha";
        private const float EmitterHeight = 6.5f;
        private const float EmitterDepth = 5f;
        private const float EmitterViewDistance = 6f;
        private const float EmitterTopMargin = 0.75f;
        private const float EmitterSideMargin = 2.4f;

        private static Material sharedParticleMaterial;
        private static Mesh sharedChipMesh;

        private GameObject root;
        private ParticleSystem chipSystem;

        /// <summary>
        /// 指定アンカー上方から紙吹雪の降下雪を開始する
        /// </summary>
        /// <param name="anchor">基準位置</param>
        public void Play(Transform anchor)
        {
            // 前回インスタンスが残るとシーン跨ぎで残留するため先に破棄する
            Dispose();
            EnsureCreated(anchor);
            if (chipSystem == null)
            {
                return;
            }

            AlignToAnchor(anchor);
            PlaySystem(chipSystem);
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
            StopSystem(ref chipSystem);

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
            // UIキャンバス配下だと駆動スケールで粒子が潰れるため親付けせずシーンだけ合わせる
            if (anchor != null && anchor.gameObject.scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(root, anchor.gameObject.scene);
            }

            AlignToAnchor(anchor);

            chipSystem = CreateLayer(
                "Chips",
                ResolveChipMesh(),
                emissionRate: 48f,
                maxParticles: 480,
                lifetimeMin: 5.5f,
                lifetimeMax: 9f,
                sizeX: new Vector2(0.12f, 0.24f),
                sizeY: new Vector2(0.015f, 0.03f),
                sizeZ: new Vector2(0.16f, 0.32f),
                gravityMin: 0.18f,
                gravityMax: 0.34f);
        }

        private ParticleSystem CreateLayer(
            string name,
            Mesh mesh,
            float emissionRate,
            int maxParticles,
            float lifetimeMin,
            float lifetimeMax,
            Vector2 sizeX,
            Vector2 sizeY,
            Vector2 sizeZ,
            float gravityMin,
            float gravityMax)
        {
            var host = new GameObject(name);
            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;

            ParticleSystem particleSystem = host.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.startDelay = 0f;
            // 演出開始時点で画面いっぱいに舞っている状態にする
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(sizeX.x, sizeX.y);
            main.startSizeY = new ParticleSystem.MinMaxCurve(sizeY.x, sizeY.y);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(sizeZ.x, sizeZ.y);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var startColor = new ParticleSystem.MinMaxGradient(BuildConfettiGradient())
            {
                mode = ParticleSystemGradientMode.RandomColor
            };
            main.startColor = startColor;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(gravityMin, gravityMax);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = maxParticles;
            main.useUnscaledTime = true;
            // 発生源は画角の外にあるため停止させず常に落下を進める
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(10f, 0.15f, EmitterDepth);
            shape.randomDirectionAmount = 0.2f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // XYZは同一CurveMode必須
            velocity.x = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.35f, 0.05f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);

            ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = particleSystem.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.separateAxes = false;
            limitVelocity.limit = 2.4f;
            limitVelocity.dampen = 0.18f;
            limitVelocity.drag = 0.08f;

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = new ParticleSystem.MinMaxCurve(0.55f);
            noise.strengthY = new ParticleSystem.MinMaxCurve(0.2f);
            noise.strengthZ = new ParticleSystem.MinMaxCurve(0.45f);
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.35f;
            noise.damping = true;
            noise.octaveCount = 3;
            noise.octaveMultiplier = 0.5f;
            noise.octaveScale = 2.1f;
            noise.quality = ParticleSystemNoiseQuality.High;
            noise.positionAmount = 1f;
            noise.rotationAmount = 0.85f;
            noise.sizeAmount = 0f;

            ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            // 紙がひらひら裏返る速度
            rotation.x = new ParticleSystem.MinMaxCurve(-6.5f, 6.5f);
            rotation.y = new ParticleSystem.MinMaxCurve(-3.5f, 3.5f);
            rotation.z = new ParticleSystem.MinMaxCurve(-8.5f, 8.5f);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = false;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.EaseInOut(0f, 1f, 1f, 0.92f));

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
                    new GradientAlphaKey(1f, 0.02f),
                    new GradientAlphaKey(1f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGradient);

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            // マテリアルがGPUインスタンス非対応だと描画落ちするため無効にする
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 50;
            renderer.minParticleSize = 0.005f;
            renderer.maxParticleSize = 1.5f;
            Material material = ResolveParticleMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return particleSystem;
        }

        private void AlignToAnchor(Transform anchor)
        {
            if (root == null)
            {
                return;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                Vector3 fallbackCenter = anchor != null ? anchor.position : Vector3.zero;
                root.transform.SetPositionAndRotation(
                    fallbackCenter + Vector3.up * EmitterHeight,
                    Quaternion.identity);
                ApplyEmitterWidth(10f);
                return;
            }

            Transform cameraTransform = camera.transform;
            Vector3 flatForward = cameraTransform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 1e-4f)
            {
                flatForward = Vector3.forward;
            }

            flatForward.Normalize();

            // 画角上端のすぐ外側から降らせ必ず画面内を通過させる
            Vector3 viewCenter = cameraTransform.position + cameraTransform.forward * EmitterViewDistance;
            float halfHeight = EmitterViewDistance
                * Mathf.Tan(Mathf.Max(1f, camera.fieldOfView) * 0.5f * Mathf.Deg2Rad);
            float width = (halfHeight * Mathf.Max(0.1f, camera.aspect) * 2f) + EmitterSideMargin;

            root.transform.SetPositionAndRotation(
                viewCenter + Vector3.up * (halfHeight + EmitterTopMargin),
                Quaternion.LookRotation(flatForward, Vector3.up));
            ApplyEmitterWidth(width);
        }

        private void ApplyEmitterWidth(float width)
        {
            ApplyEmitterWidth(chipSystem, width);
        }

        private static void ApplyEmitterWidth(ParticleSystem system, float width)
        {
            if (system == null)
            {
                return;
            }

            ParticleSystem.ShapeModule shape = system.shape;
            shape.scale = new Vector3(width, 0.15f, EmitterDepth);
        }

        private static void PlaySystem(ParticleSystem system)
        {
            if (system == null)
            {
                return;
            }

            system.Clear(true);
            system.Play(true);
        }

        private static void StopSystem(ref ParticleSystem system)
        {
            if (system == null)
            {
                return;
            }

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system = null;
        }

        private static Gradient BuildConfettiGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.95f, 0.18f, 0.28f), 0f),
                    new GradientColorKey(new Color(1f, 0.78f, 0.12f), 0.18f),
                    new GradientColorKey(new Color(1f, 0.95f, 0.35f), 0.34f),
                    new GradientColorKey(new Color(0.2f, 0.78f, 0.42f), 0.5f),
                    new GradientColorKey(new Color(0.18f, 0.62f, 1f), 0.66f),
                    new GradientColorKey(new Color(0.55f, 0.28f, 1f), 0.82f),
                    new GradientColorKey(new Color(1f, 0.4f, 0.82f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Mesh ResolveChipMesh()
        {
            if (sharedChipMesh != null)
            {
                return sharedChipMesh;
            }

            sharedChipMesh = CreatePaperMesh(1f, 0.08f, 1.35f, "VictoryConfettiChipMesh");
            return sharedChipMesh;
        }

        private static Mesh CreatePaperMesh(float width, float thickness, float length, string meshName)
        {
            float halfW = width * 0.5f;
            float halfT = thickness * 0.5f;
            float halfL = length * 0.5f;

            var mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.HideAndDontSave
            };

            // 薄い直方体で表裏がはっきり見える紙片にする
            mesh.vertices = new[]
            {
                new Vector3(-halfW, -halfT, -halfL),
                new Vector3(halfW, -halfT, -halfL),
                new Vector3(halfW, halfT, -halfL),
                new Vector3(-halfW, halfT, -halfL),
                new Vector3(-halfW, -halfT, halfL),
                new Vector3(halfW, -halfT, halfL),
                new Vector3(halfW, halfT, halfL),
                new Vector3(-halfW, halfT, halfL)
            };
            // UV未設定だとソフト円テクスチャの透明端を拾い全滅するため全面を不透明中心に寄せる
            mesh.uv = new[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                2, 3, 7, 2, 7, 6,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5
            };
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Material ResolveParticleMaterial()
        {
            if (sharedParticleMaterial != null)
            {
                ConfigureSolidPaperMaterial(sharedParticleMaterial);
                return sharedParticleMaterial;
            }

            Material template = Resources.Load<Material>(AlphaParticleMaterialResourcePath);
            if (template != null)
            {
                sharedParticleMaterial = new Material(template)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    color = Color.white
                };
                ConfigureSolidPaperMaterial(sharedParticleMaterial);
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
            ConfigureSolidPaperMaterial(sharedParticleMaterial);
            return sharedParticleMaterial;
        }

        private static void ConfigureSolidPaperMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            // 紙片は表裏どちらも見える必要があるため裏面カリングを切る
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }

            // 丸いソフトテクスチャを外し紙として塗る
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", Texture2D.whiteTexture);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
