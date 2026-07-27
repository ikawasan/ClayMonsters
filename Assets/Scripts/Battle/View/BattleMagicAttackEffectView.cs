using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Battle.View
{
    /// <summary>
    /// 魔法攻撃の詠唱円と技別エフェクトを再生する
    /// </summary>
    public sealed class BattleMagicAttackEffectView : MonoBehaviour
    {
        private const float DefaultEffectDuration = 2.2f;
        private const float ThunderShockScale = 2.4f;
        // 地面エフェクト見た目下端を地表よりわずかに浮かせる
        private const float GroundEffectSurfaceLift = 0.08f;
        // 詠唱円はピボットが地表想定なので小さな浮きだけ足す
        private const float CastCircleSurfaceLift = 0.02f;
        // パーティクル境界補正の上限(巨大boundsで飛ばないようにする)
        private const float MaxGroundAlignLift = 1.25f;

        private MagicAttackVfxCatalog catalog;
        private GameObject activeCastCircle;
        private CancellationTokenSource castCircleCts;
        private float battleGroundY;
        private bool hasBattleGroundY;

        private void Awake()
        {
            catalog = MagicAttackVfxCatalog.Load();
            if (catalog == null)
            {
                Debug.LogError(
                    "[BattleMagicAttackEffectView] Resources/Battle/MagicAttackVfxCatalogがありません",
                    this);
                return;
            }

            if (!catalog.Validate(out string error))
            {
                Debug.LogError($"[BattleMagicAttackEffectView] {error}", this);
            }
        }

        /// <summary>
        /// 平坦な戦場の地面高さを初期化する
        /// </summary>
        /// <param name="groundY">スポーン基準の地面Y</param>
        public void ConfigureGroundY(float groundY)
        {
            battleGroundY = groundY;
            hasBattleGroundY = true;
        }

        private void OnDestroy()
        {
            StopCastCircle();
        }

        /// <summary>
        /// 詠唱円を攻撃者の足元に表示する
        /// </summary>
        /// <param name="attackerRoot">攻撃側ルート</param>
        /// <param name="duration">表示時間</param>
        public void PlayCastCircle(Transform attackerRoot, float duration)
        {
            StopCastCircle();
            if (catalog == null || catalog.CastCirclePrefab == null || attackerRoot == null)
            {
                return;
            }

            if (!TryResolveGroundPosition(attackerRoot, out Vector3 surfacePosition))
            {
                return;
            }

            surfacePosition.y += CastCircleSurfaceLift;
            activeCastCircle = SpawnEffect(
                catalog.CastCirclePrefab,
                surfacePosition,
                Quaternion.identity,
                1f,
                "[BattleMagicAttackEffectView] 詠唱円prefabの生成に失敗しました");
            if (activeCastCircle == null)
            {
                return;
            }

            float playDuration = ConfigureParticlesOnce(activeCastCircle, Mathf.Max(0.2f, duration + 0.4f));
            castCircleCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            DestroyAfterAsync(activeCastCircle, playDuration, castCircleCts.Token).Forget();
        }

        /// <summary>
        /// 詠唱円を止める
        /// </summary>
        public void StopCastCircle()
        {
            castCircleCts?.Cancel();
            castCircleCts?.Dispose();
            castCircleCts = null;

            if (activeCastCircle != null)
            {
                Destroy(activeCastCircle);
                activeCastCircle = null;
            }
        }

        /// <summary>
        /// 技別エフェクトを再生する
        /// </summary>
        /// <param name="motion">攻撃</param>
        /// <param name="attackerRoot">攻撃側</param>
        /// <param name="targetRoot">対象側</param>
        public void PlayAttackEffect(MotionType motion, Transform attackerRoot, Transform targetRoot)
        {
            if (catalog == null)
            {
                return;
            }

            // ファイアーボールは飛翔開始イベント側で再生し着弾時は爆発のみ
            if (motion == MotionType.Fireball)
            {
                if (targetRoot != null)
                {
                    PlayFireballExplosion(ResolveHitPosition(targetRoot));
                }

                return;
            }

            GameObject prefab = catalog.ResolveAttackPrefab(motion);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[BattleMagicAttackEffectView] {motion}のエフェクトprefabが未配線です",
                    this);
                return;
            }

            Transform anchor = targetRoot != null ? targetRoot : attackerRoot;
            if (anchor == null)
            {
                return;
            }

            bool isGroundEffect = motion == MotionType.DiamondDust;
            Vector3 position;
            float surfaceY = 0f;
            if (isGroundEffect)
            {
                if (!TryResolveGroundPosition(anchor, out position))
                {
                    return;
                }

                surfaceY = position.y;
            }
            else
            {
                position = ResolveHitPosition(anchor);
            }

            float scale = motion == MotionType.ThunderShock ? ThunderShockScale : 1f;
            GameObject instance = SpawnEffect(
                prefab,
                position,
                Quaternion.identity,
                scale,
                $"[BattleMagicAttackEffectView] {motion}エフェクトの生成に失敗しました");
            if (instance == null)
            {
                return;
            }

            float duration = ConfigureParticlesOnce(instance, DefaultEffectDuration);
            if (isGroundEffect)
            {
                AlignGroundEffectToSurface(instance, surfaceY);
            }

            Destroy(instance, duration);
        }

        /// <summary>
        /// ファイアーボールの飛翔を開始する
        /// </summary>
        /// <param name="attackerRoot">攻撃側</param>
        /// <param name="targetRoot">対象側</param>
        /// <param name="travelDuration">飛翔時間</param>
        public void PlayFireballTravel(Transform attackerRoot, Transform targetRoot, float travelDuration)
        {
            if (catalog == null || catalog.ResolveAttackPrefab(MotionType.Fireball) == null)
            {
                return;
            }

            if (attackerRoot == null || targetRoot == null)
            {
                return;
            }

            PlayFireballTravelAsync(
                    catalog.ResolveAttackPrefab(MotionType.Fireball),
                    attackerRoot,
                    targetRoot,
                    Mathf.Max(0.05f, travelDuration))
                .Forget();
        }

        private async UniTaskVoid PlayFireballTravelAsync(
            GameObject prefab,
            Transform attackerRoot,
            Transform targetRoot,
            float travelDuration)
        {
            Vector3 start = ResolveHitPosition(attackerRoot);
            Vector3 end = ResolveHitPosition(targetRoot);
            Vector3 direction = end - start;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            GameObject instance = SpawnEffect(
                prefab,
                start,
                rotation,
                1f,
                "[BattleMagicAttackEffectView] ファイアーボールの生成に失敗しました");
            if (instance == null)
            {
                return;
            }

            ConfigureParticlesOnce(instance, travelDuration + 0.8f);
            float elapsed = 0f;
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            while (elapsed < travelDuration)
            {
                if (instance == null || targetRoot == null)
                {
                    return;
                }

                end = ResolveHitPosition(targetRoot);
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                instance.transform.position = Vector3.Lerp(start, end, t);
                Vector3 currentDirection = end - instance.transform.position;
                if (currentDirection.sqrMagnitude > 0.0001f)
                {
                    instance.transform.rotation = Quaternion.LookRotation(currentDirection.normalized, Vector3.up);
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (instance != null)
            {
                Destroy(instance);
            }
        }

        private void PlayFireballExplosion(Vector3 position)
        {
            if (catalog == null || catalog.FireballExplosionPrefab == null)
            {
                Debug.LogError(
                    "[BattleMagicAttackEffectView] fireballExplosionPrefabが未配線です",
                    this);
                return;
            }

            GameObject explosion = SpawnEffect(
                catalog.FireballExplosionPrefab,
                position,
                Quaternion.identity,
                1f,
                "[BattleMagicAttackEffectView] ファイアーボール着弾爆発の生成に失敗しました");
            if (explosion == null)
            {
                return;
            }

            float duration = ConfigureParticlesOnce(explosion, DefaultEffectDuration);
            Destroy(explosion, duration);
        }

        private static GameObject SpawnEffect(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            float scale,
            string errorMessage)
        {
            if (prefab == null)
            {
                Debug.LogError(errorMessage);
                return null;
            }

            GameObject instance = Instantiate(prefab, position, rotation);
            if (instance == null)
            {
                Debug.LogError(errorMessage);
                return null;
            }

            if (scale > 0f && !Mathf.Approximately(scale, 1f))
            {
                instance.transform.localScale = Vector3.one * scale;
            }

            return instance;
        }

        // 初期化済みの地面Yとモデル水平位置で足元座標を返す
        private bool TryResolveGroundPosition(Transform root, out Vector3 surfacePosition)
        {
            surfacePosition = Vector3.zero;
            if (!hasBattleGroundY)
            {
                Debug.LogError(
                    "[BattleMagicAttackEffectView] ConfigureGroundYが未設定です",
                    this);
                return false;
            }

            if (root == null)
            {
                return false;
            }

            surfacePosition = new Vector3(root.position.x, battleGroundY, root.position.z);
            return true;
        }

        // prefabのピボットより下にある見た目を地表へ持ち上げる
        private static void AlignGroundEffectToSurface(GameObject instance, float surfaceY)
        {
            if (instance == null)
            {
                return;
            }

            Transform root = instance.transform;
            float minLocalY = EstimateLocalBottomY(instance);

            // 子Transformやshapeがピボットより下ならその分だけ先に上げる
            Vector3 position = root.position;
            position.y = surfaceY + GroundEffectSurfaceLift - minLocalY;
            root.position = position;

            // パーティクル見た目下端がまだ埋まっていればYだけ追加補正する
            SimulateParticlesBriefly(instance);
            if (!TryGetGroundEffectLowestY(instance, surfaceY, out float effectMinY))
            {
                return;
            }

            float targetMinY = surfaceY + GroundEffectSurfaceLift;
            float deltaY = targetMinY - effectMinY;
            if (deltaY <= 0.001f)
            {
                return;
            }

            position = root.position;
            position.y += Mathf.Min(deltaY, MaxGroundAlignLift);
            root.position = position;
        }

        private static float EstimateLocalBottomY(GameObject instance)
        {
            Transform root = instance.transform;
            float minLocalY = 0f;

            Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform child = transforms[i];
                if (child == null)
                {
                    continue;
                }

                float localY = root.InverseTransformPoint(child.position).y;
                if (localY < minLocalY)
                {
                    minLocalY = localY;
                }
            }

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }

                Vector3 shapeWorld = system.transform.TransformPoint(system.shape.position);
                float localY = root.InverseTransformPoint(shapeWorld).y;
                // 頭上から降るemitterは足元補正に使わない
                if (localY < -0.01f && localY < minLocalY)
                {
                    minLocalY = localY;
                }
            }

            return minLocalY;
        }

        private static void SimulateParticlesBriefly(GameObject instance)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }

                system.Simulate(0.05f, true, false, true);
                system.Play(true);
            }
        }

        private static bool TryGetGroundEffectLowestY(
            GameObject instance,
            float surfaceY,
            out float lowestY)
        {
            lowestY = float.PositiveInfinity;
            bool found = false;
            Vector3 rootPosition = instance.transform.position;
            float minAcceptedY = surfaceY - 2f;
            float maxAcceptedY = surfaceY + 1.5f;

            ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ParticleSystemRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds candidate = renderer.bounds;
                Vector2 offsetXZ = new Vector2(
                    candidate.center.x - rootPosition.x,
                    candidate.center.z - rootPosition.z);
                if (offsetXZ.sqrMagnitude > 25f)
                {
                    continue;
                }

                float minY = candidate.min.y;
                if (minY < minAcceptedY || minY > maxAcceptedY)
                {
                    continue;
                }

                if (minY < lowestY)
                {
                    lowestY = minY;
                    found = true;
                }
            }

            return found;
        }

        private static Vector3 ResolveHitPosition(Transform root)
        {
            return BattleHitEffectView.ResolveWorldHitPoint(root, 1.1f);
        }

        private static float ConfigureParticlesOnce(GameObject instance, float fallbackDuration)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            float maxDuration = fallbackDuration;
            if (systems == null || systems.Length == 0)
            {
                return maxDuration;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }

                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                main.loop = false;
                float duration = main.duration + main.startLifetime.constantMax;
                if (duration > maxDuration)
                {
                    maxDuration = duration;
                }

                system.Play(true);
            }

            return Mathf.Max(0.2f, maxDuration);
        }

        private async UniTaskVoid DestroyAfterAsync(
            GameObject instance,
            float duration,
            CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(Mathf.Max(0.05f, duration)),
                    ignoreTimeScale: false,
                    cancellationToken: cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            if (instance != null && instance == activeCastCircle)
            {
                activeCastCircle = null;
                Destroy(instance);
            }
            else if (instance != null)
            {
                Destroy(instance);
            }
        }
    }
}
