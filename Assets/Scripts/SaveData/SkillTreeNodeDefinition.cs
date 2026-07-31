using System;

namespace SaveData
{
    /// <summary>
    /// スキルツリー1ノードの定義
    /// </summary>
    public sealed class SkillTreeNodeDefinition
    {
        /// <summary>
        /// ノードID
        /// </summary>
        public SkillTreeNodeId Id { get; }

        /// <summary>
        /// 表示名
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 効果説明の書式(レベル効果が{0})
        /// </summary>
        public string DescriptionFormat { get; }

        /// <summary>
        /// 最大レベル
        /// </summary>
        public int MaxLevel { get; }

        /// <summary>
        /// 1レベル目の消費ポイント
        /// </summary>
        public int BaseCost { get; }

        /// <summary>
        /// レベルが上がるごとの追加コスト
        /// </summary>
        public int CostPerLevel { get; }

        /// <summary>
        /// 前提ノード(いずれも1以上必要)
        /// </summary>
        public SkillTreeNodeId[] Prerequisites { get; }

        /// <summary>
        /// 効果種別
        /// </summary>
        public SkillTreeEffectType EffectType { get; }

        /// <summary>
        /// 1レベルあたりの効果量
        /// </summary>
        public float EffectPerLevel { get; }

        /// <summary>
        /// 定義を生成する
        /// </summary>
        public SkillTreeNodeDefinition(
            SkillTreeNodeId id,
            string displayName,
            string descriptionFormat,
            int maxLevel,
            int baseCost,
            int costPerLevel,
            SkillTreeEffectType effectType,
            float effectPerLevel,
            params SkillTreeNodeId[] prerequisites)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
            DescriptionFormat = descriptionFormat ?? string.Empty;
            MaxLevel = Math.Max(1, maxLevel);
            BaseCost = Math.Max(0, baseCost);
            CostPerLevel = Math.Max(0, costPerLevel);
            EffectType = effectType;
            EffectPerLevel = effectPerLevel;
            Prerequisites = prerequisites ?? Array.Empty<SkillTreeNodeId>();
        }

        /// <summary>
        /// 次レベル解放に必要なポイント
        /// </summary>
        /// <param name="currentLevel">現在レベル</param>
        public int ResolveNextLevelCost(int currentLevel)
        {
            if (currentLevel < 0)
            {
                currentLevel = 0;
            }

            if (currentLevel >= MaxLevel)
            {
                return 0;
            }

            return BaseCost + (currentLevel * CostPerLevel);
        }

        /// <summary>
        /// 現在レベルまでの累計効果量
        /// </summary>
        /// <param name="level">レベル</param>
        public float ResolveEffectValue(int level)
        {
            if (level <= 0)
            {
                return 0f;
            }

            int clamped = level > MaxLevel ? MaxLevel : level;
            return EffectPerLevel * clamped;
        }
    }
}
