using System.Collections.Generic;
using Extensions;
using UnityEngine;

namespace Battle.View
{
    /// <summary>
    /// 戦闘エフェクト用の簡易GameObjectプール
    /// </summary>
    public sealed class BattleEffectInstancePool
    {
        private readonly Stack<GameObject> free = new Stack<GameObject>(8);
        private readonly GameObject prefab;
        private readonly Transform inactiveRoot;
        private readonly string poolName;

        /// <summary>
        /// プールを生成する
        /// </summary>
        /// <param name="prefab">元プレハブnullなら空ホスト</param>
        /// <param name="poolName">プール名</param>
        /// <param name="capacityHint">初期想定数</param>
        public BattleEffectInstancePool(GameObject prefab, string poolName, int capacityHint = 4)
        {
            this.prefab = prefab;
            this.poolName = string.IsNullOrEmpty(poolName) ? "BattleEffectPool" : poolName;
            var rootObject = new GameObject(this.poolName + "_Root")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(rootObject);
            inactiveRoot = rootObject.transform;
            inactiveRoot.position = new Vector3(0f, -10000f, 0f);
            for (int i = 0; i < Mathf.Max(0, capacityHint); i++)
            {
                free.Push(CreateInstance());
            }
        }

        /// <summary>
        /// インスタンスを借りる
        /// </summary>
        public GameObject Rent(Vector3 worldPosition)
        {
            GameObject instance = free.Count > 0 ? free.Pop() : CreateInstance();
            if (instance == null)
            {
                return null;
            }

            Transform transform = instance.transform;
            transform.SetParent(null, false);
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            instance.SetActive(true);
            return instance;
        }

        /// <summary>
        /// 一定時間後に返却する
        /// </summary>
        public void ReleaseAfter(GameObject instance, float delaySeconds)
        {
            if (instance == null)
            {
                return;
            }

            var release = instance.GetComponent<PooledTimedRelease>();
            if (release == null)
            {
                release = instance.AddComponent<PooledTimedRelease>();
            }

            release.Begin(this, delaySeconds);
        }

        /// <summary>
        /// すぐに返却する
        /// </summary>
        public void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            StopParticles(instance);
            instance.SetActive(false);
            Transform transform = instance.transform;
            transform.SetParent(inactiveRoot, false);
            free.Push(instance);
        }

        private GameObject CreateInstance()
        {
            GameObject instance;
            if (prefab != null)
            {
                instance = Object.Instantiate(prefab, inactiveRoot);
            }
            else
            {
                instance = new GameObject(poolName + "_Item");
                instance.transform.SetParent(inactiveRoot, false);
            }

            instance.SetActive(false);
            return instance;
        }

        private static void StopParticles(GameObject instance)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private sealed class PooledTimedRelease : MonoBehaviour
        {
            private BattleEffectInstancePool owner;
            private float remainingSeconds;

            public void Begin(BattleEffectInstancePool pool, float delaySeconds)
            {
                owner = pool;
                remainingSeconds = Mathf.Max(0f, delaySeconds);
                enabled = true;
            }

            private void Update()
            {
                remainingSeconds -= GameplayTime.PresentationDeltaTime;
                if (remainingSeconds > 0f)
                {
                    return;
                }

                enabled = false;
                owner?.Release(gameObject);
            }
        }
    }
}
