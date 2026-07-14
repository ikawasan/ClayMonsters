#nullable enable
using ClayEditor.Interface;
using UnityEngine;
using VContainer;
using ClayMeshTopology = ClayEditor.Rigging.MeshTopology;

namespace ClayEditor
{
    public class ClayAutoRigger : MonoBehaviour
    {
        // 必要な状態を受け取る
        [Inject] private readonly IClaySceneContext sceneContext = default!;

        private Transform[] bones;

        /// <summary>
        /// スキニングに用いるボーン Transform 群
        /// </summary>
        public Transform[] Bones => bones;

        [Header("Weight Settings")]
        [Tooltip("距離の重み付けの鋭さ（大きいほど最寄りボーンに偏る）")]
        [SerializeField] private float weightFalloffPower = 4f;

        [Tooltip("各頂点に割り当てるボーンの最大数（最大4）")]
        [Range(1, 4)]
        [SerializeField] private int maxBonesPerVertex = 4;

        [Header("Smoothing Settings (案4)")]
        [Tooltip("隣接平滑化の反復回数（0で無効）")]
        [Range(0, 10)]
        [SerializeField] private int smoothingIterations = 3;

        [Tooltip("平滑化の強さ（0〜1。隣接平均へ寄せる割合）")]
        [Range(0f, 1f)]
        [SerializeField] private float smoothingStrength = 0.5f;

        [Tooltip("溶接で同一頂点とみなすグリッドサイズ（外部トポロジー未提供時のみ使用）")]
        [SerializeField] private float weldEpsilon = 0.01f;

        /// <summary>
        /// 現在編集中のモデルのボーンルート
        /// </summary>
        public Transform? BoneRoot
        {
            get
            {
                // 実行中かつ DI 解決済みの場合のみシーン状態を参照する
                if (Application.isPlaying && sceneContext != null)
                {
                    return sceneContext.CurrentModel.Value?.BoneRoot;
                }

                // エディタ実行時は DI 未解決のため、安全に null を返す
                return null;
            }
        }

        /// <summary>
        /// 直近の計算で生成されたバインドポーズ行列群
        /// </summary>
        public Matrix4x4[] BindPoses { get; private set; }

        /// <summary>
        /// スキニングに用いるボーン Transform 群を外部から設定する（自動リギング用）。
        /// </summary>
        /// <param name="newBones">設定するボーン Transform 群。</param>
        public void SetBones(Transform[] newBones)
        {
            bones = newBones;
        }

        /// <summary>
        /// 頂点群に対するボーンウェイトを計算する（トポロジー未指定。平滑化時は内部で溶接する）。
        /// </summary>
        /// <param name="vertices">対象頂点（meshRoot のローカル座標）</param>
        /// <param name="meshRoot">メッシュのルート Transform</param>
        /// <returns>計算済みボーンウェイト。前提条件を満たさない場合は null</returns>
        public BoneWeight[] Calculate(Vector3[] vertices, Transform meshRoot)
        {
            return Calculate(vertices, meshRoot, null);
        }

        /// <summary>
        /// 頂点群に対するボーンウェイトを計算する。
        /// ボーン線分（親→子）への最短距離で最大4ボーンへ加重し、メッシュの隣接で平滑化（案1＋案4）。
        /// 平滑化に使う溶接トポロジーを外部から渡せる（生成器と共有して二重溶接を防ぐ）。
        /// </summary>
        /// <param name="vertices">対象頂点（meshRoot のローカル座標）</param>
        /// <param name="meshRoot">メッシュのルート Transform</param>
        /// <param name="topology">平滑化に使う溶接トポロジー。null の場合は内部で構築する。</param>
        /// <returns>計算済みボーンウェイト。前提条件を満たさない場合は null</returns>
        public BoneWeight[] Calculate(Vector3[] vertices, Transform meshRoot, ClayMeshTopology? topology)
        {
            if (bones == null || bones.Length == 0 || vertices == null || vertices.Length == 0)
            {
                return null;
            }

            int boneCount = bones.Length;
            int vertexCount = vertices.Length;

            // バインドポーズとボーン線分（meshRoot ローカル空間）を準備する
            BindPoses = new Matrix4x4[boneCount];
            Vector3[] boneLocalPos = new Vector3[boneCount];
            int[] boneParentIndex = new int[boneCount];

            Matrix4x4 worldToLocal = meshRoot.worldToLocalMatrix;

            for (int i = 0; i < boneCount; i++)
            {
                if (bones[i] != null)
                {
                    BindPoses[i] = bones[i].worldToLocalMatrix * meshRoot.localToWorldMatrix;
                    boneLocalPos[i] = worldToLocal.MultiplyPoint3x4(bones[i].position);
                }
                else
                {
                    BindPoses[i] = Matrix4x4.identity;
                    boneLocalPos[i] = Vector3.zero;
                }

                boneParentIndex[i] = FindParentBoneIndex(i);
            }

            // 各頂点について、ボーン線分への最短距離から重みを求める
            float[][] weightTable = new float[vertexCount][];
            float pow = Mathf.Max(weightFalloffPower, 0.1f);

            for (int v = 0; v < vertexCount; v++)
            {
                Vector3 p = vertices[v];
                float[] w = new float[boneCount];
                float sum = 0f;

                for (int b = 0; b < boneCount; b++)
                {
                    if (bones[b] == null)
                    {
                        continue;
                    }

                    float dist;
                    int parent = boneParentIndex[b];
                    if (parent >= 0)
                    {
                        dist = DistancePointToSegment(p, boneLocalPos[parent], boneLocalPos[b]);
                    }
                    else
                    {
                        dist = Vector3.Distance(p, boneLocalPos[b]);
                    }

                    float weight = 1f / Mathf.Pow(Mathf.Max(dist, 0.0001f), pow);
                    w[b] = weight;
                    sum += weight;
                }

                if (sum > 0f)
                {
                    for (int b = 0; b < boneCount; b++)
                    {
                        w[b] /= sum;
                    }
                }

                weightTable[v] = w;
            }

            // 案4：メッシュの隣接でウェイトを平滑化（折り目を緩和）
            if (smoothingIterations > 0 && smoothingStrength > 0f)
            {
                // 外部トポロジーがあれば使い回し、無ければ内部で構築する（二重溶接の回避）
                ClayMeshTopology topo = topology ?? ClayMeshTopology.Build(vertices, weldEpsilon);
                SmoothWeights(vertices, topo, weightTable, boneCount);
            }

            // 上位 maxBonesPerVertex 本に絞って BoneWeight へ変換する
            BoneWeight[] result = new BoneWeight[vertexCount];
            for (int v = 0; v < vertexCount; v++)
            {
                result[v] = ToBoneWeight(weightTable[v], boneCount);
            }

            return result;
        }

        // ボーン bones[index] の親が集合内のどのボーンかを返す（無ければ -1）
        private int FindParentBoneIndex(int index)
        {
            if (bones[index] == null)
            {
                return -1;
            }

            Transform? parent = bones[index].parent;
            if (parent == null)
            {
                return -1;
            }

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == parent)
                {
                    return i;
                }
            }

            return -1;
        }

        // 点 p から線分 a-b への最短距離
        private static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr < 1e-8f)
            {
                return Vector3.Distance(p, a);
            }

            float t = Vector3.Dot(p - a, ab) / abSqr;
            t = Mathf.Clamp01(t);
            Vector3 closest = a + ab * t;
            return Vector3.Distance(p, closest);
        }

        // 案4：渡された溶接トポロジーの隣接でウェイトをラプラシアン平滑化する
        private void SmoothWeights(Vector3[] vertices, ClayMeshTopology topo, float[][] weightTable, int boneCount)
        {
            int uniqueCount = topo.Positions.Length;
            int[] remap = BuildRemap(vertices, topo);

            // ユニーク頂点ごとのウェイト（スープ頂点の平均で初期化）
            float[][] uniqueWeights = new float[uniqueCount][];
            int[] counts = new int[uniqueCount];
            for (int u = 0; u < uniqueCount; u++)
            {
                uniqueWeights[u] = new float[boneCount];
            }

            for (int v = 0; v < vertices.Length; v++)
            {
                int u = remap[v];
                float[] src = weightTable[v];
                float[] dst = uniqueWeights[u];
                for (int b = 0; b < boneCount; b++)
                {
                    dst[b] += src[b];
                }
                counts[u]++;
            }

            for (int u = 0; u < uniqueCount; u++)
            {
                if (counts[u] > 0)
                {
                    float inv = 1f / counts[u];
                    for (int b = 0; b < boneCount; b++)
                    {
                        uniqueWeights[u][b] *= inv;
                    }
                }
            }

            // 隣接平均へ寄せる反復
            float[][] buffer = new float[uniqueCount][];
            for (int u = 0; u < uniqueCount; u++)
            {
                buffer[u] = new float[boneCount];
            }

            for (int iter = 0; iter < smoothingIterations; iter++)
            {
                for (int u = 0; u < uniqueCount; u++)
                {
                    var neighbors = topo.Adjacency[u];
                    float[] avg = buffer[u];
                    for (int b = 0; b < boneCount; b++)
                    {
                        avg[b] = 0f;
                    }

                    if (neighbors.Count > 0)
                    {
                        foreach (int nb in neighbors)
                        {
                            float[] nw = uniqueWeights[nb];
                            for (int b = 0; b < boneCount; b++)
                            {
                                avg[b] += nw[b];
                            }
                        }

                        float inv = 1f / neighbors.Count;
                        for (int b = 0; b < boneCount; b++)
                        {
                            avg[b] *= inv;
                        }
                    }
                    else
                    {
                        for (int b = 0; b < boneCount; b++)
                        {
                            avg[b] = uniqueWeights[u][b];
                        }
                    }
                }

                for (int u = 0; u < uniqueCount; u++)
                {
                    float[] cur = uniqueWeights[u];
                    float[] avg = buffer[u];
                    for (int b = 0; b < boneCount; b++)
                    {
                        cur[b] = Mathf.Lerp(cur[b], avg[b], smoothingStrength);
                    }
                }
            }

            // ユニーク頂点の結果をスープ頂点へ書き戻す
            for (int v = 0; v < vertices.Length; v++)
            {
                weightTable[v] = uniqueWeights[remap[v]];
            }
        }

        // スープ頂点 → 溶接後ユニーク頂点インデックスの対応表（トポロジーの座標から逆引き）
        private int[] BuildRemap(Vector3[] vertices, ClayMeshTopology topo)
        {
            var keyToIndex = new System.Collections.Generic.Dictionary<long, int>(topo.Positions.Length);
            float inv = 1f / Mathf.Max(weldEpsilon, 1e-5f);

            for (int u = 0; u < topo.Positions.Length; u++)
            {
                long key = QuantizeKey(topo.Positions[u], inv);
                if (!keyToIndex.ContainsKey(key))
                {
                    keyToIndex.Add(key, u);
                }
            }

            int[] remap = new int[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                long key = QuantizeKey(vertices[v], inv);
                remap[v] = keyToIndex.TryGetValue(key, out int u) ? u : 0;
            }

            return remap;
        }

        // MeshTopology と同一の量子化キー
        private static long QuantizeKey(Vector3 v, float inv)
        {
            long x = (long)Mathf.Round(v.x * inv);
            long y = (long)Mathf.Round(v.y * inv);
            long z = (long)Mathf.Round(v.z * inv);

            const long mask = 0x1FFFFF;
            return ((x & mask) << 42) | ((y & mask) << 21) | (z & mask);
        }

        // ウェイト配列から上位 maxBonesPerVertex 本を取り出して BoneWeight に正規化する
        private BoneWeight ToBoneWeight(float[] weights, int boneCount)
        {
            int b0 = -1, b1 = -1, b2 = -1, b3 = -1;
            float w0 = 0f, w1 = 0f, w2 = 0f, w3 = 0f;

            for (int b = 0; b < boneCount; b++)
            {
                float w = weights[b];
                if (w > w0)
                {
                    b3 = b2; w3 = w2;
                    b2 = b1; w2 = w1;
                    b1 = b0; w1 = w0;
                    b0 = b; w0 = w;
                }
                else if (w > w1)
                {
                    b3 = b2; w3 = w2;
                    b2 = b1; w2 = w1;
                    b1 = b; w1 = w;
                }
                else if (w > w2)
                {
                    b3 = b2; w3 = w2;
                    b2 = b; w2 = w;
                }
                else if (w > w3)
                {
                    b3 = b; w3 = w;
                }
            }

            if (maxBonesPerVertex < 4)
            {
                b3 = -1; w3 = 0f;
            }
            if (maxBonesPerVertex < 3)
            {
                b2 = -1; w2 = 0f;
            }
            if (maxBonesPerVertex < 2)
            {
                b1 = -1; w1 = 0f;
            }

            float sum = w0 + w1 + w2 + w3;
            if (sum <= 0f)
            {
                return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }

            float invSum = 1f / sum;
            return new BoneWeight
            {
                boneIndex0 = b0 < 0 ? 0 : b0,
                boneIndex1 = b1 < 0 ? 0 : b1,
                boneIndex2 = b2 < 0 ? 0 : b2,
                boneIndex3 = b3 < 0 ? 0 : b3,
                weight0 = w0 * invSum,
                weight1 = w1 * invSum,
                weight2 = w2 * invSum,
                weight3 = w3 * invSum
            };
        }

        /// <summary>
        /// BoneRoot 配下の Transform からボーン配列を自動構築する。
        /// </summary>
        [ContextMenu("Auto Setup Bones From Root")]
        public void AutoSetup()
        {
            if (BoneRoot != null)
            {
                bones = BoneRoot.GetComponentsInChildren<Transform>(true);
            }
            else
            {
                Debug.LogWarning("[ClayAutoRigger] BoneRoot が null のためボーンを設定できません。");
            }
        }
    }
}