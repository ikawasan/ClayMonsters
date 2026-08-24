using UnityEngine;

namespace Battle.View
{
    /// <summary>
    /// プール済みエフェクトのParticleSystem配列を再利用する
    /// </summary>
    internal sealed class BattleEffectParticleCache : MonoBehaviour
    {
        private ParticleSystem[] particleSystems;
        private ParticleSystemRenderer[] particleRenderers;
        private Transform[] childTransforms;

        /// <summary>
        /// インスタンスからキャッシュを取得または作成する
        /// </summary>
        public static BattleEffectParticleCache GetOrAdd(GameObject instance)
        {
            if (instance == null)
            {
                return null;
            }

            BattleEffectParticleCache cache = instance.GetComponent<BattleEffectParticleCache>();
            if (cache == null)
            {
                cache = instance.AddComponent<BattleEffectParticleCache>();
            }

            return cache;
        }

        /// <summary>
        /// ParticleSystem配列を返す
        /// </summary>
        public ParticleSystem[] GetParticleSystems()
        {
            if (particleSystems == null)
            {
                particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }

            return particleSystems;
        }

        /// <summary>
        /// ParticleSystemRenderer配列を返す
        /// </summary>
        public ParticleSystemRenderer[] GetParticleRenderers()
        {
            if (particleRenderers == null)
            {
                particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            }

            return particleRenderers;
        }

        /// <summary>
        /// 子Transform配列を返す
        /// </summary>
        public Transform[] GetChildTransforms()
        {
            if (childTransforms == null)
            {
                childTransforms = GetComponentsInChildren<Transform>(true);
            }

            return childTransforms;
        }
    }
}
