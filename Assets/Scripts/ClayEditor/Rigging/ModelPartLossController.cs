using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// ロードしたモデルに「部位欠損」を行うコントローラ。
    /// SkeletonPartAnalyzer でボーンを部位(腕/脚/前/後/胴)に分類し、
    /// 指定部位のボーンにウェイトが乗った頂点を除去してメッシュを作り直す。
    /// 除去で開いた切断面は平滑化した境界を内側へへこませて塞ぐ。
    /// 欠損は累積し、RestoreAll で元に戻せる。
    /// 戦闘中もメッシュ再構築で傷口を塞ぎリムの発光を抑える。
    ///
    /// モデルロード完了との連携(OnModelLoaded → Setup)は、両方を知っている
    /// 呼び出し側(シーン/プレゼンター)で行うこと(このクラスはUI/ロード機能に依存しない)。
    /// </summary>
    public class ModelPartLossController : MonoBehaviour
    {
        [Header("分類器")]
        [Tooltip("腕/脚などの部位を判定する分類器。ClayEditと同じ設定にしておくこと")]
        [SerializeField] private SkeletonPartAnalyzer partAnalyzer;

        [Header("欠損設定")]
        [Tooltip("頂点を除去する対象ボーンへのウェイト合計のしきい値(これを超える頂点を除去)")]
        [Range(0f, 1f)]
        [SerializeField] private float removeWeightThreshold = 0.5f;

        [Header("傷口を塞ぐ")]
        [Tooltip("除去で開いた切断面を三角形で塞ぐ")]
        [SerializeField] private bool capWounds = true;

        [Tooltip("断面に傷口色を使う(falseなら断面周囲の頂点色を引き継ぐ)")]
        [SerializeField] private bool useWoundColor = false;

        [Tooltip("断面に使う傷口色")]
        [SerializeField] private Color woundColor = new Color(0.5f, 0.1f, 0.1f, 1f);

        [Tooltip("キャップ三角形を追加するサブメッシュ番号")]
        [SerializeField] private int capSubMeshIndex = 0;

        [Tooltip("切断面境界の平滑化回数")]
        [Range(0, 8)]
        [SerializeField] private int woundLoopSmoothPasses = 4;

        [Tooltip("切断面境界の平滑化強度")]
        [Range(0f, 1f)]
        [SerializeField] private float woundLoopSmoothStrength = 0.7f;

        [Tooltip("へこみ深さ(断面半径に対する比率)")]
        [Range(0.05f, 1.2f)]
        [SerializeField] private float woundRecessDepthScale = 0.42f;

        [Tooltip("縁のわずかなへこみ(断面半径に対する比率)")]
        [Range(0f, 0.4f)]
        [SerializeField] private float woundRimInsetScale = 0.1f;

        [Tooltip("へこみの中間リング数多いほど滑らかな凹みになる")]
        [Range(0, 3)]
        [SerializeField] private int woundConcentricRings = 1;

        [Tooltip("戦闘中に断面の発光を抑える頂点色アルファしきい値")]
        [Range(0f, 1f)]
        [SerializeField] private float battleGlowSuppressThreshold = 0.08f;

        [Header("戦闘修復演出")]
        [Tooltip("修復中に部位スケールへ加える揺れの強さ")]
        [Range(0f, 1f)]
        [SerializeField] private float repairWobbleScaleAmount = 0.42f;
        [Tooltip("修復中に部位へ加える回転揺れ(度)")]
        [Range(0f, 45f)]
        [SerializeField] private float repairWobbleRotationDegrees = 16f;
        [Tooltip("修復中の揺れ速度(Hz)")]
        [SerializeField] private float repairWobbleSpeed = 24f;

        /// <summary>
        /// 1つの部位(腕や脚など、リム単位)の情報
        /// </summary>
        public readonly struct LimbInfo
        {
            public LimbInfo(int index, BonePart part, Transform rootBone)
            {
                Index = index;
                Part = part;
                RootBone = rootBone;
            }

            /// <summary>リム番号(0開始)</summary>
            public int Index { get; }

            /// <summary>部位の種類(Arm/Leg/Front/Back)</summary>
            public BonePart Part { get; }

            /// <summary>リムの根本ボーン</summary>
            public Transform RootBone { get; }
        }

        private SkinnedMeshRenderer skinnedRenderer;
        private Mesh originalMesh;                 // ロード時のメッシュ(復元用、破棄しない)
        private Mesh generatedMesh;                // 欠損後に生成したメッシュ(作り直すたびに破棄)
        private Mesh battleColorMesh;              // 戦闘中の頂点色だけを書き換える軽量メッシュ
        private Color[] battleMeshBaseColors;
        private Color[] battleMeshWorkColors;
        private BoneWeight[] cachedBattleBoneWeights;
        private Transform[] bones;
        private readonly Dictionary<Transform, int> boneIndexOf = new Dictionary<Transform, int>();

        private readonly List<LimbInfo> limbs = new List<LimbInfo>();
        private readonly List<List<int>> limbBoneIndices = new List<List<int>>(); // limb番号 -> ボーンindex集合(部分木)
        private readonly HashSet<int> removedBoneIndices = new HashSet<int>();

        private Coroutine deferredRebuildCoroutine;
        private bool rebuildSuspended;
        private readonly Dictionary<Transform, Vector3> battleHiddenBoneScales = new Dictionary<Transform, Vector3>();
        private int gradualRestoreLimbIndex = -1;
        private Transform gradualRestoreRootBone;
        private Quaternion gradualRestoreBaseLocalRotation;
        private float gradualRestoreProgress;
        private float repairWobblePhase;
        private readonly HashSet<int> gradualRestoreBoneIndices = new HashSet<int>();

        private bool[] rebuildRemoveVertex;
        private int[] rebuildRemap;
        private readonly List<Vector3> rebuildVertices = new List<Vector3>(4096);
        private readonly List<Vector3> rebuildNormals = new List<Vector3>(4096);
        private readonly List<Vector4> rebuildTangents = new List<Vector4>(4096);
        private readonly List<Vector2> rebuildUv = new List<Vector2>(4096);
        private readonly List<Vector2> rebuildUv2 = new List<Vector2>(4096);
        private readonly List<Color> rebuildColors = new List<Color>(4096);
        private readonly List<BoneWeight> rebuildWeights = new List<BoneWeight>(4096);
        private readonly Dictionary<long, int> rebuildFullEdgeCount = new Dictionary<long, int>(2048);
        private readonly Dictionary<long, int> rebuildKeptEdgeCount = new Dictionary<long, int>(2048);
        private readonly Dictionary<long, (int from, int to)> rebuildKeptDirected = new Dictionary<long, (int from, int to)>(2048);
        private readonly List<List<int>> rebuildTrianglesScratch = new List<List<int>>(4);
        private BoneWeight[] rebuildBoneWeightsOut;

        /// <summary>現在のモデルから検出した部位(リム)の一覧</summary>
        public IReadOnlyList<LimbInfo> Limbs => limbs;

        /// <summary>セットアップ済みか</summary>
        public bool IsReady => skinnedRenderer != null && originalMesh != null;

        /// <summary>
        /// 分類器を指定してセットアップする(実行時にAddComponentした場合など、参照を渡したいとき用)。
        /// </summary>
        public bool Setup(GameObject model, SkeletonPartAnalyzer analyzer)
        {
            partAnalyzer = analyzer;
            return Setup(model);
        }

        /// <summary>
        /// 対象モデルを解析して欠損可能な状態にする。モデルをロードしたら呼ぶ。
        /// </summary>
        public bool Setup(GameObject model)
        {
            DestroyGeneratedMesh();
            DestroyBattleColorMesh();
            limbs.Clear();
            limbBoneIndices.Clear();
            removedBoneIndices.Clear();
            boneIndexOf.Clear();
            gradualRestoreLimbIndex = -1;
            ClearGradualRestoreState();
            RestoreBattleHiddenBones();

            if (model == null)
            {
                skinnedRenderer = null;
                originalMesh = null;
                return false;
            }

            skinnedRenderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
            {
                Debug.LogWarning("[ModelPartLossController] SkinnedMeshRendererまたはMeshが見つかりません");
                skinnedRenderer = null;
                originalMesh = null;
                return false;
            }

            originalMesh = skinnedRenderer.sharedMesh;
            bones = skinnedRenderer.bones;

            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                {
                    boneIndexOf[bones[i]] = i;
                }
            }

            BuildLimbs();
            return true;
        }

        // ボーン階層を部位分類し、リム(腕/脚など)単位にまとめる
        private void BuildLimbs()
        {
            List<SkeletonLimbPartCollector.LimbRoot> limbRoots = partAnalyzer != null
                ? SkeletonLimbPartCollector.CollectLimbRoots(partAnalyzer, bones)
                : new List<SkeletonLimbPartCollector.LimbRoot>();

            int limbIndex = 0;
            for (int i = 0; i < limbRoots.Count; i++)
            {
                SkeletonLimbPartCollector.LimbRoot limbRoot = limbRoots[i];
                List<int> subtree = CollectSubtreeIndices(limbRoot.RootBone);
                if (subtree.Count == 0)
                {
                    continue;
                }

                limbs.Add(new LimbInfo(limbIndex, limbRoot.Part, limbRoot.RootBone));
                limbBoneIndices.Add(subtree);
                limbIndex++;
            }
        }

        private List<int> CollectSubtreeIndices(Transform rootBone)
        {
            var result = new List<int>();
            var stack = new Stack<Transform>();
            stack.Push(rootBone);

            while (stack.Count > 0)
            {
                Transform cur = stack.Pop();
                if (boneIndexOf.TryGetValue(cur, out int idx))
                {
                    result.Add(idx);
                }

                for (int c = 0; c < cur.childCount; c++)
                {
                    stack.Push(cur.GetChild(c));
                }
            }

            return result;
        }

        // ===== 公開: 欠損操作 =====

        public bool RemoveLimb(int limbIndex)
        {
            if (!IsReady || limbIndex < 0 || limbIndex >= limbBoneIndices.Count)
            {
                return false;
            }

            bool changed = false;
            foreach (int boneIndex in limbBoneIndices[limbIndex])
            {
                changed |= removedBoneIndices.Add(boneIndex);
            }

            if (changed)
            {
                ApplyPartLossVisual(limbIndex);
            }

            return changed;
        }

        public bool RemoveArm(int ordinal) => RemovePartOrdinal(BonePart.Arm, ordinal);

        public bool RemoveLeg(int ordinal) => RemovePartOrdinal(BonePart.Leg, ordinal);

        private bool RemovePartOrdinal(BonePart part, int ordinal)
        {
            int count = 0;
            for (int i = 0; i < limbs.Count; i++)
            {
                if (limbs[i].Part != part)
                {
                    continue;
                }

                if (count == ordinal)
                {
                    return RemoveLimb(limbs[i].Index);
                }

                count++;
            }

            return false;
        }

        public bool RemovePartByBone(Transform bone)
        {
            if (!IsReady || bone == null)
            {
                return false;
            }

            if (boneIndexOf.TryGetValue(bone, out int boneIndex))
            {
                for (int i = 0; i < limbBoneIndices.Count; i++)
                {
                    if (limbBoneIndices[i].Contains(boneIndex))
                    {
                        return RemoveLimb(i);
                    }
                }
            }

            return RemoveBoneSubtree(bone);
        }

        public bool RemoveBoneSubtree(Transform bone)
        {
            if (!IsReady || bone == null)
            {
                return false;
            }

            bool changed = false;
            foreach (int boneIndex in CollectSubtreeIndices(bone))
            {
                changed |= removedBoneIndices.Add(boneIndex);
            }

            if (changed)
            {
                ApplyPartLossVisualForBoneSubtree(bone);
            }

            return changed;
        }

        public bool RemoveRandomPart()
        {
            var candidates = new List<int>();
            for (int i = 0; i < limbBoneIndices.Count; i++)
            {
                bool alreadyRemoved = true;
                foreach (int boneIndex in limbBoneIndices[i])
                {
                    if (!removedBoneIndices.Contains(boneIndex))
                    {
                        alreadyRemoved = false;
                        break;
                    }
                }

                if (!alreadyRemoved)
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            return RemoveLimb(candidates[Random.Range(0, candidates.Count)]);
        }

        /// <summary>
        /// 欠損したリムを1つ復旧する
        /// </summary>
        /// <param name="limbIndex">復旧するリム番号</param>
        /// <returns>復旧できたらtrue</returns>
        public bool RestoreLimb(int limbIndex)
        {
            if (!IsReady || limbIndex < 0 || limbIndex >= limbBoneIndices.Count)
            {
                return false;
            }

            bool changed = false;
            foreach (int boneIndex in limbBoneIndices[limbIndex])
            {
                changed |= removedBoneIndices.Remove(boneIndex);
            }

            if (!changed)
            {
                return false;
            }

            if (gradualRestoreLimbIndex == limbIndex || DoesGradualRestoreTouchLimb(limbIndex))
            {
                ClearGradualRestoreState();
            }

            ApplyPartRestoreVisual(limbIndex);

            return true;
        }

        /// <summary>
        /// 戦闘中の部位修復表示を開始する
        /// </summary>
        /// <param name="limbIndex">復旧するリム番号</param>
        /// <returns>開始できたらtrue</returns>
        public bool BeginGradualRestoreLimb(int limbIndex)
        {
            if (!IsReady || !rebuildSuspended || limbIndex < 0 || limbIndex >= limbBoneIndices.Count)
            {
                return false;
            }

            if (!IsLimbRemoved(limbIndex))
            {
                return false;
            }

            bool resumeFrozenProgress = DoesGradualRestoreTouchLimb(limbIndex) && gradualRestoreProgress > 0f;
            gradualRestoreLimbIndex = limbIndex;
            if (!resumeFrozenProgress)
            {
                gradualRestoreProgress = 0f;
            }

            gradualRestoreRootBone = limbs[limbIndex].RootBone;
            gradualRestoreBaseLocalRotation = gradualRestoreRootBone != null
                ? gradualRestoreRootBone.localRotation
                : Quaternion.identity;
            repairWobblePhase = Random.Range(0f, 100f);
            CacheGradualRestoreBoneIndices(limbIndex);
            ApplyPartialBattleLimbShow(limbIndex, gradualRestoreProgress);
            SyncBattleMeshVertexGlow();
            return true;
        }

        /// <summary>
        /// 戦闘中の部位修復表示の進捗を更新する
        /// </summary>
        /// <param name="limbIndex">復旧するリム番号</param>
        /// <param name="progress">0から1の進捗</param>
        public void SetGradualRestoreProgress(int limbIndex, float progress)
        {
            if (gradualRestoreLimbIndex != limbIndex)
            {
                return;
            }

            gradualRestoreProgress = progress;
            ApplyPartialBattleLimbShow(limbIndex, progress);
            SyncBattleMeshVertexGlow();
        }

        /// <summary>
        /// 戦闘中の部位修復表示をキャンセルする
        /// 途中停止時は進捗時点のメッシュを残す
        /// </summary>
        public void CancelGradualRestoreLimb()
        {
            if (gradualRestoreLimbIndex < 0)
            {
                return;
            }

            int limbIndex = gradualRestoreLimbIndex;
            ApplyPartialBattleLimbShow(limbIndex, gradualRestoreProgress, enableWobble: false);
            ResetGradualRestoreRootTransform();
            // Updateによる揺れ更新だけ止め進捗とボーン情報は残して表示を維持する
            gradualRestoreLimbIndex = -1;
            gradualRestoreRootBone = null;
            SyncBattleMeshVertexGlow();
        }

        private void Update()
        {
            if (gradualRestoreLimbIndex < 0)
            {
                return;
            }

            ApplyPartialBattleLimbShow(gradualRestoreLimbIndex, gradualRestoreProgress);
        }

        private void ClearGradualRestoreState()
        {
            ResetGradualRestoreRootTransform();
            gradualRestoreLimbIndex = -1;
            gradualRestoreRootBone = null;
            gradualRestoreProgress = 0f;
            gradualRestoreBoneIndices.Clear();
        }

        private void CacheGradualRestoreBoneIndices(int limbIndex)
        {
            gradualRestoreBoneIndices.Clear();
            if (limbIndex < 0 || limbIndex >= limbBoneIndices.Count)
            {
                return;
            }

            List<int> boneIndices = limbBoneIndices[limbIndex];
            for (int i = 0; i < boneIndices.Count; i++)
            {
                gradualRestoreBoneIndices.Add(boneIndices[i]);
            }
        }

        private void ResetGradualRestoreRootTransform()
        {
            if (gradualRestoreRootBone == null)
            {
                return;
            }

            gradualRestoreRootBone.localRotation = gradualRestoreBaseLocalRotation;
        }

        private void ScheduleRebuild()
        {
            if (rebuildSuspended || !isActiveAndEnabled)
            {
                return;
            }

            if (deferredRebuildCoroutine != null)
            {
                return;
            }

            deferredRebuildCoroutine = StartCoroutine(RebuildNextFrame());
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            deferredRebuildCoroutine = null;
            Rebuild();
        }

        /// <summary>
        /// メッシュ再構築の遅延予約を止める
        /// 戦闘中はボーン非表示で部位欠損を表現する
        /// </summary>
        public void SuspendMeshRebuild()
        {
            rebuildSuspended = true;
            CancelDeferredRebuild();
            SyncBattleHiddenLimbs();
            SyncBattleMeshVertexGlow();
        }

        private void SyncBattleHiddenLimbs()
        {
            for (int i = 0; i < limbBoneIndices.Count; i++)
            {
                if (IsLimbRemoved(i))
                {
                    ApplyBattleLimbHide(i);
                }
            }
        }

        /// <summary>
        /// 次フレームに予約されたメッシュ再構築をキャンセルする
        /// </summary>
        public void CancelDeferredRebuild()
        {
            if (deferredRebuildCoroutine == null)
            {
                return;
            }

            StopCoroutine(deferredRebuildCoroutine);
            deferredRebuildCoroutine = null;
        }

        private void ApplyPartLossVisual(int limbIndex)
        {
            if (rebuildSuspended)
            {
                ApplyBattleLimbHide(limbIndex);
                SyncBattleMeshVertexGlow();
                return;
            }

            ScheduleRebuild();
        }

        private void ApplyPartLossVisualForBoneSubtree(Transform bone)
        {
            if (!rebuildSuspended)
            {
                ScheduleRebuild();
                return;
            }

            foreach (int boneIndex in CollectSubtreeIndices(bone))
            {
                if (boneIndex >= 0 && boneIndex < bones.Length && bones[boneIndex] != null)
                {
                    ApplyBattleBoneHide(bones[boneIndex]);
                }
            }

            SyncBattleMeshVertexGlow();
        }

        private void ApplyPartRestoreVisual(int limbIndex)
        {
            if (rebuildSuspended)
            {
                RestoreBattleLimbShow(limbIndex);
                SyncBattleMeshVertexGlow();
                return;
            }

            ScheduleRebuild();
        }

        public void RestoreAll()
        {
            if (!IsReady)
            {
                return;
            }

            RestoreBattleHiddenBones();
            removedBoneIndices.Clear();
            ClearGradualRestoreState();
            DestroyGeneratedMesh();
            DestroyBattleColorMesh();
            skinnedRenderer.sharedMesh = originalMesh;
            rebuildSuspended = false;
        }

        private void EnsureBattleColorMesh()
        {
            if (battleColorMesh != null)
            {
                return;
            }

            battleColorMesh = Instantiate(originalMesh);
            battleColorMesh.name = originalMesh.name + "_BattlePartLoss";

            int vertexCount = originalMesh.vertexCount;
            Color[] sourceColors = originalMesh.colors;
            battleMeshBaseColors = new Color[vertexCount];
            battleMeshWorkColors = new Color[vertexCount];
            cachedBattleBoneWeights = originalMesh.boneWeights;

            if (sourceColors != null && sourceColors.Length == vertexCount)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    battleMeshBaseColors[i] = sourceColors[i];
                }
            }
            else
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    battleMeshBaseColors[i] = Color.white;
                }
            }

            battleColorMesh.colors = battleMeshBaseColors;
            skinnedRenderer.sharedMesh = battleColorMesh;
        }

        private void SyncBattleMeshVertexGlow()
        {
            if (!rebuildSuspended || !IsReady)
            {
                return;
            }

            if (removedBoneIndices.Count == 0)
            {
                DestroyBattleColorMesh();
                skinnedRenderer.sharedMesh = originalMesh;
                return;
            }

            EnsureBattleColorMesh();

            BoneWeight[] weights = cachedBattleBoneWeights;
            if (weights == null || weights.Length != battleMeshBaseColors.Length)
            {
                weights = originalMesh.boneWeights;
                cachedBattleBoneWeights = weights;
            }

            if (weights == null || weights.Length != battleMeshBaseColors.Length)
            {
                return;
            }

            if (battleMeshWorkColors == null || battleMeshWorkColors.Length != battleMeshBaseColors.Length)
            {
                battleMeshWorkColors = new Color[battleMeshBaseColors.Length];
            }

            Color[] colors = battleMeshWorkColors;
            float glowThreshold = Mathf.Min(removeWeightThreshold, battleGlowSuppressThreshold);
            float restoreVisibility = ResolveGradualRestoreVisibility();

            for (int v = 0; v < colors.Length; v++)
            {
                colors[v] = battleMeshBaseColors[v];
                float removedWeight = ComputeRemovedWeight(weights[v]);
                if (removedWeight <= glowThreshold)
                {
                    continue;
                }

                float restoringWeight = ComputeGradualRestoreWeight(weights[v]);
                if (restoringWeight > glowThreshold && restoreVisibility > 0f)
                {
                    colors[v].a = battleMeshBaseColors[v].a * restoreVisibility;
                    continue;
                }

                colors[v].a = 0f;
            }

            battleColorMesh.colors = colors;
        }

        private bool DoesGradualRestoreTouchLimb(int limbIndex)
        {
            if (gradualRestoreBoneIndices.Count == 0 || limbIndex < 0 || limbIndex >= limbBoneIndices.Count)
            {
                return false;
            }

            List<int> boneIndices = limbBoneIndices[limbIndex];
            for (int i = 0; i < boneIndices.Count; i++)
            {
                if (gradualRestoreBoneIndices.Contains(boneIndices[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private float ResolveGradualRestoreVisibility()
        {
            // 修復中および途中キャンセル後の凍結表示の両方で進捗を参照する
            if (gradualRestoreBoneIndices.Count == 0)
            {
                return 0f;
            }

            float t = Mathf.Clamp01(gradualRestoreProgress);
            return 1f - Mathf.Pow(1f - t, 2.4f);
        }

        private float ComputeGradualRestoreWeight(BoneWeight weight)
        {
            if (gradualRestoreBoneIndices.Count == 0)
            {
                return 0f;
            }

            float restoringWeight = 0f;
            if (gradualRestoreBoneIndices.Contains(weight.boneIndex0))
            {
                restoringWeight += weight.weight0;
            }

            if (gradualRestoreBoneIndices.Contains(weight.boneIndex1))
            {
                restoringWeight += weight.weight1;
            }

            if (gradualRestoreBoneIndices.Contains(weight.boneIndex2))
            {
                restoringWeight += weight.weight2;
            }

            if (gradualRestoreBoneIndices.Contains(weight.boneIndex3))
            {
                restoringWeight += weight.weight3;
            }

            return restoringWeight;
        }

        private float ComputeRemovedWeight(BoneWeight weight)
        {
            float removedWeight = 0f;
            if (removedBoneIndices.Contains(weight.boneIndex0))
            {
                removedWeight += weight.weight0;
            }

            if (removedBoneIndices.Contains(weight.boneIndex1))
            {
                removedWeight += weight.weight1;
            }

            if (removedBoneIndices.Contains(weight.boneIndex2))
            {
                removedWeight += weight.weight2;
            }

            if (removedBoneIndices.Contains(weight.boneIndex3))
            {
                removedWeight += weight.weight3;
            }

            return removedWeight;
        }

        private void ApplyBattleLimbHide(int limbIndex)
        {
            if (limbIndex < 0 || limbIndex >= limbBoneIndices.Count)
            {
                return;
            }

            foreach (int boneIndex in limbBoneIndices[limbIndex])
            {
                if (boneIndex >= 0 && boneIndex < bones.Length && bones[boneIndex] != null)
                {
                    ApplyBattleBoneHide(bones[boneIndex]);
                }
            }
        }

        private void RestoreBattleLimbShow(int limbIndex)
        {
            if (limbIndex < 0 || limbIndex >= limbBoneIndices.Count)
            {
                return;
            }

            foreach (int boneIndex in limbBoneIndices[limbIndex])
            {
                if (boneIndex >= 0 && boneIndex < bones.Length && bones[boneIndex] != null)
                {
                    RestoreBattleBoneShow(bones[boneIndex]);
                }
            }
        }

        private void ApplyPartialBattleLimbShow(int limbIndex, float progress, bool enableWobble = true)
        {
            if (limbIndex < 0 || limbIndex >= limbBoneIndices.Count)
            {
                return;
            }

            float t = Mathf.Clamp01(progress);
            float grow = 1f - Mathf.Pow(1f - t, 2.4f);
            float wobbleStrength = enableWobble ? (1f - t) * (1f - t) : 0f;
            float time = Time.time + repairWobblePhase;
            float speed = repairWobbleSpeed;

            List<int> boneIndices = limbBoneIndices[limbIndex];
            for (int i = 0; i < boneIndices.Count; i++)
            {
                int boneIndex = boneIndices[i];
                if (boneIndex < 0 || boneIndex >= bones.Length)
                {
                    continue;
                }

                Transform bone = bones[boneIndex];
                if (bone == null || !battleHiddenBoneScales.TryGetValue(bone, out Vector3 targetScale))
                {
                    continue;
                }

                bool isRoot = gradualRestoreRootBone != null && bone == gradualRestoreRootBone;
                if (isRoot && wobbleStrength > 0.001f)
                {
                    float scaleX = 1f + Mathf.Sin(time * speed) * repairWobbleScaleAmount * wobbleStrength;
                    float scaleY = 1f + Mathf.Sin(time * speed * 1.29f + 0.8f) * repairWobbleScaleAmount * wobbleStrength;
                    float scaleZ = 1f + Mathf.Sin(time * speed * 0.91f + 1.6f) * repairWobbleScaleAmount * wobbleStrength;
                    bone.localScale = Vector3.Scale(
                        targetScale,
                        new Vector3(grow * scaleX, grow * scaleY, grow * scaleZ));
                }
                else
                {
                    bone.localScale = targetScale * grow;
                }
            }

            if (gradualRestoreRootBone == null)
            {
                return;
            }

            if (wobbleStrength <= 0.001f || repairWobbleRotationDegrees <= 0f)
            {
                gradualRestoreRootBone.localRotation = gradualRestoreBaseLocalRotation;
                return;
            }

            float rotX = Mathf.Sin(time * speed * 1.07f) * repairWobbleRotationDegrees * wobbleStrength;
            float rotY = Mathf.Sin(time * speed * 1.43f + 1.1f) * repairWobbleRotationDegrees * 0.75f * wobbleStrength;
            float rotZ = Mathf.Sin(time * speed * 0.88f + 2.3f) * repairWobbleRotationDegrees * 0.55f * wobbleStrength;
            gradualRestoreRootBone.localRotation =
                gradualRestoreBaseLocalRotation * Quaternion.Euler(rotX, rotY, rotZ);
        }

        private bool IsLimbRemoved(int limbIndex)
        {
            foreach (int boneIndex in limbBoneIndices[limbIndex])
            {
                if (removedBoneIndices.Contains(boneIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyBattleBoneHide(Transform bone)
        {
            if (bone == null || battleHiddenBoneScales.ContainsKey(bone))
            {
                return;
            }

            battleHiddenBoneScales[bone] = bone.localScale;
            bone.localScale = Vector3.zero;
        }

        private void RestoreBattleBoneShow(Transform bone)
        {
            if (bone == null || !battleHiddenBoneScales.TryGetValue(bone, out Vector3 scale))
            {
                return;
            }

            bone.localScale = scale;
            battleHiddenBoneScales.Remove(bone);
        }

        private void RestoreBattleHiddenBones()
        {
            foreach (KeyValuePair<Transform, Vector3> entry in battleHiddenBoneScales)
            {
                if (entry.Key != null)
                {
                    entry.Key.localScale = entry.Value;
                }
            }

            battleHiddenBoneScales.Clear();
        }

        // ===== メッシュ再構築 =====

        // 作業用の頂点属性バッファ(キャップ頂点もここへ追加する)
        private struct MeshBuffers
        {
            public List<Vector3> vertices;
            public List<Vector3> normals;
            public List<Vector4> tangents;
            public List<Vector2> uv;
            public List<Vector2> uv2;
            public List<Color> colors;
            public List<BoneWeight> weights;
        }

        private void Rebuild()
        {
            if (rebuildSuspended)
            {
                return;
            }

            Mesh src = originalMesh;

            Vector3[] srcVertices = src.vertices;
            Vector3[] srcNormals = src.normals;
            Vector4[] srcTangents = src.tangents;
            Vector2[] srcUv = src.uv;
            Vector2[] srcUv2 = src.uv2;
            Color[] srcColors = src.colors;
            BoneWeight[] srcWeights = src.boneWeights;

            int vertexCount = srcVertices.Length;
            bool hasNormals = srcNormals.Length == vertexCount;
            bool hasTangents = srcTangents.Length == vertexCount;
            bool hasUv = srcUv.Length == vertexCount;
            bool hasUv2 = srcUv2.Length == vertexCount;
            bool hasColors = srcColors.Length == vertexCount;
            bool hasWeights = srcWeights.Length == vertexCount;

            EnsureRebuildArrays(vertexCount);

            // 除去頂点の判定と新indexへの詰め直し
            bool[] removeVertex = rebuildRemoveVertex;
            int[] remap = rebuildRemap;
            int newCount = 0;

            for (int v = 0; v < vertexCount; v++)
            {
                bool remove = false;
                if (hasWeights)
                {
                    BoneWeight w = srcWeights[v];
                    float removedWeight = 0f;
                    if (removedBoneIndices.Contains(w.boneIndex0)) removedWeight += w.weight0;
                    if (removedBoneIndices.Contains(w.boneIndex1)) removedWeight += w.weight1;
                    if (removedBoneIndices.Contains(w.boneIndex2)) removedWeight += w.weight2;
                    if (removedBoneIndices.Contains(w.boneIndex3)) removedWeight += w.weight3;
                    remove = removedWeight > removeWeightThreshold;
                }

                removeVertex[v] = remove;
                remap[v] = remove ? -1 : newCount++;
            }

            ClearRebuildBuffers();
            var buf = new MeshBuffers
            {
                vertices = rebuildVertices,
                normals = hasNormals ? rebuildNormals : null,
                tangents = hasTangents ? rebuildTangents : null,
                uv = hasUv ? rebuildUv : null,
                uv2 = hasUv2 ? rebuildUv2 : null,
                colors = hasColors ? rebuildColors : null,
                weights = hasWeights ? rebuildWeights : null
            };

            for (int v = 0; v < vertexCount; v++)
            {
                if (removeVertex[v])
                {
                    continue;
                }

                buf.vertices.Add(srcVertices[v]);
                if (hasNormals) buf.normals.Add(srcNormals[v]);
                if (hasTangents) buf.tangents.Add(srcTangents[v]);
                if (hasUv) buf.uv.Add(srcUv[v]);
                if (hasUv2) buf.uv2.Add(srcUv2[v]);
                if (hasColors) buf.colors.Add(srcColors[v]);
                if (hasWeights) buf.weights.Add(srcWeights[v]);
            }

            int subMeshCount = src.subMeshCount;

            // 三角形の残し判定と、エッジ集計(キャップ用)
            rebuildFullEdgeCount.Clear();
            rebuildKeptEdgeCount.Clear();
            rebuildKeptDirected.Clear();
            EnsureTriangleLists(subMeshCount);
            List<int>[] newTrianglesPerSub = new List<int>[subMeshCount];

            for (int sub = 0; sub < subMeshCount; sub++)
            {
                int[] tris = src.GetTriangles(sub);
                List<int> list = rebuildTrianglesScratch[sub];
                list.Clear();
                if (list.Capacity < tris.Length)
                {
                    list.Capacity = tris.Length;
                }

                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t];
                    int b = tris[t + 1];
                    int c = tris[t + 2];

                    // 全三角形のエッジを数える(切断面検出の基準)
                    AddEdge(rebuildFullEdgeCount, a, b);
                    AddEdge(rebuildFullEdgeCount, b, c);
                    AddEdge(rebuildFullEdgeCount, c, a);

                    if (removeVertex[a] || removeVertex[b] || removeVertex[c])
                    {
                        continue;
                    }

                    list.Add(remap[a]);
                    list.Add(remap[b]);
                    list.Add(remap[c]);

                    // 残した三角形のエッジ(向き付き)を記録
                    AddKeptEdge(rebuildKeptEdgeCount, rebuildKeptDirected, a, b);
                    AddKeptEdge(rebuildKeptEdgeCount, rebuildKeptDirected, b, c);
                    AddKeptEdge(rebuildKeptEdgeCount, rebuildKeptDirected, c, a);
                }

                newTrianglesPerSub[sub] = list;
            }

            if (capWounds && !hasColors && subMeshCount > 0)
            {
                hasColors = true;
                buf.colors = rebuildColors;
                buf.colors.Clear();
                for (int i = 0; i < buf.vertices.Count; i++)
                {
                    buf.colors.Add(Color.white);
                }
            }

            // 傷口を塞ぐ
            if (capWounds && subMeshCount > 0)
            {
                BuildCaps(srcVertices, srcNormals, srcTangents, srcUv, srcUv2, srcColors, srcWeights,
                    hasNormals, hasTangents, hasUv, hasUv2, hasColors, hasWeights,
                    rebuildFullEdgeCount, rebuildKeptEdgeCount, rebuildKeptDirected, buf, newTrianglesPerSub);
            }

            // メッシュ生成(生成済みMeshを可能な限り再利用する)
            Mesh newMesh = generatedMesh != null ? generatedMesh : new Mesh();
            newMesh.name = src.name + "_PartLoss";
            newMesh.Clear();
            newMesh.indexFormat = buf.vertices.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            newMesh.SetVertices(buf.vertices);
            if (hasNormals) newMesh.SetNormals(buf.normals);
            if (hasTangents) newMesh.SetTangents(buf.tangents);
            if (hasUv) newMesh.SetUVs(0, buf.uv);
            if (hasUv2) newMesh.SetUVs(1, buf.uv2);
            if (hasColors) newMesh.SetColors(buf.colors);
            if (hasWeights)
            {
                int weightCount = buf.weights.Count;
                if (rebuildBoneWeightsOut == null || rebuildBoneWeightsOut.Length != weightCount)
                {
                    rebuildBoneWeightsOut = new BoneWeight[weightCount];
                }

                buf.weights.CopyTo(rebuildBoneWeightsOut);
                newMesh.boneWeights = rebuildBoneWeightsOut;
            }

            newMesh.bindposes = src.bindposes;

            newMesh.subMeshCount = subMeshCount;
            for (int sub = 0; sub < subMeshCount; sub++)
            {
                newMesh.SetTriangles(newTrianglesPerSub[sub], sub);
            }

            newMesh.RecalculateBounds();

            generatedMesh = newMesh;
            skinnedRenderer.sharedMesh = newMesh;
        }

        private void EnsureRebuildArrays(int vertexCount)
        {
            if (rebuildRemoveVertex == null || rebuildRemoveVertex.Length < vertexCount)
            {
                rebuildRemoveVertex = new bool[vertexCount];
            }

            if (rebuildRemap == null || rebuildRemap.Length < vertexCount)
            {
                rebuildRemap = new int[vertexCount];
            }
        }

        private void ClearRebuildBuffers()
        {
            rebuildVertices.Clear();
            rebuildNormals.Clear();
            rebuildTangents.Clear();
            rebuildUv.Clear();
            rebuildUv2.Clear();
            rebuildColors.Clear();
            rebuildWeights.Clear();
        }

        private void EnsureTriangleLists(int subMeshCount)
        {
            while (rebuildTrianglesScratch.Count < subMeshCount)
            {
                rebuildTrianglesScratch.Add(new List<int>(1024));
            }
        }

        // 除去で新たに開いた境界エッジをループ化しへこんだ断面で塞ぐ
        private void BuildCaps(
            Vector3[] srcVertices, Vector3[] srcNormals, Vector4[] srcTangents,
            Vector2[] srcUv, Vector2[] srcUv2, Color[] srcColors, BoneWeight[] srcWeights,
            bool hasNormals, bool hasTangents, bool hasUv, bool hasUv2, bool hasColors, bool hasWeights,
            Dictionary<long, int> fullEdgeCount,
            Dictionary<long, int> keptEdgeCount,
            Dictionary<long, (int from, int to)> keptDirected,
            MeshBuffers buf,
            List<int>[] newTrianglesPerSub)
        {
            // 切断面エッジ: 元は内部(2三角形共有)だったが、残った三角形が1つだけ使うエッジ
            var cutFrom = new Dictionary<int, int>();
            foreach (var kv in keptEdgeCount)
            {
                if (kv.Value != 1)
                {
                    continue;
                }

                if (fullEdgeCount.TryGetValue(kv.Key, out int fullCount) && fullCount == 2)
                {
                    (int from, int to) = keptDirected[kv.Key];
                    cutFrom[from] = to;
                }
            }

            if (cutFrom.Count == 0)
            {
                return;
            }

            // 本体の中心(キャップ法線を外向きにするための基準)
            Vector3 bodyCenter = Vector3.zero;
            for (int i = 0; i < buf.vertices.Count; i++)
            {
                bodyCenter += buf.vertices[i];
            }
            if (buf.vertices.Count > 0)
            {
                bodyCenter /= buf.vertices.Count;
            }

            int capSub = Mathf.Clamp(capSubMeshIndex, 0, newTrianglesPerSub.Length - 1);
            List<int> capTriangles = newTrianglesPerSub[capSub];

            // 向き付きエッジをたどってループを作る
            var visited = new HashSet<int>();
            foreach (int startVertex in cutFrom.Keys)
            {
                if (visited.Contains(startVertex))
                {
                    continue;
                }

                var loop = new List<int>();
                int cur = startVertex;
                int guard = 0;
                while (!visited.Contains(cur))
                {
                    visited.Add(cur);
                    loop.Add(cur);
                    if (!cutFrom.TryGetValue(cur, out cur))
                    {
                        break;
                    }
                    if (++guard > cutFrom.Count + 1)
                    {
                        break;
                    }
                }

                if (loop.Count < 3)
                {
                    continue;
                }

                CapLoop(loop, srcVertices, srcNormals, srcTangents, srcUv, srcUv2, srcColors, srcWeights,
                    hasNormals, hasTangents, hasUv, hasUv2, hasColors, hasWeights,
                    bodyCenter, buf, capTriangles);
            }
        }

        // 1つの境界ループを平滑化し内側へへこませて塞ぐ
        private void CapLoop(
            List<int> loop,
            Vector3[] srcVertices, Vector3[] srcNormals, Vector4[] srcTangents,
            Vector2[] srcUv, Vector2[] srcUv2, Color[] srcColors, BoneWeight[] srcWeights,
            bool hasNormals, bool hasTangents, bool hasUv, bool hasUv2, bool hasColors, bool hasWeights,
            Vector3 bodyCenter, MeshBuffers buf, List<int> capTriangles)
        {
            int count = loop.Count;
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = srcVertices[loop[i]];
            }

            SmoothClosedLoop(positions, woundLoopSmoothPasses, woundLoopSmoothStrength);

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                centroid += positions[i];
            }
            centroid /= count;

            Vector3 normal = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Vector3 c0 = positions[i];
                Vector3 c1 = positions[(i + 1) % count];
                normal.x += (c0.y - c1.y) * (c0.z + c1.z);
                normal.y += (c0.z - c1.z) * (c0.x + c1.x);
                normal.z += (c0.x - c1.x) * (c0.y + c1.y);
            }
            normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : (centroid - bodyCenter).normalized;
            if (Vector3.Dot(normal, centroid - bodyCenter) < 0f)
            {
                normal = -normal;
            }

            for (int i = 0; i < count; i++)
            {
                positions[i] = ProjectOntoPlane(positions[i], centroid, normal);
            }

            float radius = 0f;
            for (int i = 0; i < count; i++)
            {
                radius += Vector3.Distance(positions[i], centroid);
            }
            radius = Mathf.Max(radius / count, 1e-4f);

            float recessDepth = radius * woundRecessDepthScale;
            float rimInset = radius * woundRimInsetScale;
            Vector3 inward = -normal;
            int ringCount = Mathf.Max(0, woundConcentricRings);
            int ringLayerCount = ringCount + 1;

            Color capColor = woundColor;
            bool hasSourceColors = srcColors.Length == srcVertices.Length;
            BoneWeight averagedWeight = AverageLoopWeights(loop, srcWeights, hasWeights);

            int firstRingStart = buf.vertices.Count;
            for (int ring = 0; ring < ringLayerCount; ring++)
            {
                float ringT = ringCount == 0 ? 0f : ring / (float)(ringCount + 1);
                float inset = Mathf.Lerp(rimInset, recessDepth, ringT);

                for (int i = 0; i < count; i++)
                {
                    int sourceIndex = loop[i];
                    Vector3 ringPos = Vector3.Lerp(positions[i], centroid, ringT) + inward * inset;
                    Vector3 radial = Vector3.ProjectOnPlane(positions[i] - centroid, normal);
                    Vector3 ringNormal = normal;
                    if (radial.sqrMagnitude > 1e-8f)
                    {
                        // 凹み内壁が見えるよう軸側へ少し傾ける
                        float tilt = 0.12f + ringT * 0.55f;
                        ringNormal = Vector3.Normalize(normal - radial.normalized * tilt);
                    }

                    Color capVertexColor = useWoundColor
                        ? capColor
                        : (hasSourceColors ? srcColors[sourceIndex] : Color.white);
                    capVertexColor.a = 0f;
                    AppendVertex(
                        buf,
                        ringPos,
                        ringNormal,
                        hasTangents ? srcTangents[sourceIndex] : default,
                        hasUv ? srcUv[sourceIndex] : default,
                        hasUv2 ? srcUv2[sourceIndex] : default,
                        capVertexColor,
                        hasWeights ? srcWeights[sourceIndex] : default,
                        hasNormals,
                        hasTangents,
                        hasUv,
                        hasUv2,
                        true,
                        hasWeights);
                }
            }

            int centerIndex = buf.vertices.Count;
            int s0 = loop[0];
            Color centerColor = useWoundColor ? capColor : (hasSourceColors ? srcColors[s0] : Color.white);
            centerColor.a = 0f;
            AppendVertex(
                buf,
                centroid + inward * recessDepth,
                normal,
                hasTangents ? srcTangents[s0] : default,
                hasUv ? srcUv[s0] : default,
                hasUv2 ? srcUv2[s0] : default,
                centerColor,
                averagedWeight,
                hasNormals,
                hasTangents,
                hasUv,
                hasUv2,
                true,
                hasWeights);

            int outerStart = firstRingStart;
            Vector3 a0 = buf.vertices[outerStart];
            Vector3 b0 = buf.vertices[outerStart + 1];
            Vector3 faceNormal = Vector3.Cross(a0 - (centroid + inward * recessDepth), b0 - (centroid + inward * recessDepth));
            bool flip = Vector3.Dot(faceNormal, normal) < 0f;

            for (int ring = 0; ring < ringCount; ring++)
            {
                int ringA = firstRingStart + ring * count;
                int ringB = firstRingStart + (ring + 1) * count;
                for (int i = 0; i < count; i++)
                {
                    int a0i = ringA + i;
                    int a1i = ringA + (i + 1) % count;
                    int b0i = ringB + i;
                    int b1i = ringB + (i + 1) % count;
                    AddCapQuad(capTriangles, a0i, a1i, b0i, b1i, flip);
                }
            }

            int innerRingStart = firstRingStart + ringCount * count;
            for (int i = 0; i < count; i++)
            {
                int a = innerRingStart + i;
                int b = innerRingStart + (i + 1) % count;
                if (flip)
                {
                    capTriangles.Add(centerIndex);
                    capTriangles.Add(b);
                    capTriangles.Add(a);
                }
                else
                {
                    capTriangles.Add(centerIndex);
                    capTriangles.Add(a);
                    capTriangles.Add(b);
                }
            }
        }

        private static void SmoothClosedLoop(Vector3[] positions, int passes, float strength)
        {
            if (positions == null || positions.Length < 3 || passes <= 0 || strength <= 0f)
            {
                return;
            }

            float t = Mathf.Clamp01(strength);
            var next = new Vector3[positions.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    Vector3 prev = positions[(i - 1 + positions.Length) % positions.Length];
                    Vector3 cur = positions[i];
                    Vector3 nxt = positions[(i + 1) % positions.Length];
                    next[i] = Vector3.Lerp(cur, (prev + nxt) * 0.5f, t);
                }

                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = next[i];
                }
            }
        }

        private static Vector3 ProjectOntoPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            return point - planeNormal * Vector3.Dot(point - planePoint, planeNormal);
        }

        private static BoneWeight AverageLoopWeights(List<int> loop, BoneWeight[] srcWeights, bool hasWeights)
        {
            if (!hasWeights || srcWeights == null || loop == null || loop.Count == 0)
            {
                return default;
            }

            var boneAccum = new Dictionary<int, float>(8);
            for (int i = 0; i < loop.Count; i++)
            {
                int vertexIndex = loop[i];
                if (vertexIndex < 0 || vertexIndex >= srcWeights.Length)
                {
                    continue;
                }

                BoneWeight weight = srcWeights[vertexIndex];
                AccumulateBoneWeight(boneAccum, weight.boneIndex0, weight.weight0);
                AccumulateBoneWeight(boneAccum, weight.boneIndex1, weight.weight1);
                AccumulateBoneWeight(boneAccum, weight.boneIndex2, weight.weight2);
                AccumulateBoneWeight(boneAccum, weight.boneIndex3, weight.weight3);
            }

            if (boneAccum.Count == 0)
            {
                return srcWeights[loop[0]];
            }

            var ranked = new List<KeyValuePair<int, float>>(boneAccum);
            ranked.Sort((a, b) => b.Value.CompareTo(a.Value));

            float w0 = ranked.Count > 0 ? ranked[0].Value : 0f;
            float w1 = ranked.Count > 1 ? ranked[1].Value : 0f;
            float w2 = ranked.Count > 2 ? ranked[2].Value : 0f;
            float w3 = ranked.Count > 3 ? ranked[3].Value : 0f;
            float sum = w0 + w1 + w2 + w3;
            if (sum <= 1e-6f)
            {
                return srcWeights[loop[0]];
            }

            return new BoneWeight
            {
                boneIndex0 = ranked.Count > 0 ? ranked[0].Key : 0,
                boneIndex1 = ranked.Count > 1 ? ranked[1].Key : 0,
                boneIndex2 = ranked.Count > 2 ? ranked[2].Key : 0,
                boneIndex3 = ranked.Count > 3 ? ranked[3].Key : 0,
                weight0 = w0 / sum,
                weight1 = w1 / sum,
                weight2 = w2 / sum,
                weight3 = w3 / sum
            };
        }

        private static void AccumulateBoneWeight(Dictionary<int, float> boneAccum, int boneIndex, float weight)
        {
            if (weight <= 0f)
            {
                return;
            }

            if (boneAccum.TryGetValue(boneIndex, out float existing))
            {
                boneAccum[boneIndex] = existing + weight;
            }
            else
            {
                boneAccum[boneIndex] = weight;
            }
        }

        private static void AddCapQuad(
            List<int> triangles,
            int a0,
            int a1,
            int b0,
            int b1,
            bool flip)
        {
            if (flip)
            {
                triangles.Add(a0);
                triangles.Add(b0);
                triangles.Add(a1);
                triangles.Add(a1);
                triangles.Add(b0);
                triangles.Add(b1);
            }
            else
            {
                triangles.Add(a0);
                triangles.Add(a1);
                triangles.Add(b0);
                triangles.Add(a1);
                triangles.Add(b1);
                triangles.Add(b0);
            }
        }

        private static void AppendVertex(
            MeshBuffers buf, Vector3 pos, Vector3 normal, Vector4 tangent, Vector2 uv, Vector2 uv2,
            Color color, BoneWeight weight,
            bool hasNormals, bool hasTangents, bool hasUv, bool hasUv2, bool hasColors, bool hasWeights)
        {
            buf.vertices.Add(pos);
            if (hasNormals) buf.normals.Add(normal);
            if (hasTangents) buf.tangents.Add(tangent);
            if (hasUv) buf.uv.Add(uv);
            if (hasUv2) buf.uv2.Add(uv2);
            if (hasColors) buf.colors.Add(color);
            if (hasWeights) buf.weights.Add(weight);
        }

        private static long EdgeKey(int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            return ((long)lo << 32) | (uint)hi;
        }

        private static void AddEdge(Dictionary<long, int> counts, int a, int b)
        {
            long key = EdgeKey(a, b);
            counts.TryGetValue(key, out int c);
            counts[key] = c + 1;
        }

        private static void AddKeptEdge(Dictionary<long, int> counts, Dictionary<long, (int, int)> directed, int a, int b)
        {
            long key = EdgeKey(a, b);
            counts.TryGetValue(key, out int c);
            counts[key] = c + 1;
            directed[key] = (a, b);
        }

        private void DestroyGeneratedMesh()
        {
            if (generatedMesh != null)
            {
                Destroy(generatedMesh);
                generatedMesh = null;
            }
        }

        private void DestroyBattleColorMesh()
        {
            if (battleColorMesh != null)
            {
                Destroy(battleColorMesh);
                battleColorMesh = null;
            }

            battleMeshBaseColors = null;
            battleMeshWorkColors = null;
            cachedBattleBoneWeights = null;
        }

        private void OnDestroy()
        {
            if (deferredRebuildCoroutine != null)
            {
                StopCoroutine(deferredRebuildCoroutine);
                deferredRebuildCoroutine = null;
            }

            DestroyGeneratedMesh();
            DestroyBattleColorMesh();
        }
    }
}