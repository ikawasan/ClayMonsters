using System.Collections.Generic;
using UnityEngine;

namespace Battle.View
{
    /// <summary>
    /// 魔法攻撃エフェクト用のprefab別プール
    /// </summary>
    internal static class BattleMagicEffectPools
    {
        private static readonly Dictionary<int, BattleEffectInstancePool> Pools =
            new Dictionary<int, BattleEffectInstancePool>(16);

        /// <summary>
        /// prefabからインスタンスを借りる
        /// </summary>
        public static GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
        {
            if (prefab == null)
            {
                return null;
            }

            int key = prefab.GetInstanceID();
            if (!Pools.TryGetValue(key, out BattleEffectInstancePool pool) || pool == null)
            {
                pool = new BattleEffectInstancePool(prefab, "BattleMagicFx_" + prefab.name, 2);
                Pools[key] = pool;
            }

            GameObject instance = pool.Rent(position);
            if (instance == null)
            {
                return null;
            }

            instance.transform.rotation = rotation;
            instance.transform.localScale = Vector3.one * (scale > 0f ? scale : 1f);

            return instance;
        }

        /// <summary>
        /// 一定時間後に返却する
        /// </summary>
        public static void ReleaseAfter(GameObject prefab, GameObject instance, float delaySeconds)
        {
            if (prefab == null || instance == null)
            {
                Object.Destroy(instance);
                return;
            }

            int key = prefab.GetInstanceID();
            if (!Pools.TryGetValue(key, out BattleEffectInstancePool pool) || pool == null)
            {
                Object.Destroy(instance);
                return;
            }

            pool.ReleaseAfter(instance, delaySeconds);
        }

        /// <summary>
        /// すぐ返却する
        /// </summary>
        public static void Release(GameObject prefab, GameObject instance)
        {
            if (prefab == null || instance == null)
            {
                Object.Destroy(instance);
                return;
            }

            int key = prefab.GetInstanceID();
            if (!Pools.TryGetValue(key, out BattleEffectInstancePool pool) || pool == null)
            {
                Object.Destroy(instance);
                return;
            }

            pool.Release(instance);
        }
    }
}
