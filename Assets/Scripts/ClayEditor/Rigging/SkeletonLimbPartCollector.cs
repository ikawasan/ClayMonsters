using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// 骨格ボーン配列から戦闘と同じ基準でリム根本を集める
    /// ModelPartLossControllerと保存時攻撃選定で共通利用する
    /// </summary>
    public static class SkeletonLimbPartCollector
    {
        /// <summary>
        /// 1リム(腕や脚など)の根本情報
        /// </summary>
        public readonly struct LimbRoot
        {
            /// <summary>
            /// リム根本情報を生成する
            /// </summary>
            /// <param name="part">部位種別</param>
            /// <param name="rootBone">リム根本ボーン</param>
            public LimbRoot(BonePart part, Transform rootBone)
            {
                Part = part;
                RootBone = rootBone;
            }

            /// <summary>
            /// 部位種別
            /// </summary>
            public BonePart Part { get; }

            /// <summary>
            /// リム根本ボーン
            /// </summary>
            public Transform RootBone { get; }
        }

        /// <summary>
        /// 骨格から検出したリム根本の一覧を返す
        /// </summary>
        /// <param name="analyzer">部位分類器</param>
        /// <param name="bones">ボーン配列</param>
        /// <returns>リム根本の一覧</returns>
        public static List<LimbRoot> CollectLimbRoots(SkeletonPartAnalyzer analyzer, Transform[] bones)
        {
            var limbRoots = new List<LimbRoot>();
            if (analyzer == null || bones == null || bones.Length == 0)
            {
                return limbRoots;
            }

            var boneSet = new HashSet<Transform>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                {
                    boneSet.Add(bones[i]);
                }
            }

            if (boneSet.Count == 0)
            {
                return limbRoots;
            }

            Dictionary<Transform, BonePart> partMap = analyzer.ClassifyBones(bones);
            Transform root = FindRoot(bones, boneSet);

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null || !IsLimbRoot(bone, root, boneSet, partMap))
                {
                    continue;
                }

                limbRoots.Add(new LimbRoot(GetPart(bone, partMap), bone));
            }

            return limbRoots;
        }

        /// <summary>
        /// 使用可能な部位を返す(Bodyは常に含む)
        /// </summary>
        /// <param name="analyzer">部位分類器</param>
        /// <param name="bones">ボーン配列</param>
        /// <returns>使用可能な部位</returns>
        public static HashSet<BonePart> CollectAvailableParts(SkeletonPartAnalyzer analyzer, Transform[] bones)
        {
            var parts = new HashSet<BonePart> { BonePart.Body };
            List<LimbRoot> limbRoots = CollectLimbRoots(analyzer, bones);
            for (int i = 0; i < limbRoots.Count; i++)
            {
                BonePart part = limbRoots[i].Part;
                if (part != BonePart.Body)
                {
                    parts.Add(part);
                }
            }

            return parts;
        }

        private static bool IsLimbRoot(
            Transform bone,
            Transform root,
            HashSet<Transform> boneSet,
            Dictionary<Transform, BonePart> partMap)
        {
            if (GetPart(bone, partMap) == BonePart.Body)
            {
                return false;
            }

            Transform parent = bone.parent;
            if (parent == null || !boneSet.Contains(parent) || parent == root)
            {
                return true;
            }

            return GetPart(parent, partMap) == BonePart.Body;
        }

        private static BonePart GetPart(Transform bone, Dictionary<Transform, BonePart> partMap)
        {
            return partMap.TryGetValue(bone, out BonePart part) ? part : BonePart.Body;
        }

        private static Transform FindRoot(Transform[] bones, HashSet<Transform> boneSet)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null)
                {
                    continue;
                }

                Transform parent = bone.parent;
                if (parent == null || !boneSet.Contains(parent))
                {
                    return bone;
                }
            }

            return bones.Length > 0 ? bones[0] : null;
        }
    }
}
