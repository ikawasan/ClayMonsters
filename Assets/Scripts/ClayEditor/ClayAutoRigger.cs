#nullable enable
using ClayEditor.Interface;
using System;
using UnityEngine;
using VContainer;
using ClayMeshTopology = ClayEditor.Rigging.MeshTopology;

namespace ClayEditor
{
    /// <summary>
    /// メッシュ頂点へボーンウェイトを自動割り当てする
    /// </summary>
    public class ClayAutoRigger : MonoBehaviour
    {
        [Inject] private readonly IClaySceneContext sceneContext = default!;

        private Transform[] bones = Array.Empty<Transform>();

        /// <summary>
        /// スキニングに用いるボーンTransform群
        /// </summary>
        public Transform[] Bones => bones;

        [Header("Weight Settings")]
        [Tooltip("距離の重み付けの鋭さ大きいほど最寄りボーンに偏る")]
        [SerializeField] private float weightFalloffPower = 3.5f;

        [Tooltip("各頂点に割り当てるボーンの最大数最大4")]
        [Range(1, 4)]
        [SerializeField] private int maxBonesPerVertex = 4;

        [Tooltip("候補に残す近傍ボーン数")]
        [Range(2, 12)]
        [SerializeField] private int candidateBoneCount = 8;

        [Tooltip("最寄り距離に対する候補半径倍率")]
        [SerializeField] private float candidateRadiusScale = 3.5f;

        [Tooltip("優勢ボーンを強める閾値1.0固定にはしない")]
        [Range(0.55f, 0.98f)]
        [SerializeField] private float softPinThreshold = 0.88f;

        [Tooltip("優勢時に残す第2ボーンの下限")]
        [Range(0f, 0.35f)]
        [SerializeField] private float softPinSecondaryFloor = 0.12f;

        [Tooltip("親子関節で親へ流す最小ブレンド")]
        [Range(0f, 0.45f)]
        [SerializeField] private float jointParentBlend = 0.22f;

        [Header("Smoothing Settings")]
        [Tooltip("隣接平滑化の反復回数0で無効")]
        [Range(0, 10)]
        [SerializeField] private int smoothingIterations = 5;

        [Tooltip("平滑化の強さ0〜1隣接平均へ寄せる割合")]
        [Range(0f, 1f)]
        [SerializeField] private float smoothingStrength = 0.45f;

        [Tooltip("支配ボーンが違う隣との追加ブレンド")]
        [Range(0f, 1f)]
        [SerializeField] private float seamBlendStrength = 0.55f;

        [Tooltip("溶接で同一頂点とみなすグリッドサイズ外部トポロジー未提供時のみ使用")]
        [SerializeField] private float weldEpsilon = 0.01f;

        /// <summary>
        /// 現在編集中のモデルのボーンルート
        /// </summary>
        public Transform? BoneRoot
        {
            get
            {
                if (Application.isPlaying && sceneContext != null)
                {
                    return sceneContext.CurrentModel.Value?.BoneRoot;
                }

                return null;
            }
        }

        /// <summary>
        /// 直近の計算で生成されたバインドポーズ行列群
        /// </summary>
        public Matrix4x4[] BindPoses { get; private set; } = Array.Empty<Matrix4x4>();

        /// <summary>
        /// スキニングに用いるボーンTransform群を外部から設定する
        /// </summary>
        /// <param name="newBones">設定するボーンTransform群</param>
        public void SetBones(Transform[] newBones)
        {
            bones = newBones ?? Array.Empty<Transform>();
        }

        /// <summary>
        /// 頂点群に対するボーンウェイトを計算するトポロジー未指定時は内部で溶接する
        /// </summary>
        /// <param name="vertices">対象頂点meshRootのローカル座標</param>
        /// <param name="meshRoot">メッシュのルートTransform</param>
        /// <returns>計算済みボーンウェイト前提条件を満たさない場合はnull</returns>
        public BoneWeight[] Calculate(Vector3[] vertices, Transform meshRoot)
        {
            return Calculate(vertices, meshRoot, null);
        }

        /// <summary>
        /// 頂点群に対するボーンウェイトを計算する
        /// 溶接頂点上で近傍ボーン限定の距離加重を行い関節ブレンドと境界平滑で境目をならす
        /// </summary>
        /// <param name="vertices">対象頂点meshRootのローカル座標</param>
        /// <param name="meshRoot">メッシュのルートTransform</param>
        /// <param name="topology">平滑化に使う溶接トポロジーnullの場合は内部で構築する</param>
        /// <returns>計算済みボーンウェイト前提条件を満たさない場合はnull</returns>
        public BoneWeight[] Calculate(Vector3[] vertices, Transform meshRoot, ClayMeshTopology? topology)
        {
            if (bones == null || bones.Length == 0 || vertices == null || vertices.Length == 0 || meshRoot == null)
            {
                return null;
            }

            int boneCount = bones.Length;
            ClayMeshTopology topo = topology ?? ClayMeshTopology.Build(vertices, weldEpsilon);
            int[] remap = BuildRemap(vertices, topo);
            Vector3[] uniquePositions = topo.Positions;
            int uniqueCount = uniquePositions.Length;

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

            float[] boneThickness = EstimateBoneThickness(uniquePositions, boneLocalPos, boneParentIndex);

            float[][] uniqueWeights = new float[uniqueCount][];
            float[] confidence = new float[uniqueCount];
            float pow = Mathf.Max(weightFalloffPower, 0.1f);
            int candidateLimit = Mathf.Clamp(candidateBoneCount, 2, Mathf.Min(12, boneCount));

            for (int u = 0; u < uniqueCount; u++)
            {
                uniqueWeights[u] = ComputeVertexWeights(
                    uniquePositions[u],
                    boneLocalPos,
                    boneParentIndex,
                    boneThickness,
                    boneCount,
                    candidateLimit,
                    pow,
                    out _);

                ApplyParentJointBlend(
                    uniqueWeights[u],
                    uniquePositions[u],
                    boneLocalPos,
                    boneParentIndex,
                    boneCount);

                int dominant = FindDominantBone(uniqueWeights[u]);
                float conf = dominant >= 0 ? uniqueWeights[u][dominant] : 0f;
                SoftPinDominant(uniqueWeights[u], conf);
                dominant = FindDominantBone(uniqueWeights[u]);
                confidence[u] = dominant >= 0 ? uniqueWeights[u][dominant] : 0f;
            }

            if (smoothingIterations > 0 && smoothingStrength > 0f)
            {
                SmoothWeightsWithSeamBlend(topo, uniqueWeights, confidence, boneCount);
            }

            for (int u = 0; u < uniqueCount; u++)
            {
                TruncateAndNormalize(uniqueWeights[u], maxBonesPerVertex);
            }

            BoneWeight[] result = new BoneWeight[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                result[v] = ToBoneWeight(uniqueWeights[remap[v]], boneCount);
            }

            return result;
        }

        /// <summary>
        /// BoneRoot配下のTransformからボーン配列を自動構築する
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
                Debug.LogWarning("[ClayAutoRigger] BoneRootがnullのためボーンを設定できません");
            }
        }

        private float[] ComputeVertexWeights(
            Vector3 point,
            Vector3[] boneLocalPos,
            int[] boneParentIndex,
            float[] boneThickness,
            int boneCount,
            int candidateLimit,
            float pow,
            out float maxWeight)
        {
            float[] distances = new float[boneCount];
            int validCount = 0;
            float nearest = float.MaxValue;

            for (int b = 0; b < boneCount; b++)
            {
                if (bones[b] == null)
                {
                    distances[b] = float.MaxValue;
                    continue;
                }

                float dist = ResolveBoneDistance(point, b, boneLocalPos, boneParentIndex);
                // 骨の太さ分だけ距離を縮めて太い部位の拘束を強める
                dist = Mathf.Max(0.0001f, dist - boneThickness[b] * 0.35f);
                distances[b] = dist;
                nearest = Mathf.Min(nearest, dist);
                validCount++;
            }

            float[] weights = new float[boneCount];
            maxWeight = 0f;
            if (validCount == 0 || nearest >= float.MaxValue * 0.5f)
            {
                weights[0] = 1f;
                maxWeight = 1f;
                return weights;
            }

            float radius = nearest * Mathf.Max(1.1f, candidateRadiusScale) + 0.0001f;
            int[] candidateIndices = new int[candidateLimit];
            float[] candidateDistances = new float[candidateLimit];
            int candidateFilled = 0;

            int nearestBone = 0;
            float nearestDist = float.MaxValue;
            for (int b = 0; b < boneCount; b++)
            {
                if (distances[b] < nearestDist)
                {
                    nearestDist = distances[b];
                    nearestBone = b;
                }
            }

            for (int b = 0; b < boneCount; b++)
            {
                float dist = distances[b];
                if (dist > radius)
                {
                    continue;
                }

                InsertCandidate(candidateIndices, candidateDistances, ref candidateFilled, candidateLimit, b, dist);
            }

            // 最寄りとその親は必ず候補に入れ関節の切れ目を防ぐ
            ForceCandidate(candidateIndices, candidateDistances, ref candidateFilled, candidateLimit, nearestBone, distances[nearestBone]);
            int nearestParent = boneParentIndex[nearestBone];
            if (nearestParent >= 0)
            {
                ForceCandidate(
                    candidateIndices,
                    candidateDistances,
                    ref candidateFilled,
                    candidateLimit,
                    nearestParent,
                    distances[nearestParent]);
            }

            // 半径内が足りないときは最寄り順で埋める
            if (candidateFilled < Mathf.Min(candidateLimit, validCount))
            {
                for (int b = 0; b < boneCount; b++)
                {
                    if (distances[b] >= float.MaxValue * 0.5f)
                    {
                        continue;
                    }

                    bool exists = false;
                    for (int c = 0; c < candidateFilled; c++)
                    {
                        if (candidateIndices[c] == b)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (exists)
                    {
                        continue;
                    }

                    InsertCandidate(candidateIndices, candidateDistances, ref candidateFilled, candidateLimit, b, distances[b]);
                }
            }

            float sum = 0f;
            float localScale = Mathf.Max(nearest, 0.0001f);
            for (int c = 0; c < candidateFilled; c++)
            {
                int b = candidateIndices[c];
                float normalizedDist = candidateDistances[c] / localScale;
                float weight = 1f / Mathf.Pow(Mathf.Max(normalizedDist, 0.0001f), pow);
                weights[b] = weight;
                sum += weight;
            }

            if (sum <= 0f)
            {
                weights[nearestBone] = 1f;
                maxWeight = 1f;
                return weights;
            }

            float invSum = 1f / sum;
            for (int b = 0; b < boneCount; b++)
            {
                weights[b] *= invSum;
                maxWeight = Mathf.Max(maxWeight, weights[b]);
            }

            return weights;
        }

        private static void InsertCandidate(
            int[] indices,
            float[] distances,
            ref int filled,
            int capacity,
            int boneIndex,
            float distance)
        {
            for (int i = 0; i < filled; i++)
            {
                if (indices[i] == boneIndex)
                {
                    if (distance < distances[i])
                    {
                        distances[i] = distance;
                    }

                    return;
                }
            }

            if (filled < capacity)
            {
                indices[filled] = boneIndex;
                distances[filled] = distance;
                filled++;
                return;
            }

            int worst = 0;
            for (int i = 1; i < capacity; i++)
            {
                if (distances[i] > distances[worst])
                {
                    worst = i;
                }
            }

            if (distance >= distances[worst])
            {
                return;
            }

            indices[worst] = boneIndex;
            distances[worst] = distance;
        }

        private static void ForceCandidate(
            int[] indices,
            float[] distances,
            ref int filled,
            int capacity,
            int boneIndex,
            float distance)
        {
            if (boneIndex < 0 || distance >= float.MaxValue * 0.5f || capacity <= 0)
            {
                return;
            }

            for (int i = 0; i < filled; i++)
            {
                if (indices[i] == boneIndex)
                {
                    distances[i] = Mathf.Min(distances[i], distance);
                    return;
                }
            }

            if (filled < capacity)
            {
                indices[filled] = boneIndex;
                distances[filled] = distance;
                filled++;
                return;
            }

            int worst = 0;
            for (int i = 1; i < capacity; i++)
            {
                if (distances[i] > distances[worst])
                {
                    worst = i;
                }
            }

            indices[worst] = boneIndex;
            distances[worst] = distance;
        }

        private float[] EstimateBoneThickness(
            Vector3[] uniquePositions,
            Vector3[] boneLocalPos,
            int[] boneParentIndex)
        {
            int boneCount = boneLocalPos.Length;
            float[] thickness = new float[boneCount];
            float[] accum = new float[boneCount];
            int[] counts = new int[boneCount];

            // 各頂点の最寄りボーンへ距離を集め中央寄りの太さ推定にする
            for (int u = 0; u < uniquePositions.Length; u++)
            {
                Vector3 p = uniquePositions[u];
                float bestDist = float.MaxValue;
                int bestBone = -1;
                for (int b = 0; b < boneCount; b++)
                {
                    if (bones[b] == null)
                    {
                        continue;
                    }

                    float dist = ResolveBoneDistance(p, b, boneLocalPos, boneParentIndex);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestBone = b;
                    }
                }

                if (bestBone >= 0)
                {
                    accum[bestBone] += bestDist;
                    counts[bestBone]++;
                }
            }

            float globalFallback = 0.05f;
            int measured = 0;
            float measuredSum = 0f;
            for (int b = 0; b < boneCount; b++)
            {
                if (counts[b] > 0)
                {
                    thickness[b] = accum[b] / counts[b];
                    measuredSum += thickness[b];
                    measured++;
                }
            }

            if (measured > 0)
            {
                globalFallback = measuredSum / measured;
            }

            for (int b = 0; b < boneCount; b++)
            {
                if (counts[b] <= 0)
                {
                    thickness[b] = globalFallback;
                }

                thickness[b] = Mathf.Clamp(thickness[b], globalFallback * 0.25f, globalFallback * 3f);
            }

            return thickness;
        }

        private static float ResolveBoneDistance(
            Vector3 point,
            int boneIndex,
            Vector3[] boneLocalPos,
            int[] boneParentIndex)
        {
            int parent = boneParentIndex[boneIndex];
            if (parent >= 0)
            {
                return DistancePointToSegment(point, boneLocalPos[parent], boneLocalPos[boneIndex]);
            }

            return Vector3.Distance(point, boneLocalPos[boneIndex]);
        }

        private void ApplyParentJointBlend(
            float[] weights,
            Vector3 point,
            Vector3[] boneLocalPos,
            int[] boneParentIndex,
            int boneCount)
        {
            int dominant = FindDominantBone(weights);
            if (dominant < 0)
            {
                return;
            }

            int parent = boneParentIndex[dominant];
            if (parent < 0)
            {
                return;
            }

            // 線分上の位置が親側ほど親ウェイトを厚くする
            float along = 0.5f;
            Vector3 a = boneLocalPos[parent];
            Vector3 b = boneLocalPos[dominant];
            Vector3 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr > 1e-8f)
            {
                along = Mathf.Clamp01(Vector3.Dot(point - a, ab) / abSqr);
            }

            float parentShare = Mathf.Lerp(Mathf.Max(jointParentBlend, 0.28f), jointParentBlend * 0.35f, along);
            parentShare = Mathf.Clamp(parentShare, 0.05f, 0.45f);
            float childKeep = 1f - parentShare;
            float childWeight = weights[dominant];
            float injected = childWeight * parentShare;
            weights[dominant] = childWeight * childKeep;
            weights[parent] += injected;
            NormalizeWeights(weights);
        }

        private void SoftPinDominant(float[] weights, float maxWeight)
        {
            if (maxWeight < softPinThreshold)
            {
                return;
            }

            int dominant = FindDominantBone(weights);
            if (dominant < 0)
            {
                return;
            }

            int secondary = FindSecondBone(weights, dominant);
            float secondaryKeep = Mathf.Clamp(softPinSecondaryFloor, 0.08f, 0.35f);
            if (secondary >= 0)
            {
                secondaryKeep = Mathf.Max(weights[secondary], secondaryKeep);
            }
            else
            {
                secondary = (dominant + 1) % weights.Length;
                if (secondary == dominant)
                {
                    return;
                }
            }

            secondaryKeep = Mathf.Min(secondaryKeep, 0.35f);
            float dominantShare = 1f - secondaryKeep;
            for (int b = 0; b < weights.Length; b++)
            {
                if (b == dominant)
                {
                    weights[b] = dominantShare;
                }
                else if (b == secondary)
                {
                    weights[b] = secondaryKeep;
                }
                else
                {
                    weights[b] = 0f;
                }
            }
        }

        private static int FindDominantBone(float[] weights)
        {
            int best = -1;
            float bestWeight = -1f;
            for (int b = 0; b < weights.Length; b++)
            {
                if (weights[b] > bestWeight)
                {
                    bestWeight = weights[b];
                    best = b;
                }
            }

            return best;
        }

        private static int FindSecondBone(float[] weights, int dominant)
        {
            int best = -1;
            float bestWeight = -1f;
            for (int b = 0; b < weights.Length; b++)
            {
                if (b == dominant)
                {
                    continue;
                }

                if (weights[b] > bestWeight)
                {
                    bestWeight = weights[b];
                    best = b;
                }
            }

            return bestWeight > 0.001f ? best : -1;
        }

        private static void NormalizeWeights(float[] weights)
        {
            float sum = 0f;
            for (int b = 0; b < weights.Length; b++)
            {
                sum += weights[b];
            }

            if (sum <= 0f)
            {
                weights[0] = 1f;
                return;
            }

            float inv = 1f / sum;
            for (int b = 0; b < weights.Length; b++)
            {
                weights[b] *= inv;
            }
        }

        private static void TruncateAndNormalize(float[] weights, int maxBones)
        {
            int limit = Mathf.Clamp(maxBones, 1, 4);
            int[] top = new int[4];
            float[] topW = new float[4];
            for (int i = 0; i < 4; i++)
            {
                top[i] = -1;
            }

            for (int b = 0; b < weights.Length; b++)
            {
                float w = weights[b];
                if (w > topW[0])
                {
                    top[3] = top[2]; topW[3] = topW[2];
                    top[2] = top[1]; topW[2] = topW[1];
                    top[1] = top[0]; topW[1] = topW[0];
                    top[0] = b; topW[0] = w;
                }
                else if (w > topW[1])
                {
                    top[3] = top[2]; topW[3] = topW[2];
                    top[2] = top[1]; topW[2] = topW[1];
                    top[1] = b; topW[1] = w;
                }
                else if (w > topW[2])
                {
                    top[3] = top[2]; topW[3] = topW[2];
                    top[2] = b; topW[2] = w;
                }
                else if (w > topW[3])
                {
                    top[3] = b; topW[3] = w;
                }
            }

            Array.Clear(weights, 0, weights.Length);
            float sum = 0f;
            for (int i = 0; i < limit; i++)
            {
                if (top[i] < 0 || topW[i] <= 0f)
                {
                    continue;
                }

                weights[top[i]] = topW[i];
                sum += topW[i];
            }

            if (sum <= 0f)
            {
                weights[0] = 1f;
                return;
            }

            float inv = 1f / sum;
            for (int b = 0; b < weights.Length; b++)
            {
                weights[b] *= inv;
            }
        }

        private void SmoothWeightsWithSeamBlend(
            ClayMeshTopology topo,
            float[][] uniqueWeights,
            float[] confidence,
            int boneCount)
        {
            int uniqueCount = uniqueWeights.Length;
            float[][] buffer = new float[uniqueCount][];
            int[] dominant = new int[uniqueCount];
            for (int u = 0; u < uniqueCount; u++)
            {
                buffer[u] = new float[boneCount];
                dominant[u] = FindDominantBone(uniqueWeights[u]);
            }

            for (int iter = 0; iter < smoothingIterations; iter++)
            {
                for (int u = 0; u < uniqueCount; u++)
                {
                    var neighbors = topo.Adjacency[u];
                    float[] avg = buffer[u];
                    Array.Clear(avg, 0, boneCount);

                    if (neighbors.Count == 0)
                    {
                        Array.Copy(uniqueWeights[u], avg, boneCount);
                        continue;
                    }

                    float weightSum = 0f;
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        int nb = neighbors[n];
                        float seamBoost = dominant[u] != dominant[nb] ? 1f + seamBlendStrength : 1f;
                        float[] nw = uniqueWeights[nb];
                        for (int b = 0; b < boneCount; b++)
                        {
                            avg[b] += nw[b] * seamBoost;
                        }

                        weightSum += seamBoost;
                    }

                    if (weightSum <= 0f)
                    {
                        Array.Copy(uniqueWeights[u], avg, boneCount);
                        continue;
                    }

                    float inv = 1f / weightSum;
                    for (int b = 0; b < boneCount; b++)
                    {
                        avg[b] *= inv;
                    }

                    NormalizeWeights(avg);
                }

                for (int u = 0; u < uniqueCount; u++)
                {
                    // 自信が高い頂点も少しは混ぜ境目の段差を消す
                    float localStrength = Mathf.Lerp(smoothingStrength, smoothingStrength * 0.35f, confidence[u]);
                    bool hasSeamNeighbor = false;
                    var neighbors = topo.Adjacency[u];
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        if (dominant[neighbors[n]] != dominant[u])
                        {
                            hasSeamNeighbor = true;
                            break;
                        }
                    }

                    if (hasSeamNeighbor)
                    {
                        localStrength = Mathf.Max(localStrength, seamBlendStrength);
                    }

                    float[] cur = uniqueWeights[u];
                    float[] avg = buffer[u];
                    for (int b = 0; b < boneCount; b++)
                    {
                        cur[b] = Mathf.Lerp(cur[b], avg[b], localStrength);
                    }

                    NormalizeWeights(cur);
                    dominant[u] = FindDominantBone(cur);
                    confidence[u] = cur[dominant[u]];
                }
            }
        }

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

        private static long QuantizeKey(Vector3 v, float inv)
        {
            long x = (long)Mathf.Round(v.x * inv);
            long y = (long)Mathf.Round(v.y * inv);
            long z = (long)Mathf.Round(v.z * inv);

            const long mask = 0x1FFFFF;
            return ((x & mask) << 42) | ((y & mask) << 21) | (z & mask);
        }

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
            if (sum <= 0f || b0 < 0)
            {
                return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }

            float invSum = 1f / sum;
            return new BoneWeight
            {
                boneIndex0 = b0,
                boneIndex1 = b1 < 0 ? 0 : b1,
                boneIndex2 = b2 < 0 ? 0 : b2,
                boneIndex3 = b3 < 0 ? 0 : b3,
                weight0 = w0 * invSum,
                weight1 = b1 < 0 ? 0f : w1 * invSum,
                weight2 = b2 < 0 ? 0f : w2 * invSum,
                weight3 = b3 < 0 ? 0f : w3 * invSum
            };
        }
    }
}
