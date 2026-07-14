using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// 生成済みボーン階層を解析し、各枝(分岐点または末端までの一本道)を脚、腕、前(頭)、後ろ(尻尾)、胴体に分類して
    /// 実行可能なモーションを判定する、また各ボーンの部位分類も返す
    /// 前提として、モデルは立ち姿勢(上がワールドY)かつ正面がワールド+Zで造形される
    /// 脚は下方向(-Y)、腕は左右方向(X)、頭は前方(+Z)、尻尾は後方(-Z)に伸びるものとして分類する
    /// ルート直下に限らず、胴体の途中から分岐する腕や脚も枝として個別に評価する
    /// </summary>
    public class SkeletonPartAnalyzer : MonoBehaviour
    {
        [Header("分類のしきい値")]
        [Tooltip("枝を脚とみなす下方向(-Y)成分のしきい値、向きベクトルのy成分がこの値より小さいと脚")]
        [Range(-1f, 0f)]
        [SerializeField] private float legDownThreshold = -0.5f;

        [Tooltip("枝を腕とみなす左右(X)成分のしきい値、向きベクトルの|x|がこの値より大きいと腕候補")]
        [Range(0f, 1f)]
        [SerializeField] private float armSideThreshold = 0.5f;

        [Tooltip("枝を前後(頭/尻尾)とみなす前後(Z)成分のしきい値、向きベクトルの|z|がこの値より大きいと前後")]
        [Range(0f, 1f)]
        [SerializeField] private float frontBackThreshold = 0.5f;

        [Header("モーション可否の条件")]
        [Tooltip("歩行を可能とする脚の最小本数")]
        [SerializeField] private int minLegsForWalk = 2;

        [Tooltip("攻撃を可能とする腕の最小本数")]
        [SerializeField] private int minArmsForPunch = 1;

        [Header("モーション条件(全モーションを保有するアセット)")]
        [Tooltip("全モーションの条件をまとめたMotionRequirement")]
        [SerializeField] private MotionRequirement motionRequirement;

        /// <summary>
        /// 1つの枝(分岐点または末端までの一本道)の情報
        /// </summary>
        private struct Branch
        {
            public Transform start;          // 枝の始点(分岐の子、または分岐点ノード)
            public Transform end;            // 枝の終点(次の分岐点または末端)
            public List<Transform> bones;    // 枝に属するボーン群
        }

        /// <summary>
        /// ボーン階層を解析し、実行可能なモーションを判定して返す
        /// </summary>
        /// <param name="bones">生成済みボーン群(BoneSkeletonGeneratorの出力、ルートを含む)</param>
        /// <returns>実行可能なモーションの情報</returns>
        public MotionAvailability Analyze(Transform[] bones)
        {
            MotionAvailability result = new MotionAvailability();

            if (bones == null || bones.Length == 0)
            {
                return result;
            }

            CountParts(bones, out int armCount, out int legCount, out int frontCount, out int backCount);

            result.LegCount = legCount;
            result.ArmCount = armCount;
            result.FrontCount = frontCount;
            result.BackCount = backCount;
            result.CanWalk = legCount >= minLegsForWalk;
            result.CanPunch = armCount >= minArmsForPunch;

            return result;
        }

        /// <summary>
        /// インスペクターに設定したMotionRequirementで、実行可能なモーションの一覧を返す
        /// </summary>
        /// <param name="bones">生成済みボーン群</param>
        /// <returns>実行可能なモーションの種類のリスト</returns>
        public List<MotionType> GetAvailableMotions(Transform[] bones)
        {
            return GetAvailableMotions(bones, motionRequirement);
        }

        /// <summary>
        /// 与えられたMotionRequirementで、実行可能なモーションの一覧を返す
        /// </summary>
        /// <param name="bones">生成済みボーン群</param>
        /// <param name="requirement">全モーションの条件を保有するアセット</param>
        /// <returns>実行可能なモーションの種類のリスト</returns>
        public List<MotionType> GetAvailableMotions(Transform[] bones, MotionRequirement requirement)
        {
            if (requirement == null)
            {
                return new List<MotionType>();
            }

            CountParts(bones, out int armCount, out int legCount, out int frontCount, out int backCount);
            return requirement.GetAvailableMotions(armCount, legCount, frontCount, backCount);
        }

        /// <summary>
        /// ボーン階層から 腕、脚、前(頭)、後ろ(尻尾)それぞれの枝の本数を数える
        /// </summary>
        /// <param name="bones">生成済みボーン群</param>
        /// <param name="armCount">腕の本数</param>
        /// <param name="legCount">脚の本数</param>
        /// <param name="frontCount">前(頭など)の本数</param>
        /// <param name="backCount">後ろ(尻尾など)の本数</param>
        public void CountParts(Transform[] bones, out int armCount, out int legCount, out int frontCount, out int backCount)
        {
            armCount = 0;
            legCount = 0;
            frontCount = 0;
            backCount = 0;

            if (bones == null || bones.Length == 0)
            {
                return;
            }

            Transform root = FindRoot(bones);
            if (root == null)
            {
                return;
            }

            var boneSet = new HashSet<Transform>(bones);
            var branches = CollectBranches(root, boneSet);

            foreach (var branch in branches)
            {
                switch (ClassifyBranch(branch))
                {
                    case BonePart.Leg:
                        legCount++;
                        break;
                    case BonePart.Arm:
                        armCount++;
                        break;
                    case BonePart.Front:
                        frontCount++;
                        break;
                    case BonePart.Back:
                        backCount++;
                        break;
                }
            }
        }

        /// <summary>
        /// 各ボーンが属する部位を判定し、ボーンから部位への辞書で返す
        /// 木を分岐点ごとに枝へ分け、各枝の向きで分類して、枝に属する全ボーンへ同じ部位を割り当てる
        /// </summary>
        /// <param name="bones">生成済みボーン群</param>
        /// <returns>ボーンから部位への辞書</returns>
        public Dictionary<Transform, BonePart> ClassifyBones(Transform[] bones)
        {
            var map = new Dictionary<Transform, BonePart>();

            if (bones == null || bones.Length == 0)
            {
                return map;
            }

            Transform root = FindRoot(bones);
            if (root == null)
            {
                return map;
            }

            var boneSet = new HashSet<Transform>(bones);
            var branches = CollectBranches(root, boneSet);

            foreach (var branch in branches)
            {
                BonePart part = ClassifyBranch(branch);
                foreach (Transform b in branch.bones)
                {
                    map[b] = part;
                }
            }

            // ルートは胴体にする(枝に含まれていても上書き)
            map[root] = BonePart.Body;

            return map;
        }

        /// <summary>
        /// 木を分岐点ごとに枝へ分割して集める
        /// 各枝は分岐の子から、次の分岐点または末端までの一本道
        /// </summary>
        /// <param name="root">ルートボーン</param>
        /// <param name="boneSet">生成ボーンの集合</param>
        /// <returns>枝の一覧</returns>
        private List<Branch> CollectBranches(Transform root, HashSet<Transform> boneSet)
        {
            var branches = new List<Branch>();

            var startNodes = new Queue<Transform>();
            foreach (Transform child in GetBoneChildren(root, boneSet))
            {
                startNodes.Enqueue(child);
            }

            while (startNodes.Count > 0)
            {
                Transform start = startNodes.Dequeue();

                var branchBones = new List<Transform>();
                Transform current = start;
                branchBones.Add(current);

                // 子が1つの間は一本道としてたどる、子が0(末端)か2つ以上(分岐)で枝を終える
                var children = GetBoneChildren(current, boneSet);
                while (children.Count == 1)
                {
                    current = children[0];
                    branchBones.Add(current);
                    children = GetBoneChildren(current, boneSet);
                }

                branches.Add(new Branch
                {
                    start = start,
                    end = current,
                    bones = branchBones
                });

                // 分岐点(子が2つ以上)に到達したら、その各子から新たな枝を始める
                if (children.Count >= 2)
                {
                    foreach (Transform next in children)
                    {
                        startNodes.Enqueue(next);
                    }
                }
            }

            return branches;
        }

        /// <summary>
        /// ボーン集合に含まれる子だけを返す(可視化用オブジェクトなどを除外する)
        /// </summary>
        /// <param name="node">対象ノード</param>
        /// <param name="boneSet">生成ボーンの集合</param>
        /// <returns>ボーンである子の一覧</returns>
        private List<Transform> GetBoneChildren(Transform node, HashSet<Transform> boneSet)
        {
            var children = new List<Transform>();
            for (int i = 0; i < node.childCount; i++)
            {
                Transform child = node.GetChild(i);
                if (boneSet.Contains(child))
                {
                    children.Add(child);
                }
            }

            return children;
        }

        /// <summary>
        /// 1つの枝を向きで脚、腕、前(頭)、後ろ(尻尾)、胴体に分類する
        /// </summary>
        /// <param name="branch">対象の枝</param>
        /// <returns>分類した部位</returns>
        private BonePart ClassifyBranch(Branch branch)
        {
            Vector3 dir = branch.end.position - branch.start.position;
            if (dir.sqrMagnitude < 1e-8f)
            {
                return BonePart.Body;
            }

            dir.Normalize();

            // 下方向に伸びていれば脚
            if (dir.y < legDownThreshold)
            {
                return BonePart.Leg;
            }

            float absX = Mathf.Abs(dir.x);
            float absZ = Mathf.Abs(dir.z);

            // 左右に伸びていて、前後成分が小さければ腕
            if (absX > armSideThreshold && absX >= absZ)
            {
                return BonePart.Arm;
            }

            // 前後に伸びていれば、Zの符号で前(頭)と後ろ(尻尾)を分ける
            if (absZ > frontBackThreshold)
            {
                return dir.z >= 0f ? BonePart.Front : BonePart.Back;
            }

            // どれにも当てはまらない枝(縦の幹など)は胴体
            return BonePart.Body;
        }

        /// <summary>
        /// 生成ボーンのルートを求める、親がbonesに含まれないボーンをルートとする
        /// </summary>
        /// <param name="bones">生成済みボーン群</param>
        /// <returns>ルートボーン</returns>
        private Transform FindRoot(Transform[] bones)
        {
            var boneSet = new HashSet<Transform>(bones);
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                {
                    continue;
                }

                Transform parent = bones[i].parent;
                if (parent == null || !boneSet.Contains(parent))
                {
                    return bones[i];
                }
            }

            return bones.Length > 0 ? bones[0] : null;
        }
    }
}