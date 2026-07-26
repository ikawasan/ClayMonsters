using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘カメラの注視点をモデル上下動の影響を受けないよう求める
    /// </summary>
    public static class BattleFieldFocusResolver
    {
        // ベイク結果の使い回し用で毎回のMesh確保を避ける
        private static Mesh bakeMesh;

        /// <summary>
        /// 両モデル間の注視点を返す(高さは地面基準+オフセット)
        /// </summary>
        /// <param name="playerModel">プレイヤーモデル</param>
        /// <param name="enemyModel">敵モデル</param>
        /// <param name="groundY">地面のY座標</param>
        /// <param name="heightOffset">注視点の高さオフセット</param>
        /// <returns>注視点</returns>
        public static Vector3 ResolveMidpoint(
            Transform playerModel,
            Transform enemyModel,
            float groundY,
            float heightOffset)
        {
            Vector3 focus = ResolveMidpointOnGround(playerModel, enemyModel, groundY);
            focus.y += heightOffset;
            return focus;
        }

        /// <summary>
        /// 攻撃演出用の注視点を返す(高さは地面基準+オフセット)
        /// </summary>
        /// <param name="attackerModel">攻撃側モデル</param>
        /// <param name="targetModel">被攻撃側モデル</param>
        /// <param name="groundY">地面のY座標</param>
        /// <param name="heightOffset">注視点の高さオフセット</param>
        /// <param name="towardTargetRatio">ターゲット方向へ寄せる比率</param>
        /// <returns>注視点</returns>
        public static Vector3 ResolveAttackFocus(
            Transform attackerModel,
            Transform targetModel,
            float groundY,
            float heightOffset,
            float towardTargetRatio)
        {
            if (attackerModel == null)
            {
                return new Vector3(0f, groundY + heightOffset, 0f);
            }

            Vector3 attackerFlat = FlattenY(attackerModel.position, groundY);
            Vector3 targetFlat = targetModel != null
                ? FlattenY(targetModel.position, groundY)
                : attackerFlat + FlattenDirection(attackerModel.forward);

            Vector3 focus = Vector3.Lerp(attackerFlat, targetFlat, towardTargetRatio);
            focus.y += heightOffset;
            return focus;
        }

        /// <summary>
        /// 位置のY座標を地面高さへ固定する
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="groundY">地面のY座標</param>
        /// <returns>Y固定後の位置</returns>
        public static Vector3 FlattenY(Vector3 position, float groundY)
        {
            position.y = groundY;
            return position;
        }

        /// <summary>
        /// 両モデル間の地面平面上の中点を返す
        /// </summary>
        /// <param name="playerModel">プレイヤーモデル</param>
        /// <param name="enemyModel">敵モデル</param>
        /// <param name="groundY">地面のY座標</param>
        /// <returns>中点</returns>
        public static Vector3 ResolveMidpointOnGround(
            Transform playerModel,
            Transform enemyModel,
            float groundY)
        {
            if (playerModel != null && enemyModel != null)
            {
                return (FlattenY(playerModel.position, groundY) + FlattenY(enemyModel.position, groundY)) * 0.5f;
            }

            if (playerModel != null)
            {
                return FlattenY(playerModel.position, groundY);
            }

            if (enemyModel != null)
            {
                return FlattenY(enemyModel.position, groundY);
            }

            return new Vector3(0f, groundY, 0f);
        }

        /// <summary>
        /// 両モデルのRenderer境界を結合する
        /// </summary>
        /// <param name="playerModel">プレイヤーモデル</param>
        /// <param name="enemyModel">敵モデル</param>
        /// <param name="bounds">結合境界</param>
        /// <returns>境界を取得できたか</returns>
        public static bool TryGetCombinedRendererBounds(
            Transform playerModel,
            Transform enemyModel,
            out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            hasBounds |= EncapsulateRenderers(playerModel, ref bounds, ref hasBounds);
            hasBounds |= EncapsulateRenderers(enemyModel, ref bounds, ref hasBounds);
            return hasBounds;
        }

        /// <summary>
        /// 単一モデルのRenderer境界を返す
        /// </summary>
        public static bool TryGetModelRendererBounds(Transform model, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            bool encapsulated = EncapsulateRenderers(model, ref bounds, ref hasBounds);
            return encapsulated && hasBounds;
        }

        /// <summary>
        /// 実姿勢のメッシュから単一モデルの境界を返す
        /// スキンメッシュをベイクするため既定境界の誤りに影響されない
        /// </summary>
        /// <param name="model">対象モデル</param>
        /// <param name="bounds">実姿勢の境界</param>
        /// <returns>境界を取得できたか</returns>
        public static bool TryGetPosedModelBounds(Transform model, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            EncapsulatePosedRenderers(model, ref bounds, ref hasBounds);
            return hasBounds;
        }

        /// <summary>
        /// 実姿勢のメッシュから両モデルの結合境界を返す
        /// スキンメッシュをベイクするため既定境界の誤りに影響されない
        /// </summary>
        /// <param name="playerModel">プレイヤーモデル</param>
        /// <param name="enemyModel">敵モデル</param>
        /// <param name="bounds">実姿勢の結合境界</param>
        /// <returns>境界を取得できたか</returns>
        public static bool TryGetPosedCombinedBounds(
            Transform playerModel,
            Transform enemyModel,
            out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            EncapsulatePosedRenderers(playerModel, ref bounds, ref hasBounds);
            EncapsulatePosedRenderers(enemyModel, ref bounds, ref hasBounds);
            return hasBounds;
        }

        /// <summary>
        /// 画面上の左右方向へ投影したモデル半幅を返す
        /// </summary>
        public static float ResolveHorizontalHalfExtent(Transform model, float cameraHorizontalAngle)
        {
            if (!TryGetPosedModelBounds(model, out Bounds bounds))
            {
                return 0.75f;
            }

            Vector3 screenRight = BattleFieldScreenAxis.ResolveScreenRight(cameraHorizontalAngle);
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float maxHalf = 0f;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        float projection = Mathf.Abs(Vector3.Dot(corner - center, screenRight));
                        if (projection > maxHalf)
                        {
                            maxHalf = projection;
                        }
                    }
                }
            }

            return Mathf.Max(0.35f, maxHalf);
        }

        /// <summary>
        /// 対戦紹介用の横並び間隔をモデルサイズから算出する
        /// </summary>
        public static float ResolveMatchupIntroSeparation(
            Transform playerModel,
            Transform enemyModel,
            float cameraHorizontalAngle,
            float gapPadding,
            float minSeparation,
            float maxSeparation)
        {
            float playerHalf = ResolveHorizontalHalfExtent(playerModel, cameraHorizontalAngle);
            float enemyHalf = ResolveHorizontalHalfExtent(enemyModel, cameraHorizontalAngle);
            float separation = playerHalf + enemyHalf + gapPadding;
            return Mathf.Clamp(separation, minSeparation, maxSeparation);
        }

        /// <summary>
        /// 対戦紹介で左右モデルがレイアウト中央を越えないよう外側へ押し出す
        /// 前後に長いモデルの相手側はみ出しを防ぎ短いモデルは中央帯まで寄せられる
        /// </summary>
        /// <param name="playerModel">左側のプレイヤー</param>
        /// <param name="enemyModel">右側の敵</param>
        /// <param name="layoutCenter">対戦紹介の固定中央</param>
        /// <param name="cameraHorizontalAngle">対戦紹介カメラの水平角</param>
        /// <param name="gapPadding">中央に残す隙間</param>
        public static void EnforceMatchupSideClearance(
            Transform playerModel,
            Transform enemyModel,
            Vector3 layoutCenter,
            float cameraHorizontalAngle,
            float gapPadding)
        {
            if (playerModel == null || enemyModel == null)
            {
                return;
            }

            Vector3 screenRight = BattleFieldScreenAxis.ResolveScreenRight(cameraHorizontalAngle);
            if (screenRight.sqrMagnitude < 1e-6f)
            {
                return;
            }

            float requiredGap = Mathf.Max(0.05f, gapPadding);
            float halfGap = requiredGap * 0.5f;
            float midProjection = Vector3.Dot(layoutCenter, screenRight);
            const int maxPasses = 6;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                if (!TryGetPosedModelBounds(playerModel, out Bounds playerBounds)
                    || !TryGetPosedModelBounds(enemyModel, out Bounds enemyBounds))
                {
                    return;
                }

                float playerMax = ResolveMaxAxisProjection(playerBounds, Vector3.zero, screenRight);
                float enemyMin = ResolveMinAxisProjection(enemyBounds, Vector3.zero, screenRight);
                bool moved = false;

                // 固定中央帯をまたぐはみ出しを先に外側へ戻す
                float playerOverflow = playerMax - (midProjection - halfGap);
                if (playerOverflow > 1e-4f)
                {
                    playerModel.position -= screenRight * playerOverflow;
                    moved = true;
                }

                float enemyOverflow = (midProjection + halfGap) - enemyMin;
                if (enemyOverflow > 1e-4f)
                {
                    enemyModel.position += screenRight * enemyOverflow;
                    moved = true;
                }

                if (moved)
                {
                    continue;
                }

                // 中央帯を侵さない範囲で余った隙間だけ内側へ寄せる
                float currentGap = enemyMin - playerMax;
                float excessGap = currentGap - requiredGap;
                if (excessGap <= 1e-4f)
                {
                    return;
                }

                float playerSlack = (midProjection - halfGap) - playerMax;
                float enemySlack = enemyMin - (midProjection + halfGap);
                float playerPull = Mathf.Min(excessGap * 0.5f, Mathf.Max(0f, playerSlack));
                float enemyPull = Mathf.Min(excessGap * 0.5f, Mathf.Max(0f, enemySlack));
                if (playerPull <= 1e-4f && enemyPull <= 1e-4f)
                {
                    return;
                }

                playerModel.position += screenRight * playerPull;
                enemyModel.position -= screenRight * enemyPull;
            }
        }

        private static float ResolveMaxAxisProjection(Bounds bounds, Vector3 origin, Vector3 axis)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float maxProjection = float.NegativeInfinity;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        float projection = Vector3.Dot(corner - origin, axis);
                        if (projection > maxProjection)
                        {
                            maxProjection = projection;
                        }
                    }
                }
            }

            return maxProjection;
        }

        private static float ResolveMinAxisProjection(Bounds bounds, Vector3 origin, Vector3 axis)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float minProjection = float.PositiveInfinity;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        float projection = Vector3.Dot(corner - origin, axis);
                        if (projection < minProjection)
                        {
                            minProjection = projection;
                        }
                    }
                }
            }

            return minProjection;
        }

        /// <summary>
        /// 境界全体が収まるオービット距離を返す
        /// </summary>
        /// <param name="bounds">対象境界</param>
        /// <param name="verticalFovDegrees">垂直FOV</param>
        /// <param name="aspect">アスペクト比</param>
        /// <param name="padding">余白係数(小さいほど寄る)</param>
        /// <param name="minDistance">最小距離</param>
        /// <param name="maxDistance">最大距離</param>
        /// <returns>オービット距離</returns>
        public static float ResolveOrbitDistanceForBounds(
            Bounds bounds,
            float verticalFovDegrees,
            float aspect,
            float padding,
            float minDistance,
            float maxDistance)
        {
            float verticalRadians = verticalFovDegrees * Mathf.Deg2Rad;
            float horizontalRadians = 2f * Mathf.Atan(Mathf.Tan(verticalRadians * 0.5f) * aspect);
            float halfHeight = bounds.extents.y * 1.08f;
            float halfWidth = bounds.extents.x * 1.06f;
            float distanceVertical = halfHeight / Mathf.Tan(verticalRadians * 0.5f);
            float distanceHorizontal = halfWidth / Mathf.Tan(horizontalRadians * 0.5f);
            float distance = Mathf.Max(distanceVertical, distanceHorizontal) * padding;
            return Mathf.Clamp(distance, minDistance, maxDistance);
        }

        /// <summary>
        /// カメラ角度を考慮し境界全体が収まるオービット距離を返す
        /// 境界の8頂点を画面右上奥へ投影し必要距離と奥行き余裕を足す
        /// </summary>
        /// <param name="bounds">対象境界</param>
        /// <param name="horizontalAngleDegrees">水平オービット角</param>
        /// <param name="verticalAngleDegrees">垂直オービット角</param>
        /// <param name="verticalFovDegrees">垂直FOV</param>
        /// <param name="aspect">アスペクト比</param>
        /// <param name="padding">余白係数(小さいほど寄る)</param>
        /// <param name="minDistance">最小距離</param>
        /// <param name="maxDistance">最大距離</param>
        /// <returns>オービット距離</returns>
        public static float ResolveOrbitDistanceForBounds(
            Bounds bounds,
            float horizontalAngleDegrees,
            float verticalAngleDegrees,
            float verticalFovDegrees,
            float aspect,
            float padding,
            float minDistance,
            float maxDistance)
        {
            Quaternion orbit = Quaternion.Euler(verticalAngleDegrees, horizontalAngleDegrees, 0f);
            Vector3 right = orbit * Vector3.right;
            Vector3 up = orbit * Vector3.up;
            Vector3 forward = orbit * Vector3.forward;

            Vector3 extents = bounds.extents;
            float halfWidth = 0f;
            float halfHeight = 0f;
            float halfDepth = 0f;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 offset = Vector3.Scale(extents, new Vector3(x, y, z));
                        halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(offset, right)));
                        halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(offset, up)));
                        halfDepth = Mathf.Max(halfDepth, Mathf.Abs(Vector3.Dot(offset, forward)));
                    }
                }
            }

            float verticalRadians = Mathf.Max(1f, verticalFovDegrees) * Mathf.Deg2Rad;
            float safeAspect = Mathf.Max(0.1f, aspect);
            float horizontalRadians = 2f * Mathf.Atan(Mathf.Tan(verticalRadians * 0.5f) * safeAspect);

            float distanceVertical = halfHeight / Mathf.Tan(verticalRadians * 0.5f);
            float distanceHorizontal = halfWidth / Mathf.Tan(horizontalRadians * 0.5f);
            // 手前側の膨らみが画角から外れないよう奥行き半分を足す
            float distance =
                (Mathf.Max(distanceVertical, distanceHorizontal) * Mathf.Max(0.01f, padding)) + halfDepth;
            return Mathf.Clamp(distance, minDistance, maxDistance);
        }

        /// <summary>
        /// 指定注視点まわりで境界の8頂点が収まるオービット距離を返す
        /// 注視点が境界中心とずれる攻撃構図向け
        /// </summary>
        /// <param name="bounds">対象境界</param>
        /// <param name="focus">注視点</param>
        /// <param name="horizontalAngleDegrees">水平オービット角</param>
        /// <param name="verticalAngleDegrees">垂直オービット角</param>
        /// <param name="verticalFovDegrees">垂直FOV</param>
        /// <param name="aspect">アスペクト比</param>
        /// <param name="padding">余白係数(小さいほど寄る)</param>
        /// <param name="minDistance">最小距離</param>
        /// <param name="maxDistance">最大距離</param>
        /// <returns>オービット距離</returns>
        public static float ResolveOrbitDistanceForBoundsAroundFocus(
            Bounds bounds,
            Vector3 focus,
            float horizontalAngleDegrees,
            float verticalAngleDegrees,
            float verticalFovDegrees,
            float aspect,
            float padding,
            float minDistance,
            float maxDistance)
        {
            Quaternion orbit = Quaternion.Euler(verticalAngleDegrees, horizontalAngleDegrees, 0f);
            Vector3 right = orbit * Vector3.right;
            Vector3 up = orbit * Vector3.up;
            Vector3 forward = orbit * Vector3.forward;

            Vector3 extents = bounds.extents;
            Vector3 center = bounds.center;
            float halfWidth = 0f;
            float halfHeight = 0f;
            float halfDepth = 0f;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 offset = worldCorner - focus;
                        halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(offset, right)));
                        halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(offset, up)));
                        halfDepth = Mathf.Max(halfDepth, Mathf.Abs(Vector3.Dot(offset, forward)));
                    }
                }
            }

            float verticalRadians = Mathf.Max(1f, verticalFovDegrees) * Mathf.Deg2Rad;
            float safeAspect = Mathf.Max(0.1f, aspect);
            float horizontalRadians = 2f * Mathf.Atan(Mathf.Tan(verticalRadians * 0.5f) * safeAspect);

            float distanceVertical = halfHeight / Mathf.Tan(verticalRadians * 0.5f);
            float distanceHorizontal = halfWidth / Mathf.Tan(horizontalRadians * 0.5f);
            float distance =
                (Mathf.Max(distanceVertical, distanceHorizontal) * Mathf.Max(0.01f, padding)) + halfDepth;
            return Mathf.Clamp(distance, minDistance, maxDistance);
        }

        private static void EncapsulatePosedRenderers(Transform model, ref Bounds bounds, ref bool hasBounds)
        {
            if (model == null)
            {
                return;
            }

            SkinnedMeshRenderer[] skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
                if (skinnedRenderer == null || !skinnedRenderer.enabled)
                {
                    continue;
                }

                EncapsulateBakedSkinned(skinnedRenderer, ref bounds, ref hasBounds);
            }

            MeshRenderer[] meshRenderers = model.GetComponentsInChildren<MeshRenderer>(false);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                if (meshRenderer == null || !meshRenderer.enabled)
                {
                    continue;
                }

                EncapsulateBounds(meshRenderer.bounds, ref bounds, ref hasBounds);
            }
        }

        private static void EncapsulateBakedSkinned(
            SkinnedMeshRenderer skinnedRenderer,
            ref Bounds bounds,
            ref bool hasBounds)
        {
            Mesh posedMesh = GetBakeMesh();
            // useScaleをfalseにしTransformPointとのスケール二重適用を避ける
            skinnedRenderer.BakeMesh(posedMesh, false);
            Vector3[] vertices = posedMesh.vertices;
            if (vertices.Length == 0)
            {
                EncapsulateBounds(skinnedRenderer.bounds, ref bounds, ref hasBounds);
                return;
            }

            Transform rendererTransform = skinnedRenderer.transform;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = rendererTransform.TransformPoint(vertices[i]);
                if (!hasBounds)
                {
                    bounds = new Bounds(world, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(world);
                }
            }
        }

        private static void EncapsulateBounds(Bounds source, ref Bounds bounds, ref bool hasBounds)
        {
            if (!hasBounds)
            {
                bounds = source;
                hasBounds = true;
                return;
            }

            bounds.Encapsulate(source);
        }

        private static Mesh GetBakeMesh()
        {
            if (bakeMesh == null)
            {
                bakeMesh = new Mesh
                {
                    name = "BattleFieldFocusResolverBake",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return bakeMesh;
        }

        private static bool EncapsulateRenderers(Transform model, ref Bounds bounds, ref bool hasBounds)
        {
            if (model == null)
            {
                return false;
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(false);
            bool encapsulated = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                encapsulated = true;
            }

            return encapsulated;
        }

        private static Vector3 FlattenDirection(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        }
    }
}
