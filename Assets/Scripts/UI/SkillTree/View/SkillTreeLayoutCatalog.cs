using SaveData;
using System.Collections.Generic;
using UnityEngine;

namespace UI.SkillTree.View
{
    /// <summary>
    /// スキルツリーノードのマス目配置
    /// </summary>
    public static class SkillTreeLayoutCatalog
    {
        /// <summary>
        /// 1マスあたりのピクセル幅
        /// </summary>
        public const float CellWidth = 140f;

        /// <summary>
        /// 1マスあたりのピクセル高さ
        /// </summary>
        public const float CellHeight = 140f;

        private static readonly Dictionary<SkillTreeNodeId, Vector2Int> GridById =
            new Dictionary<SkillTreeNodeId, Vector2Int>
            {
                // 中央
                { SkillTreeNodeId.Center, new Vector2Int(0, 0) },

                // 上方向:基礎ステ系統
                { SkillTreeNodeId.StartingHp, new Vector2Int(0, 1) },
                { SkillTreeNodeId.StartingHp2, new Vector2Int(0, 2) },
                { SkillTreeNodeId.StartingHp3, new Vector2Int(0, 3) },
                { SkillTreeNodeId.StartingHp4, new Vector2Int(0, 4) },
                { SkillTreeNodeId.StartingHp5, new Vector2Int(0, 5) },
                { SkillTreeNodeId.StartingHp6, new Vector2Int(-1, 5) },
                { SkillTreeNodeId.StartingHp7, new Vector2Int(-1, 6) },
                { SkillTreeNodeId.StartingAll5, new Vector2Int(-1, 7) },

                { SkillTreeNodeId.StartingAttack, new Vector2Int(2, 1) },
                { SkillTreeNodeId.StartingAttack2, new Vector2Int(2, 2) },
                { SkillTreeNodeId.StartingAttack3, new Vector2Int(2, 3) },
                { SkillTreeNodeId.StartingAttack4, new Vector2Int(2, 4) },
                { SkillTreeNodeId.StartingAttack5, new Vector2Int(3, 4) },
                { SkillTreeNodeId.StartingAttack6, new Vector2Int(3, 5) },
                { SkillTreeNodeId.StartingAll, new Vector2Int(3, 6) },

                { SkillTreeNodeId.StartingDefense, new Vector2Int(4, 1) },
                { SkillTreeNodeId.StartingDefense2, new Vector2Int(4, 2) },
                { SkillTreeNodeId.StartingDefense3, new Vector2Int(4, 3) },
                { SkillTreeNodeId.StartingDefense4, new Vector2Int(4, 4) },
                { SkillTreeNodeId.StartingDefense5, new Vector2Int(5, 4) },
                { SkillTreeNodeId.StartingDefense6, new Vector2Int(5, 5) },
                { SkillTreeNodeId.StartingAll2, new Vector2Int(5, 6) },

                { SkillTreeNodeId.StartingSpeed, new Vector2Int(-2, 1) },
                { SkillTreeNodeId.StartingSpeed2, new Vector2Int(-2, 2) },
                { SkillTreeNodeId.StartingSpeed3, new Vector2Int(-2, 3) },
                { SkillTreeNodeId.StartingSpeed4, new Vector2Int(-2, 4) },
                { SkillTreeNodeId.StartingSpeed5, new Vector2Int(-3, 4) },
                { SkillTreeNodeId.StartingSpeed6, new Vector2Int(-3, 5) },
                { SkillTreeNodeId.StartingAll3, new Vector2Int(-3, 6) },

                { SkillTreeNodeId.StartingHit, new Vector2Int(-4, 1) },
                { SkillTreeNodeId.StartingHit2, new Vector2Int(-4, 2) },
                { SkillTreeNodeId.StartingHit3, new Vector2Int(-4, 3) },
                { SkillTreeNodeId.StartingHit4, new Vector2Int(-4, 4) },
                { SkillTreeNodeId.StartingHit5, new Vector2Int(-5, 4) },
                { SkillTreeNodeId.StartingHit6, new Vector2Int(-5, 5) },
                { SkillTreeNodeId.StartingAll4, new Vector2Int(-5, 6) },

                // 下方向:大成功
                { SkillTreeNodeId.GreatSuccess1, new Vector2Int(0, -1) },
                { SkillTreeNodeId.GreatSuccess2, new Vector2Int(0, -2) },
                { SkillTreeNodeId.GreatSuccess3, new Vector2Int(0, -3) },
                { SkillTreeNodeId.GreatSuccess4, new Vector2Int(0, -4) },
                { SkillTreeNodeId.GreatSuccess5, new Vector2Int(0, -5) },
                { SkillTreeNodeId.GreatSuccess6, new Vector2Int(0, -6) },
                { SkillTreeNodeId.GreatSuccess7, new Vector2Int(0, -7) },
                { SkillTreeNodeId.GreatSuccess8, new Vector2Int(1, -6) },
                { SkillTreeNodeId.GreatSuccess9, new Vector2Int(1, -7) },
                { SkillTreeNodeId.GreatSuccess10, new Vector2Int(1, -8) },

                // 左下:所持金と獲得金
                { SkillTreeNodeId.StartingMoney1, new Vector2Int(-2, -1) },
                { SkillTreeNodeId.StartingMoney2, new Vector2Int(-2, -2) },
                { SkillTreeNodeId.StartingMoney3, new Vector2Int(-2, -3) },
                { SkillTreeNodeId.StartingMoney4, new Vector2Int(-2, -4) },
                { SkillTreeNodeId.StartingMoney5, new Vector2Int(-2, -5) },
                { SkillTreeNodeId.StartingMoney6, new Vector2Int(-2, -6) },
                { SkillTreeNodeId.StartingMoney7, new Vector2Int(-3, -6) },
                { SkillTreeNodeId.StartingMoney8, new Vector2Int(-3, -7) },

                { SkillTreeNodeId.TrainingMoney1, new Vector2Int(-4, -2) },
                { SkillTreeNodeId.TrainingMoney2, new Vector2Int(-4, -3) },
                { SkillTreeNodeId.TrainingMoney3, new Vector2Int(-4, -4) },
                { SkillTreeNodeId.TrainingMoney4, new Vector2Int(-4, -5) },
                { SkillTreeNodeId.TrainingMoney5, new Vector2Int(-4, -6) },
                { SkillTreeNodeId.TrainingMoney6, new Vector2Int(-4, -7) },
                { SkillTreeNodeId.TrainingMoney7, new Vector2Int(-4, -8) },
                { SkillTreeNodeId.TrainingMoney8, new Vector2Int(-5, -4) },
                { SkillTreeNodeId.TrainingMoney9, new Vector2Int(-5, -5) },
                { SkillTreeNodeId.TrainingMoney10, new Vector2Int(-5, -6) },

                // 右下:ポイント効率
                { SkillTreeNodeId.PointsGain1, new Vector2Int(2, -1) },
                { SkillTreeNodeId.PointsGain2, new Vector2Int(2, -2) },
                { SkillTreeNodeId.PointsGain3, new Vector2Int(2, -3) },
                { SkillTreeNodeId.PointsGain4, new Vector2Int(2, -4) },
                { SkillTreeNodeId.PointsGain5, new Vector2Int(2, -5) },
                { SkillTreeNodeId.PointsGain6, new Vector2Int(2, -6) },
                { SkillTreeNodeId.PointsGain7, new Vector2Int(2, -7) },
                { SkillTreeNodeId.PointsGain8, new Vector2Int(3, -6) },
                { SkillTreeNodeId.PointsGain9, new Vector2Int(3, -7) },
                { SkillTreeNodeId.PointsGain10, new Vector2Int(3, -8) },

                // さらに右下:継承
                { SkillTreeNodeId.Inheritance1, new Vector2Int(5, -1) },
                { SkillTreeNodeId.Inheritance2, new Vector2Int(5, -2) },
                { SkillTreeNodeId.Inheritance3, new Vector2Int(5, -3) },
                { SkillTreeNodeId.Inheritance4, new Vector2Int(5, -4) },
                { SkillTreeNodeId.Inheritance5, new Vector2Int(5, -5) },
                { SkillTreeNodeId.Inheritance6, new Vector2Int(5, -6) },
                { SkillTreeNodeId.Inheritance7, new Vector2Int(5, -7) },
                { SkillTreeNodeId.Inheritance8, new Vector2Int(6, -6) },
                { SkillTreeNodeId.Inheritance9, new Vector2Int(6, -7) },
                { SkillTreeNodeId.Inheritance10, new Vector2Int(6, -8) }
            };

        /// <summary>
        /// ノードのマス座標を取得する
        /// </summary>
        /// <param name="nodeId">ノードID</param>
        /// <param name="grid">マス座標</param>
        /// <returns>定義があればtrue</returns>
        public static bool TryGetGrid(SkillTreeNodeId nodeId, out Vector2Int grid)
        {
            return GridById.TryGetValue(nodeId, out grid);
        }

        /// <summary>
        /// マス座標をアンカー位置へ変換する
        /// </summary>
        /// <param name="grid">マス座標</param>
        /// <returns>アンカー位置</returns>
        public static Vector2 ToAnchoredPosition(Vector2Int grid)
        {
            return new Vector2(grid.x * CellWidth, grid.y * CellHeight);
        }
    }
}
