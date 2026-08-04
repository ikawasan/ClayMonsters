using System.Collections.Generic;
using UnityEngine;

namespace Battle.View
{
    /// <summary>
    /// モデル配下のRenderer境界をキャッシュして再利用する
    /// </summary>
    public static class BattleModelBoundsCache
    {
        private sealed class Entry
        {
            public Transform Root;
            public Renderer[] Renderers;
            public int ChildCount;
        }

        private static readonly Dictionary<int, Entry> Cache = new Dictionary<int, Entry>(16);
        private static readonly List<int> PruneKeys = new List<int>(16);
        private static readonly List<Renderer> RendererScratch = new List<Renderer>(32);

        /// <summary>
        /// モデルのワールド境界中心を返す
        /// </summary>
        /// <param name="modelRoot">モデルルート</param>
        /// <param name="fallbackHeight">Rendererが無いときの高さオフセット</param>
        public static Vector3 ResolveWorldCenter(Transform modelRoot, float fallbackHeight)
        {
            if (modelRoot == null)
            {
                return Vector3.zero;
            }

            if (!TryResolveBounds(modelRoot, out Bounds bounds))
            {
                return modelRoot.position + Vector3.up * fallbackHeight;
            }

            return bounds.center;
        }

        /// <summary>
        /// モデルのワールド境界を返す
        /// </summary>
        /// <param name="modelRoot">モデルルート</param>
        /// <param name="bounds">境界</param>
        public static bool TryResolveBounds(Transform modelRoot, out Bounds bounds)
        {
            bounds = default;
            if (modelRoot == null)
            {
                return false;
            }

            Renderer[] renderers = ResolveRenderers(modelRoot);
            bool hasBounds = false;
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
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        /// <summary>
        /// 破棄済みキャッシュを捨てる
        /// </summary>
        public static void Prune()
        {
            if (Cache.Count == 0)
            {
                return;
            }

            PruneKeys.Clear();
            foreach (KeyValuePair<int, Entry> pair in Cache)
            {
                Entry entry = pair.Value;
                if (entry == null || entry.Root == null)
                {
                    PruneKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < PruneKeys.Count; i++)
            {
                Cache.Remove(PruneKeys[i]);
            }

            PruneKeys.Clear();
        }

        /// <summary>
        /// 全キャッシュを捨てる
        /// </summary>
        public static void Clear()
        {
            Cache.Clear();
        }

        private static Renderer[] ResolveRenderers(Transform modelRoot)
        {
            int id = modelRoot.GetInstanceID();
            int childCount = modelRoot.childCount;
            if (Cache.TryGetValue(id, out Entry entry)
                && entry != null
                && entry.Root == modelRoot
                && entry.Renderers != null
                && entry.ChildCount == childCount
                && AreRenderersValid(entry.Renderers))
            {
                return entry.Renderers;
            }

            RendererScratch.Clear();
            modelRoot.GetComponentsInChildren(false, RendererScratch);
            var array = RendererScratch.Count == 0
                ? System.Array.Empty<Renderer>()
                : RendererScratch.ToArray();
            Cache[id] = new Entry
            {
                Root = modelRoot,
                Renderers = array,
                ChildCount = childCount,
            };
            return array;
        }

        private static bool AreRenderersValid(Renderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
