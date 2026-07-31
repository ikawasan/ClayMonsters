using R3;
using SaveData.Interface;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaveData.Service
{
    /// <summary>
    /// スキルツリーの解放進捗と効果集計
    /// </summary>
    public sealed class SkillTreeService : ISkillTreeService
    {
        private readonly IPointsService pointsService;
        private readonly Dictionary<SkillTreeNodeId, int> levels = new();
        private readonly ReactiveProperty<SkillTreeBonuses> bonusesProperty =
            new(SkillTreeBonuses.None);

        /// <summary>
        /// 依存を注入して進捗を読み込む
        /// </summary>
        public SkillTreeService(IPointsService pointsService)
        {
            this.pointsService = pointsService;
            ReloadFromDisk();
        }

        /// <inheritdoc />
        public SkillTreeBonuses Bonuses => bonusesProperty.Value;

        /// <inheritdoc />
        public Observable<SkillTreeBonuses> BonusesObservable => bonusesProperty;

        /// <inheritdoc />
        public IReadOnlyList<SkillTreeNodeDefinition> AllNodes => SkillTreeCatalog.AllNodes;

        /// <inheritdoc />
        public int GetLevel(SkillTreeNodeId nodeId)
        {
            return levels.TryGetValue(nodeId, out int level) ? level : 0;
        }

        /// <inheritdoc />
        public bool ArePrerequisitesMet(SkillTreeNodeId nodeId)
        {
            if (!SkillTreeCatalog.TryGet(nodeId, out SkillTreeNodeDefinition definition))
            {
                return false;
            }

            SkillTreeNodeId[] prerequisites = definition.Prerequisites;
            if (prerequisites == null || prerequisites.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < prerequisites.Length; i++)
            {
                if (GetLevel(prerequisites[i]) <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public bool CanUnlockNextLevel(SkillTreeNodeId nodeId)
        {
            if (!SkillTreeCatalog.TryGet(nodeId, out SkillTreeNodeDefinition definition))
            {
                return false;
            }

            int current = GetLevel(nodeId);
            if (current >= definition.MaxLevel)
            {
                return false;
            }

            if (!ArePrerequisitesMet(nodeId))
            {
                return false;
            }

            int cost = definition.ResolveNextLevelCost(current);
            return pointsService != null && pointsService.Points >= cost;
        }

        /// <inheritdoc />
        public int GetNextLevelCost(SkillTreeNodeId nodeId)
        {
            if (!SkillTreeCatalog.TryGet(nodeId, out SkillTreeNodeDefinition definition))
            {
                return 0;
            }

            return definition.ResolveNextLevelCost(GetLevel(nodeId));
        }

        /// <inheritdoc />
        public bool TryUnlockNextLevel(SkillTreeNodeId nodeId)
        {
            if (!SkillTreeCatalog.TryGet(nodeId, out SkillTreeNodeDefinition definition))
            {
                Debug.LogWarning($"[SkillTreeService] 未知のノードです id={nodeId}");
                return false;
            }

            int current = GetLevel(nodeId);
            if (current >= definition.MaxLevel)
            {
                return false;
            }

            if (!ArePrerequisitesMet(nodeId))
            {
                return false;
            }

            int cost = definition.ResolveNextLevelCost(current);
            if (pointsService == null || !pointsService.TrySpendPoints(cost))
            {
                return false;
            }

            levels[nodeId] = current + 1;
            RebuildBonuses();
            Persist();
            Debug.Log(
                "[SkillTreeService] ノード解放"
                + $" id={nodeId}"
                + $" level={levels[nodeId]}"
                + $" cost={cost}");
            return true;
        }

        /// <inheritdoc />
        public int ApplyPointsGainBonus(int basePoints)
        {
            if (basePoints <= 0)
            {
                return 0;
            }

            float percent = Bonuses.PointsGainPercent;
            if (percent <= 0f)
            {
                return basePoints;
            }

            int bonus = Mathf.FloorToInt(basePoints * percent / 100f);
            return basePoints + Mathf.Max(0, bonus);
        }

        /// <inheritdoc />
        public void Reload()
        {
            ReloadFromDisk();
        }

        /// <inheritdoc />
        public void UnlockAllNodes()
        {
            IReadOnlyList<SkillTreeNodeDefinition> nodes = SkillTreeCatalog.AllNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                SkillTreeNodeDefinition definition = nodes[i];
                levels[definition.Id] = definition.MaxLevel;
            }

            RebuildBonuses();
            Persist();
            Debug.Log($"[SkillTreeService] 全ノードを解放しました count={nodes.Count}");
        }

        /// <inheritdoc />
        public void ResetProgress()
        {
            levels.Clear();
            RebuildBonuses();
            Persist();
            Debug.Log("[SkillTreeService] スキルツリーを初期化しました");
        }

        private void ReloadFromDisk()
        {
            levels.Clear();
            SaveData saveData = SaveDataManager.Load();
            SkillTreeSaveData progress = saveData.SkillTree ?? new SkillTreeSaveData();
            NormalizeProgress(progress);
            if (progress.nodeIds != null && progress.levels != null)
            {
                int count = Math.Min(progress.nodeIds.Length, progress.levels.Length);
                for (int i = 0; i < count; i++)
                {
                    var id = (SkillTreeNodeId)progress.nodeIds[i];
                    if (!SkillTreeCatalog.TryGet(id, out SkillTreeNodeDefinition definition))
                    {
                        continue;
                    }

                    int level = Mathf.Clamp(progress.levels[i], 0, definition.MaxLevel);
                    if (level > 0)
                    {
                        levels[id] = level;
                    }
                }
            }

            RebuildBonuses();
        }

        private void Persist()
        {
            var progress = new SkillTreeSaveData
            {
                nodeIds = new int[levels.Count],
                levels = new int[levels.Count]
            };
            int index = 0;
            foreach (KeyValuePair<SkillTreeNodeId, int> pair in levels)
            {
                progress.nodeIds[index] = (int)pair.Key;
                progress.levels[index] = pair.Value;
                index++;
            }

            SaveDataManager.Update(data =>
            {
                data.SkillTree = progress;
            });
        }

        private void RebuildBonuses()
        {
            int startingHp = 0;
            int startingAttack = 0;
            int startingDefense = 0;
            int startingSpeed = 0;
            int startingHit = 0;
            int startingAll = 0;
            float greatSuccess = 0f;
            int startingMoney = 0;
            float trainingMoney = 0f;
            float pointsGain = 0f;
            int inheritance = 0;

            IReadOnlyList<SkillTreeNodeDefinition> nodes = SkillTreeCatalog.AllNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                SkillTreeNodeDefinition definition = nodes[i];
                int level = GetLevel(definition.Id);
                if (level <= 0)
                {
                    continue;
                }

                float value = definition.ResolveEffectValue(level);
                switch (definition.EffectType)
                {
                    case SkillTreeEffectType.StartingHp:
                        startingHp += Mathf.RoundToInt(value);
                        break;
                    case SkillTreeEffectType.StartingAttack:
                        startingAttack += Mathf.RoundToInt(value);
                        break;
                    case SkillTreeEffectType.StartingDefense:
                        startingDefense += Mathf.RoundToInt(value);
                        break;
                    case SkillTreeEffectType.StartingSpeed:
                        startingSpeed += Mathf.RoundToInt(value);
                        break;
                    case SkillTreeEffectType.StartingHit:
                        startingHit += Mathf.RoundToInt(value);
                        break;
                    case SkillTreeEffectType.StartingAll:
                        startingAll += Mathf.RoundToInt(value);
                        break;
                    case SkillTreeEffectType.GreatSuccessPercent:
                        greatSuccess += value;
                        break;
                    case SkillTreeEffectType.StartingMoney:
                        startingMoney += Mathf.RoundToInt(value);
                        break;
                    case SkillTreeEffectType.TrainingMoneyPercent:
                        trainingMoney += value;
                        break;
                    case SkillTreeEffectType.PointsGainPercent:
                        pointsGain += value;
                        break;
                    case SkillTreeEffectType.InheritancePercent:
                        inheritance += Mathf.RoundToInt(value);
                        break;
                }
            }

            bonusesProperty.Value = new SkillTreeBonuses(
                startingHp,
                startingAttack,
                startingDefense,
                startingSpeed,
                startingHit,
                startingAll,
                greatSuccess,
                startingMoney,
                trainingMoney,
                pointsGain,
                inheritance);
        }

        private static void NormalizeProgress(SkillTreeSaveData progress)
        {
            if (progress == null)
            {
                return;
            }

            progress.nodeIds ??= Array.Empty<int>();
            progress.levels ??= Array.Empty<int>();
        }
    }
}
