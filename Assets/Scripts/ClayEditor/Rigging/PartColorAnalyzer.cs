using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// 塗られたメッシュ頂点色とボーン分類から部位ごとの代表色を求める解析
    /// </summary>
    public static class PartColorAnalyzer
    {
        private const int QuantizeLevels = 4;

        /// <summary>
        /// 部位ごとに最も多く使われている頂点色(代表色)を返す
        /// </summary>
        public static Dictionary<BonePart, Color> Analyze(SkinnedMeshRenderer renderer, IReadOnlyDictionary<Transform, BonePart> boneParts)
        {
            var result = new Dictionary<BonePart, Color>();
            if (renderer == null || boneParts == null)
            {
                return result;
            }

            Mesh mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                return result;
            }

            Color[] colors = mesh.colors;
            BoneWeight[] weights = mesh.boneWeights;
            Transform[] bones = renderer.bones;
            if (colors == null || colors.Length == 0 || weights == null || weights.Length != colors.Length || bones == null || bones.Length == 0)
            {
                return result;
            }

            // 部位ごとに 量子化色->(出現数・色の合計) のヒストグラムを作る
            var histograms = new Dictionary<BonePart, Dictionary<int, ColorBucket>>();

            for (int i = 0; i < colors.Length; i++)
            {
                int boneIndex = weights[i].boneIndex0;
                if (boneIndex < 0 || boneIndex >= bones.Length)
                {
                    continue;
                }

                Transform bone = bones[boneIndex];
                if (bone == null || !boneParts.TryGetValue(bone, out BonePart part))
                {
                    continue;
                }

                Color color = colors[i];
                int key = Quantize(color);

                if (!histograms.TryGetValue(part, out Dictionary<int, ColorBucket> buckets))
                {
                    buckets = new Dictionary<int, ColorBucket>();
                    histograms[part] = buckets;
                }

                buckets.TryGetValue(key, out ColorBucket bucket);
                bucket.Count++;
                bucket.Sum += color;
                buckets[key] = bucket;
            }

            // 部位ごとに最頻バケットの平均色を代表色にする
            foreach (KeyValuePair<BonePart, Dictionary<int, ColorBucket>> pair in histograms)
            {
                ColorBucket best = default;
                bool found = false;
                foreach (ColorBucket bucket in pair.Value.Values)
                {
                    if (!found || bucket.Count > best.Count)
                    {
                        best = bucket;
                        found = true;
                    }
                }

                if (found && best.Count > 0)
                {
                    result[pair.Key] = best.Sum * (1f / best.Count);
                }
            }

            return result;
        }

        /// <summary>
        /// モデル全体で最も多く使われている頂点色(代表色)を返す
        /// </summary>
        public static Color AnalyzeDominantColor(
            SkinnedMeshRenderer renderer,
            IReadOnlyDictionary<Transform, BonePart> boneParts)
        {
            if (!TryBuildGlobalHistogram(renderer, boneParts, out Dictionary<int, ColorBucket> buckets))
            {
                return Color.clear;
            }

            return PickDominantColor(buckets);
        }

        private static bool TryBuildGlobalHistogram(
            SkinnedMeshRenderer renderer,
            IReadOnlyDictionary<Transform, BonePart> boneParts,
            out Dictionary<int, ColorBucket> buckets)
        {
            buckets = null;
            if (renderer == null || boneParts == null)
            {
                return false;
            }

            Mesh mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                return false;
            }

            Color[] colors = mesh.colors;
            BoneWeight[] weights = mesh.boneWeights;
            Transform[] bones = renderer.bones;
            if (colors == null || colors.Length == 0 || weights == null || weights.Length != colors.Length || bones == null || bones.Length == 0)
            {
                return false;
            }

            buckets = new Dictionary<int, ColorBucket>();
            for (int i = 0; i < colors.Length; i++)
            {
                int boneIndex = weights[i].boneIndex0;
                if (boneIndex < 0 || boneIndex >= bones.Length)
                {
                    continue;
                }

                Transform bone = bones[boneIndex];
                if (bone == null || !boneParts.ContainsKey(bone))
                {
                    continue;
                }

                Color color = colors[i];
                int key = Quantize(color);
                buckets.TryGetValue(key, out ColorBucket bucket);
                bucket.Count++;
                bucket.Sum += color;
                buckets[key] = bucket;
            }

            return buckets.Count > 0;
        }

        private static Color PickDominantColor(Dictionary<int, ColorBucket> buckets)
        {
            ColorBucket best = default;
            bool found = false;
            foreach (ColorBucket bucket in buckets.Values)
            {
                if (!found || bucket.Count > best.Count)
                {
                    best = bucket;
                    found = true;
                }
            }

            return found && best.Count > 0 ? best.Sum * (1f / best.Count) : Color.clear;
        }

        // 色を粗く量子化して整数キーにする
        private static int Quantize(Color color)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(color.r * QuantizeLevels), 0, QuantizeLevels);
            int g = Mathf.Clamp(Mathf.RoundToInt(color.g * QuantizeLevels), 0, QuantizeLevels);
            int b = Mathf.Clamp(Mathf.RoundToInt(color.b * QuantizeLevels), 0, QuantizeLevels);
            int span = QuantizeLevels + 1;
            return ((r * span) + g) * span + b;
        }

        // 量子化バケットの集計
        private struct ColorBucket
        {
            public int Count;
            public Color Sum;
        }
    }
}