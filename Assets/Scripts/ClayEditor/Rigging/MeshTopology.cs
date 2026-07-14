using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// マーチングキューブが出力する三角形スープ（頂点が重複し、面が共有されないメッシュ）から、
    /// 同一座標の頂点を溶接して接続情報（隣接リスト）を復元するユーティリティ。
    /// カーブスケルトン抽出にはメッシュの接続性が必要なため、その前処理として用いる。
    /// </summary>
    public sealed class MeshTopology
    {
        /// <summary>
        /// 溶接後のユニーク頂点座標（meshRoot のローカル座標）。
        /// </summary>
        public Vector3[] Positions { get; }

        /// <summary>
        /// 各ユニーク頂点に隣接するユニーク頂点インデックスのリスト。
        /// </summary>
        public List<int>[] Adjacency { get; }

        private MeshTopology(Vector3[] positions, List<int>[] adjacency)
        {
            Positions = positions;
            Adjacency = adjacency;
        }

        /// <summary>
        /// 三角形スープ（連続する3頂点で1三角形）から溶接済みトポロジーを構築する。
        /// </summary>
        /// <param name="vertices">頂点配列（連続する3つで1三角形を構成する）。</param>
        /// <param name="weldEpsilon">同一頂点とみなす量子化グリッドサイズ。</param>
        public static MeshTopology Build(Vector3[] vertices, float weldEpsilon)
        {
            int[] remap = new int[vertices.Length];
            var keyToIndex = new Dictionary<long, int>(vertices.Length);
            var positions = new List<Vector3>(vertices.Length);

            float inv = 1f / Mathf.Max(weldEpsilon, 1e-5f);
            for (int i = 0; i < vertices.Length; i++)
            {
                long key = QuantizeKey(vertices[i], inv);
                if (!keyToIndex.TryGetValue(key, out int idx))
                {
                    idx = positions.Count;
                    keyToIndex.Add(key, idx);
                    positions.Add(vertices[i]);
                }

                remap[i] = idx;
            }

            int uniqueCount = positions.Count;
            var adjacency = new List<int>[uniqueCount];
            for (int i = 0; i < uniqueCount; i++)
            {
                adjacency[i] = new List<int>();
            }

            // 連続する3頂点を1三角形として辺を張る
            for (int t = 0; t + 2 < vertices.Length; t += 3)
            {
                int a = remap[t];
                int b = remap[t + 1];
                int c = remap[t + 2];
                AddEdge(adjacency, a, b);
                AddEdge(adjacency, b, c);
                AddEdge(adjacency, c, a);
            }

            return new MeshTopology(positions.ToArray(), adjacency);
        }

        private static void AddEdge(List<int>[] adjacency, int a, int b)
        {
            if (a == b)
            {
                return;
            }

            if (!adjacency[a].Contains(b))
            {
                adjacency[a].Add(b);
            }

            if (!adjacency[b].Contains(a))
            {
                adjacency[b].Add(a);
            }
        }

        // 座標を量子化して1つの long キーへ畳み込む（溶接判定用）
        private static long QuantizeKey(Vector3 v, float inv)
        {
            long x = (long)Mathf.Round(v.x * inv);
            long y = (long)Mathf.Round(v.y * inv);
            long z = (long)Mathf.Round(v.z * inv);

            const long mask = 0x1FFFFF; // 各成分21bit
            return ((x & mask) << 42) | ((y & mask) << 21) | (z & mask);
        }
    }
}