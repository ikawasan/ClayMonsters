using Localization;
using R3;
using SaveData;
using SaveData.Interface;
using System.Collections.Generic;
using UI.SkillTree.Interface;
using UI.SkillTree.View;
using UnityEngine;
using VContainer;

namespace UI.SkillTree.Presenter
{
    /// <summary>
    /// スキルツリーの表示と解放操作
    /// </summary>
    public sealed class SkillTreePresenter : ISkillTreePresenter
    {
        private readonly ISkillTreeView view;
        private readonly ISkillTreeService skillTreeService;
        private readonly IPointsService pointsService;

        private SkillTreeNodeId selectedNodeId = SkillTreeNodeId.Center;
        private bool isSetup;
        private bool isVisible;
        private bool suppressRefreshAnimation;
        private System.IDisposable pointsSubscription;
        private System.IDisposable bonusesSubscription;
        private System.IDisposable languageSubscription;

        /// <summary>
        /// 依存を注入する
        /// </summary>
        [Inject]
        public SkillTreePresenter(
            ISkillTreeView view,
            ISkillTreeService skillTreeService,
            IPointsService pointsService)
        {
            this.view = view;
            this.skillTreeService = skillTreeService;
            this.pointsService = pointsService;
        }

        /// <inheritdoc />
        public void Setup()
        {
            if (isSetup)
            {
                return;
            }

            view.SubscribeCloseButtonClick(Hide);
            view.SubscribeUnlockButtonClick(OnClickUnlock);
            view.SubscribeNodeSelected(OnNodeSelected);
            pointsSubscription = pointsService.PointsObservable
                .Subscribe(points => view.SetPoints(points));
            bonusesSubscription = skillTreeService.BonusesObservable
                .Subscribe(_ =>
                {
                    if (isVisible && !suppressRefreshAnimation)
                    {
                        RefreshAll();
                    }
                });
            languageSubscription = LanguageAwareUi.Register(OnLanguageChanged);
            isSetup = true;
        }

        private void OnLanguageChanged()
        {
            if (!isVisible)
            {
                return;
            }

            view.SetPoints(pointsService.Points);
            RefreshAll();
        }

        /// <inheritdoc />
        public void Show()
        {
            Setup();
            skillTreeService.Reload();
            pointsService.Reload();
            view.SetPoints(pointsService.Points);
            isVisible = true;
            RefreshAll();
            view.Show();
        }

        /// <inheritdoc />
        public void Hide()
        {
            isVisible = false;
            view.Hide();
        }

        private void OnNodeSelected(SkillTreeNodeId nodeId)
        {
            if (!skillTreeService.ArePrerequisitesMet(nodeId) && skillTreeService.GetLevel(nodeId) <= 0)
            {
                return;
            }

            selectedNodeId = nodeId;
            RefreshAll();
        }

        private void OnClickUnlock()
        {
            SkillTreeNodeId unlockingNodeId = selectedNodeId;
            List<SkillTreeNodeId> hiddenBefore = CollectHiddenNodeIds();

            suppressRefreshAnimation = true;
            bool unlocked;
            try
            {
                unlocked = skillTreeService.TryUnlockNextLevel(unlockingNodeId);
            }
            finally
            {
                suppressRefreshAnimation = false;
            }

            if (!unlocked)
            {
                RefreshDetail();
                return;
            }

            view.SetPoints(pointsService.Points);
            RefreshAll();

            List<SkillTreeNodeId> newlyRevealed = CollectNewlyRevealedNodeIds(hiddenBefore);
            view.PlayUnlockFeedback(unlockingNodeId, newlyRevealed);
        }

        private List<SkillTreeNodeId> CollectHiddenNodeIds()
        {
            IReadOnlyList<SkillTreeNodeDefinition> nodes = skillTreeService.AllNodes;
            List<SkillTreeNodeId> hidden = new(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                SkillTreeNodeId id = nodes[i].Id;
                if (!IsNodeVisible(id))
                {
                    hidden.Add(id);
                }
            }

            return hidden;
        }

        private List<SkillTreeNodeId> CollectNewlyRevealedNodeIds(List<SkillTreeNodeId> hiddenBefore)
        {
            List<SkillTreeNodeId> revealed = new();
            if (hiddenBefore == null || hiddenBefore.Count == 0)
            {
                return revealed;
            }

            for (int i = 0; i < hiddenBefore.Count; i++)
            {
                SkillTreeNodeId id = hiddenBefore[i];
                if (IsNodeVisible(id) && skillTreeService.GetLevel(id) <= 0)
                {
                    revealed.Add(id);
                }
            }

            return revealed;
        }

        private bool IsNodeVisible(SkillTreeNodeId nodeId)
        {
            if (skillTreeService.GetLevel(nodeId) > 0)
            {
                return true;
            }

            return skillTreeService.ArePrerequisitesMet(nodeId);
        }

        private void RefreshAll()
        {
            EnsureSelectedNodeVisible();
            view.RefreshNodes(BindNode);
            view.RefreshConnections(skillTreeService.GetLevel);
            RefreshBonusSummary();
            RefreshDetail();
        }

        private void EnsureSelectedNodeVisible()
        {
            if (skillTreeService.GetLevel(selectedNodeId) > 0
                || skillTreeService.ArePrerequisitesMet(selectedNodeId))
            {
                return;
            }

            selectedNodeId = SkillTreeNodeId.Center;
        }

        private void RefreshBonusSummary()
        {
            view.SetBonusSummary(
                SkillTreeBonusSummaryFormatter.Format(skillTreeService.Bonuses));
        }

        private void BindNode(SkillTreeNodeView node)
        {
            if (node == null)
            {
                return;
            }

            if (!SkillTreeCatalog.TryGet(node.NodeId, out _))
            {
                Debug.LogError($"[SkillTreePresenter] 未知のノードです id={node.NodeId}");
                return;
            }

            int level = skillTreeService.GetLevel(node.NodeId);
            bool prerequisitesMet = skillTreeService.ArePrerequisitesMet(node.NodeId);
            bool selected = node.NodeId == selectedNodeId;
            node.Bind(level, prerequisitesMet, selected);
        }

        private void RefreshDetail()
        {
            if (!SkillTreeCatalog.TryGet(selectedNodeId, out SkillTreeNodeDefinition definition))
            {
                view.SetDetail(
                    string.Empty,
                    LocalizedText.Get(GameTextKeys.SkillTreeSelectNode),
                    LocalizedText.Get(GameTextKeys.SkillTreeUnlock),
                    string.Empty,
                    false);
                return;
            }

            int level = skillTreeService.GetLevel(selectedNodeId);
            bool unlocked = level > 0;
            int cost = skillTreeService.GetNextLevelCost(selectedNodeId);
            bool canUnlock = skillTreeService.CanUnlockNextLevel(selectedNodeId);
            string nodeIdName = definition.Id.ToString();
            string description = LocalizedText.FormatOrdinal(
                GameTextKeys.SkillTreeNodeDesc(nodeIdName),
                definition.DescriptionFormat,
                FormatEffect(definition.EffectType, definition.EffectPerLevel));

            string actionLabel;
            string statusLabel;
            if (unlocked)
            {
                actionLabel = LocalizedText.Get(GameTextKeys.SkillTreeUnlocked);
                statusLabel = string.Empty;
            }
            else if (pointsService.Points < cost)
            {
                actionLabel = $"{cost} P";
                statusLabel = LocalizedText.Get(GameTextKeys.SkillTreePointsShort);
            }
            else
            {
                actionLabel = $"{cost} P";
                statusLabel = string.Empty;
            }

            view.SetDetail(
                LocalizedText.GetOrFallback(
                    GameTextKeys.SkillTreeNodeName(nodeIdName),
                    definition.DisplayName),
                description,
                actionLabel,
                statusLabel,
                canUnlock);
        }

        private static string FormatEffect(SkillTreeEffectType effectType, float perLevel)
        {
            switch (effectType)
            {
                case SkillTreeEffectType.GreatSuccessPercent:
                case SkillTreeEffectType.TrainingMoneyPercent:
                case SkillTreeEffectType.PointsGainPercent:
                case SkillTreeEffectType.InheritancePercent:
                    return $"{perLevel:0.#}";
                default:
                    return $"{Mathf.RoundToInt(perLevel)}";
            }
        }
    }
}
