using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘開始時に採寸したモデル見た目のローカル境界
    /// 攻撃カメラで毎フレームBakeMeshしないためのキャッシュ
    /// </summary>
    public readonly struct BattleModelVisualExtents
    {
        /// <summary>
        /// ルートからのローカル中心
        /// </summary>
        public Vector3 LocalCenter { get; }

        /// <summary>
        /// ローカル半サイズ
        /// </summary>
        public Vector3 LocalExtents { get; }

        /// <summary>
        /// 有効な採寸結果か
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// 採寸結果を生成する
        /// </summary>
        public BattleModelVisualExtents(Vector3 localCenter, Vector3 localExtents)
        {
            LocalCenter = localCenter;
            LocalExtents = localExtents;
            IsValid = localExtents.x > 0f || localExtents.y > 0f || localExtents.z > 0f;
        }

        /// <summary>
        /// 実姿勢メッシュからローカル境界を採寸する
        /// </summary>
        /// <param name="model">対象モデル</param>
        /// <param name="extents">採寸結果</param>
        /// <returns>採寸できたか</returns>
        public static bool TryCapture(Transform model, out BattleModelVisualExtents extents)
        {
            extents = default;
            if (model == null
                || !BattleFieldFocusResolver.TryGetPosedModelBounds(model, out Bounds worldBounds))
            {
                return false;
            }

            Vector3 localMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 localMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            Vector3 center = worldBounds.center;
            Vector3 e = worldBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = center + Vector3.Scale(e, new Vector3(x, y, z));
                        Vector3 localCorner = model.InverseTransformPoint(worldCorner);
                        localMin = Vector3.Min(localMin, localCorner);
                        localMax = Vector3.Max(localMax, localCorner);
                    }
                }
            }

            Vector3 localCenter = (localMin + localMax) * 0.5f;
            Vector3 localExtents = (localMax - localMin) * 0.5f;
            localExtents.x = Mathf.Max(0.05f, localExtents.x);
            localExtents.y = Mathf.Max(0.05f, localExtents.y);
            localExtents.z = Mathf.Max(0.05f, localExtents.z);
            extents = new BattleModelVisualExtents(localCenter, localExtents);
            return extents.IsValid;
        }

        /// <summary>
        /// 現在のルート姿勢からワールド境界を復元する
        /// </summary>
        /// <param name="model">対象モデル</param>
        public Bounds ToWorldBounds(Transform model)
        {
            if (model == null || !IsValid)
            {
                return default;
            }

            bool hasBounds = false;
            Bounds worldBounds = default;
            Vector3 e = LocalExtents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 localCorner = LocalCenter + Vector3.Scale(e, new Vector3(x, y, z));
                        Vector3 worldCorner = model.TransformPoint(localCorner);
                        if (!hasBounds)
                        {
                            worldBounds = new Bounds(worldCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            worldBounds.Encapsulate(worldCorner);
                        }
                    }
                }
            }

            return worldBounds;
        }
    }
}
