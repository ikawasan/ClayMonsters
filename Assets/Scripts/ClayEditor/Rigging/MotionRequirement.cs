using System.Collections.Generic;
using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// 全モーションの実行条件をまとめて保有するScriptableObject
    /// 各モーションは部位ごとの最小本数で条件を表し、すべて満たすとき使用可能とみなす
    /// 攻撃などを増やすときは このアセットのリストへ要素を足すだけでよい
    /// </summary>
    [CreateAssetMenu(fileName = "MotionRequirement", menuName = "ClayEditor/MotionRequirement")]
    public class MotionRequirement : ScriptableObject
    {
        /// <summary>
        /// 1つのモーションの実行条件(部位ごとの最小本数)
        /// </summary>
        [System.Serializable]
        public class Entry
        {
            [Tooltip("このモーションの種類")]
            [SerializeField] private MotionType motionType = MotionType.Tackle;

            [Header("必要な部位の最小本数")]
            [Tooltip("必要な腕の最小本数")]
            [SerializeField] private int requiredArmCount = 0;

            [Tooltip("必要な脚の最小本数")]
            [SerializeField] private int requiredLegCount = 0;

            [Tooltip("必要な前(頭など)の最小本数")]
            [SerializeField] private int requiredFrontCount = 0;

            [Tooltip("必要な後ろ(尻尾など)の最小本数")]
            [SerializeField] private int requiredBackCount = 0;

            /// <summary>
            /// このモーションの種類
            /// </summary>
            public MotionType MotionType => motionType;

            /// <summary>
            /// 必要な腕の最小本数
            /// </summary>
            public int RequiredArmCount => requiredArmCount;

            /// <summary>
            /// 必要な脚の最小本数
            /// </summary>
            public int RequiredLegCount => requiredLegCount;

            /// <summary>
            /// 必要な前(頭など)の最小本数
            /// </summary>
            public int RequiredFrontCount => requiredFrontCount;

            /// <summary>
            /// 必要な後ろ(尻尾など)の最小本数
            /// </summary>
            public int RequiredBackCount => requiredBackCount;

            /// <summary>
            /// 与えられた部位本数が この条件をすべて満たすか判定する
            /// </summary>
            /// <param name="armCount">腕の本数</param>
            /// <param name="legCount">脚の本数</param>
            /// <param name="frontCount">前(頭など)の本数</param>
            /// <param name="backCount">後ろ(尻尾など)の本数</param>
            /// <returns>条件を満たすならtrue</returns>
            public bool IsSatisfied(int armCount, int legCount, int frontCount, int backCount)
            {
                if (armCount < requiredArmCount)
                {
                    return false;
                }

                if (legCount < requiredLegCount)
                {
                    return false;
                }

                if (frontCount < requiredFrontCount)
                {
                    return false;
                }

                if (backCount < requiredBackCount)
                {
                    return false;
                }

                return true;
            }
        }

        [Tooltip("全モーションの条件一覧")]
        [SerializeField] private List<Entry> entries = new();

        /// <summary>
        /// 全モーションの条件一覧
        /// </summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>
        /// 与えられた部位本数で 実行可能なモーションの一覧を返す
        /// </summary>
        /// <param name="armCount">腕の本数</param>
        /// <param name="legCount">脚の本数</param>
        /// <param name="frontCount">前(頭など)の本数</param>
        /// <param name="backCount">後ろ(尻尾など)の本数</param>
        /// <returns>条件を満たすモーションの種類のリスト</returns>
        public List<MotionType> GetAvailableMotions(int armCount, int legCount, int frontCount, int backCount)
        {
            var available = new List<MotionType>();

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.IsSatisfied(armCount, legCount, frontCount, backCount))
                {
                    available.Add(entry.MotionType);
                }
            }

            return available;
        }
    }
}