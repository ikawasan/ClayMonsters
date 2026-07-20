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
        [SerializeField] private float weightFalloffPower = 4.5f;

        [Tooltip("各頂点に割り当てるボーンの最大数最大4")]
        [Range(1, 4)]
        [SerializeField] private int maxBonesPerVertex = 4;

        [Tooltip("候補に残す近傍ボーン数")]
        [Range(2, 12)]
        [SerializeField] private int candidateBoneCount = 6;

        [Tooltip("最寄り距離に対する候補半径倍率")]
        [SerializeField] private float candidateRadiusScale = 2.5f;

        [Tooltip("優勢ボーンを強める閾値")]
        [Range(0.55f, 0.98f)]
        [SerializeField] private float softPinThreshold = 0.9f;

        [Tooltip("優勢時に残す第2ボーンの下限親子のみ")]
        [Range(0f, 0.35f)]
        [SerializeField] private float softPinSecondaryFloor = 0.1f;

        [Tooltip("親子関節で親へ流す最小ブレンド")]
        [Range(0f, 0.45f)]
        [SerializeField] private float jointParentBlend = 0.16f;

        [Header("Smoothing Settings")]
        [Tooltip("隣接平滑化の反復回数0で無効")]
        [Range(0, 10)]
        [SerializeField] private int smoothingIterations = 3;

        [Tooltip("平滑化の強さ0〜1隣接平均へ寄せる割合")]
        [Range(0f, 1f)]
        [SerializeField] private float smoothingStrength = 0.3f;

        [Tooltip("親子関節境目の追加ブレンド")]
        [Range(0f, 1f)]
        [SerializeField] private float seamBlendStrength = 0.35f;

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
            int[] limbBranchIds = BuildLimbBranchIds(boneParentIndex);
            int rootBoneIndex = FindRootBoneIndex(boneParentIndex);
            Vector3 rootLocalPos = rootBoneIndex >= 0 ? boneLocalPos[rootBoneIndex] : Vector3.zero;
            float lateralThreshold = EstimateLateralThreshold(boneLocalPos, rootLocalPos, rootBoneIndex);

            float[][] uniqueWeights = new float[uniqueCount][];
            float[] confidence = new float[uniqueCount];
            int[] dominantBones = new int[uniqueCount];
            float pow = Mathf.Max(weightFalloffPower, 0.1f);
            int candidateLimit = Mathf.Clamp(candidateBoneCount, 2, Mathf.Min(12, boneCount));

            for (int u = 0; u < uniqueCount; u++)
            {
                uniqueWeights[u] = ComputeVertexWeights(
                    uniquePositions[u],
                    boneLocalPos,
                    boneParentIndex,
                    boneThickness,
                    limbBranchIds,
                    rootLocalPos,
                    lateralThreshold,
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

                StripOppositeLimbWeights(
                    uniqueWeights[u],
                    limbBranchIds,
                    boneParentIndex,
                    boneLocalPos,
                    rootLocalPos,
                    lateralThreshold);
                int dominant = FindDominantBone(uniqueWeights[u]);
                float conf = dominant >= 0 ? uniqueWeights[u][dominant] : 0f;
                SoftPinDominant(uniqueWeights[u], conf, boneParentIndex, limbBranchIds);
                dominant = FindDominantBone(uniqueWeights[u]);
                dominantBones[u] = dominant;
                confidence[u] = dominant >= 0 ? uniqueWeights[u][dominant] : 0f;
            }

            if (smoothingIterations > 0 && smoothingStrength > 0f)
            {
                SmoothWeightsWithSeamBlend(
                    topo,
                    uniqueWeights,
                    confidence,
                    dominantBones,
                    boneParentIndex,
                    limbBranchIds,
                    boneLocalPos,
                    rootLocalPos,
                    lateralThreshold,
                    boneCount);
            }

            for (int u = 0; u < uniqueCount; u++)
            {
                StripOppositeLimbWeights(
                    uniqueWeights[u],
                    limbBranchIds,
                    boneParentIndex,
                    boneLocalPos,
                    rootLocalPos,
                    lateralThreshold);
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
            int[] limbBranchIds,
            Vector3 rootLocalPos,
            float lateralThreshold,
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
                // 左右反対側の肢は距離を伸ばして混入を防ぐ
                dist *= ResolveLateralPenalty(point, boneLocalPos[b], rootLocalPos, lateralThreshold);
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

                if (!IsAllowedLimbInfluence(nearestBone, b, limbBranchIds, boneParentIndex))
                {
                    continue;
                }

                InsertCandidate(candidateIndices, candidateDistances, ref candidateFilled, candidateLimit, b, dist);
            }

            // 最寄りとその親は必ず候補に入れ関節の切れ目を防ぐ
            ForceCandidate(candidateIndices, candidateDistances, ref candidateFilled, candidateLimit, nearestBone, distances[nearestBone]);
            int nearestParent = boneParentIndex[nearestBone];
            if (nearestParent >= 0
                && IsAllowedLimbInfluence(nearestBone, nearestParent, limbBranchIds, boneParentIndex))
            {
                ForceCandidate(
                    candidateIndices,
                    candidateDistances,
                    ref candidateFilled,
                    candidateLimit,
                    nearestParent,
                    distances[nearestParent]);
            }

            // 半径内が足りないときは同肢内の最寄り順で埋める
            if (candidateFilled < Mathf.Min(candidateLimit, validCount))
            {
                for (int b = 0; b < boneCount; b++)
                {
                    if (distances[b] >= float.MaxValue * 0.5f)
                    {
                        continue;
                    }

                    if (!IsAllowedLimbInfluence(nearestBone, b, limbBranchIds, boneParentIndex))
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

        private static float ResolveLateralPenalty(
            Vector3 point,
            Vector3 bonePos,
            Vector3 rootPos,
            float lateralThreshold)
        {
            float pointSide = point.x - rootPos.x;
            float boneSide = bonePos.x - rootPos.x;
            float threshold = Mathf.Max(0.02f, lateralThreshold);
            if (Mathf.Abs(pointSide) < threshold || Mathf.Abs(boneSide) < threshold)
            {
                return 1f;
            }

            if (pointSide * boneSide < 0f)
            {
                return 12f;
            }

            return 1f;
        }

        private static float EstimateLateralThreshold(Vector3[] boneLocalPos, Vector3 rootLocalPos, int rootBoneIndex)
        {
            float maxAbs = 0.05f;
            for (int i = 0; i < boneLocalPos.Length; i++)
            {
                if (i == rootBoneIndex)
                {
                    continue;
                }

                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(boneLocalPos[i].x - rootLocalPos.x));
            }

            return Mathf.Max(0.03f, maxAbs * 0.08f);
        }

        private int FindRootBoneIndex(int[] boneParentIndex)
        {
            for (int i = 0; i < boneParentIndex.Length; i++)
            {
                if (bones[i] != null && boneParentIndex[i] < 0)
                {
                    return i;
                }
            }

            return bones.Length > 0 ? 0 : -1;
        }

        private int[] BuildLimbBranchIds(int[] boneParentIndex)
        {
            int boneCount = bones.Length;
            int[] branchIds = new int[boneCount];
            int root = FindRootBoneIndex(boneParentIndex);

            for (int i = 0; i < boneCount; i++)
            {
                if (bones[i] == null || i == root)
                {
                    branchIds[i] = -1;
                    continue;
                }

                int current = i;
                int childOfRoot = i;
                int guard = 0;
                while (current >= 0 && current != root && guard++ < boneCount)
                {
                    childOfRoot = current;
                    current = boneParentIndex[current];
                }

                branchIds[i] = childOfRoot;
            }

            return branchIds;
        }

        private static bool IsAllowedLimbInfluence(
            int nearestBone,
            int candidateBone,
            int[] limbBranchIds,
            int[] boneParentIndex)
        {
            if (nearestBone < 0 || candidateBone < 0)
            {
                return false;
            }

            if (nearestBone == candidateBone)
            {
                return true;
            }

            int nearestBranch = limbBranchIds[nearestBone];
            int candidateBranch = limbBranchIds[candidateBone];

            // 胴体ルート側は常に許可
            if (candidateBranch < 0 || nearestBranch < 0)
            {
                return true;
            }

            if (nearestBranch == candidateBranch)
            {
                return true;
            }

            // 最寄りの祖先だけは関節つなぎ用に許可
            return IsAncestorBone(candidateBone, nearestBone, boneParentIndex);
        }

        private static bool IsAncestorBone(int ancestor, int descendant, int[] boneParentIndex)
        {
            int current = descendant;
            int guard = 0;
            while (current >= 0 && guard++ < boneParentIndex.Length)
            {
                if (current == ancestor)
                {
                    return true;
                }

                current = boneParentIndex[current];
            }

            return false;
        }

        private static void StripOppositeLimbWeights(
            float[] weights,
            int[] limbBranchIds,
            int[] boneParentIndex,
            Vector3[] boneLocalPos,
            Vector3 rootLocalPos,
            float lateralThreshold)
        {
            int dominant = FindDominantBone(weights);
            if (dominant < 0)
            {
                return;
            }

            int dominantBranch = limbBranchIds[dominant];
            float dominantSide = boneLocalPos[dominant].x - rootLocalPos.x;
            float threshold = Mathf.Max(0.02f, lateralThreshold);
            bool dominantHasSide = Mathf.Abs(dominantSide) >= threshold;
            bool changed = false;

            for (int b = 0; b < weights.Length; b++)
            {
                if (weights[b] <= 0f || b == dominant)
                {
                    continue;
                }

                if (IsAncestorBone(b, dominant, boneParentIndex) || limbBranchIds[b] < 0)
                {
                    continue;
                }

                bool oppositeBranch = dominantBranch >= 0
                    && limbBranchIds[b] >= 0
                    && limbBranchIds[b] != dominantBranch;
                float boneSide = boneLocalPos[b].x - rootLocalPos.x;
                bool oppositeSide = dominantHasSide
                    && Mathf.Abs(boneSide) >= threshold
                    && dominantSide * boneSide < 0f;

                if (!oppositeBranch && !oppositeSide)
                {
                    continue;
                }

                weights[b] = 0f;
                changed = true;
            }

            if (changed)
            {
                NormalizeWeights(weights);
            }
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

            float parentShare = Mathf.Lerp(jointParentBlend, jointParentBlend * 0.35f, along);
            parentShare = Mathf.Clamp(parentShare, 0.04f, 0.32f);
            float childKeep = 1f - parentShare;
            float childWeight = weights[dominant];
            float injected = childWeight * parentShare;
            weights[dominant] = childWeight * childKeep;
            weights[parent] += injected;
            NormalizeWeights(weights);
        }

        private void SoftPinDominant(float[] weights, float maxWeight, int[] boneParentIndex, int[] limbBranchIds)
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

            int secondary = ResolveSoftPinSecondary(weights, dominant, boneParentIndex);
            int dominantBranch = limbBranchIds[dominant];

            // 反対肢のウェイトを落とし同肢と親子だけ残す
            for (int b = 0; b < weights.Length; b++)
            {
                if (b == dominant || b == secondary)
                {
                    continue;
                }

                if (AreBonesRelated(dominant, b, boneParentIndex))
                {
                    continue;
                }

                if (dominantBranch >= 0
                    && limbBranchIds[b] >= 0
                    && limbBranchIds[b] != dominantBranch
                    && !IsAncestorBone(b, dominant, boneParentIndex))
                {
                    weights[b] = 0f;
                    continue;
                }

                weights[b] *= 0.12f;
            }

            if (secondary >= 0)
            {
                weights[secondary] = Mathf.Max(weights[secondary], softPinSecondaryFloor);
            }

            weights[dominant] = Mathf.Max(weights[dominant], Mathf.Max(0.55f, 1f - softPinSecondaryFloor * 2f));
            NormalizeWeights(weights);
        }

        private int ResolveSoftPinSecondary(float[] weights, int dominant, int[] boneParentIndex)
        {
            int second = FindSecondBone(weights, dominant);
            if (second >= 0 && AreBonesRelated(dominant, second, boneParentIndex))
            {
                return second;
            }

            int parent = boneParentIndex[dominant];
            if (parent >= 0)
            {
                return parent;
            }

            return FindBestChildBone(weights, dominant, boneParentIndex);
        }

        private static int FindBestChildBone(float[] weights, int dominant, int[] boneParentIndex)
        {
            int best = -1;
            float bestWeight = -1f;
            for (int b = 0; b < weights.Length; b++)
            {
                if (boneParentIndex[b] != dominant)
                {
                    continue;
                }

                if (weights[b] > bestWeight)
                {
                    bestWeight = weights[b];
                    best = b;
                }
            }

            return best;
        }

        private static bool AreBonesRelated(int a, int b, int[] boneParentIndex)
        {
            if (a < 0 || b < 0 || a == b)
            {
                return false;
            }

            if (boneParentIndex[a] == b || boneParentIndex[b] == a)
            {
                return true;
            }

            int parentA = boneParentIndex[a];
            return parentA >= 0 && parentA == boneParentIndex[b];
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
            int[] dominantBones,
            int[] boneParentIndex,
            int[] limbBranchIds,
            Vector3[] boneLocalPos,
            Vector3 rootLocalPos,
            float lateralThreshold,
            int boneCount)
        {
            int uniqueCount = uniqueWeights.Length;
            float[][] buffer = new float[uniqueCount][];
            for (int u = 0; u < uniqueCount; u++)
            {
                buffer[u] = new float[boneCount];
                if (dominantBones[u] < 0)
                {
                    dominantBones[u] = FindDominantBone(uniqueWeights[u]);
                }
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
                    int selfDominant = dominantBones[u];
                    int selfBranch = selfDominant >= 0 ? limbBranchIds[selfDominant] : -1;
                    float selfSide = selfDominant >= 0
                        ? boneLocalPos[selfDominant].x - rootLocalPos.x
                        : 0f;
                    float threshold = Mathf.Max(0.02f, lateralThreshold);
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        int nb = neighbors[n];
                        int neighborDominant = dominantBones[nb];
                        int neighborBranch = neighborDominant >= 0 ? limbBranchIds[neighborDominant] : -1;

                        if (selfBranch >= 0
                            && neighborBranch >= 0
                            && selfBranch != neighborBranch)
                        {
                            continue;
                        }

                        if (selfDominant >= 0 && neighborDominant >= 0)
                        {
                            float neighborSide = boneLocalPos[neighborDominant].x - rootLocalPos.x;
                            if (Mathf.Abs(selfSide) >= threshold
                                && Mathf.Abs(neighborSide) >= threshold
                                && selfSide * neighborSide < 0f)
                            {
                                continue;
                            }
                        }

                        float seamBoost = 1f;
                        if (selfDominant != neighborDominant
                            && AreBonesRelated(selfDominant, neighborDominant, boneParentIndex))
                        {
                            seamBoost = 1f + seamBlendStrength;
                        }
                        else if (selfDominant != neighborDominant)
                        {
                            seamBoost = 0.35f;
                        }

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
                    float localStrength = Mathf.Lerp(smoothingStrength, smoothingStrength * 0.2f, confidence[u]);
                    bool hasRelatedSeamNeighbor = false;
                    int selfDominant = dominantBones[u];
                    var neighbors = topo.Adjacency[u];
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        int nb = neighbors[n];
                        if (dominantBones[nb] != selfDominant
                            && AreBonesRelated(selfDominant, dominantBones[nb], boneParentIndex))
                        {
                            hasRelatedSeamNeighbor = true;
                            break;
                        }
                    }

                    if (hasRelatedSeamNeighbor)
                    {
                        localStrength = Mathf.Max(localStrength, seamBlendStrength * 0.7f);
                    }

                    float[] cur = uniqueWeights[u];
                    float[] avg = buffer[u];
                    for (int b = 0; b < boneCount; b++)
                    {
                        cur[b] = Mathf.Lerp(cur[b], avg[b], localStrength);
                    }

                    NormalizeWeights(cur);
                    StripOppositeLimbWeights(
                        cur,
                        limbBranchIds,
                        boneParentIndex,
                        boneLocalPos,
                        rootLocalPos,
                        lateralThreshold);
                    dominantBones[u] = FindDominantBone(cur);
                    confidence[u] = dominantBones[u] >= 0 ? cur[dominantBones[u]] : 0f;
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
