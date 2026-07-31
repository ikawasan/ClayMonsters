using R3;
using System.Collections.Generic;

namespace SaveData.Interface
{
    /// <summary>
    /// スキルツリーの解放と効果参照
    /// </summary>
    public interface ISkillTreeService
    {
        /// <summary>
        /// 現在の集計ボーナス
        /// </summary>
        SkillTreeBonuses Bonuses { get; }

        /// <summary>
        /// ボーナス変化の購読
        /// </summary>
        Observable<SkillTreeBonuses> BonusesObservable { get; }

        /// <summary>
        /// 全ノード定義
        /// </summary>
        IReadOnlyList<SkillTreeNodeDefinition> AllNodes { get; }

        /// <summary>
        /// ノードの現在レベル
        /// </summary>
        /// <param name="nodeId">ノードID</param>
        int GetLevel(SkillTreeNodeId nodeId);

        /// <summary>
        /// 前提を満たしているか
        /// </summary>
        /// <param name="nodeId">ノードID</param>
        bool ArePrerequisitesMet(SkillTreeNodeId nodeId);

        /// <summary>
        /// 次レベルを解放できるか
        /// </summary>
        /// <param name="nodeId">ノードID</param>
        bool CanUnlockNextLevel(SkillTreeNodeId nodeId);

        /// <summary>
        /// 次レベル解放コスト
        /// </summary>
        /// <param name="nodeId">ノードID</param>
        int GetNextLevelCost(SkillTreeNodeId nodeId);

        /// <summary>
        /// ポイントを消費して次レベルを解放する
        /// </summary>
        /// <param name="nodeId">ノードID</param>
        bool TryUnlockNextLevel(SkillTreeNodeId nodeId);

        /// <summary>
        /// ポイント取得量に効率ボーナスを適用する
        /// </summary>
        /// <param name="basePoints">基本ポイント</param>
        int ApplyPointsGainBonus(int basePoints);

        /// <summary>
        /// 永続化データから再読込する
        /// </summary>
        void Reload();

        /// <summary>
        /// 全ノードを最大レベルまで解放する
        /// </summary>
        void UnlockAllNodes();

        /// <summary>
        /// スキルツリーの解放進捗を初期化する
        /// </summary>
        void ResetProgress();
    }
}
