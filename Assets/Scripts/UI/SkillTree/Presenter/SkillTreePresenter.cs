using Localization;
using R3;
using SaveData;
using SaveData.Interface;
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
        private System.IDisposable pointsSubscription;
        private System.IDisposable bonusesSubscription;

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
                    if (isVisible)
                    {
                        RefreshAll();
                    }
                });
            isSetup = true;
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
            if (!skillTreeService.TryUnlockNextLevel(selectedNodeId))
            {
                RefreshDetail();
                return;
            }

            view.SetPoints(pointsService.Points);
            RefreshAll();
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
