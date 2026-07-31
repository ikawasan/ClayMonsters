using Extensions;
using LighthouseExtends.UIComponent.Button;
using SaveData;
using System;
using System.Collections.Generic;
using TMPro;
using UI.SkillTree.Interface;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI.SkillTree.View
{
    /// <summary>
    /// スキルツリー本体UI
    /// Canvas.enabledで表示切替する
    /// </summary>
    public sealed class SkillTreeView : MonoBehaviour, ISkillTreeView
    {
        private static readonly Color LockedConnectionColor = new(0.45f, 0.42f, 0.38f, 0.55f);
        private static readonly Color UnlockedConnectionColor = new(0.95f, 0.92f, 0.85f, 0.95f);

        [SerializeField] private Canvas canvas;
        [SerializeField] private LHButton closeButton;
        [SerializeField] private LHButton unlockButton;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailDescriptionText;
        [SerializeField] private TMP_Text unlockButtonLabel;
        [SerializeField] private TMP_Text detailStatusText;
        [SerializeField] private TMP_Text bonusSummaryText;
        [SerializeField] private Canvas bonusSummaryCanvas;
        [SerializeField] private Toggle bonusSummaryToggle;
        [SerializeField] private SkillTreeNodeView[] nodeViews;
        [SerializeField] private SkillTreePanZoom panZoom;
        [SerializeField] private Image[] connectionLines;
        [SerializeField] private float connectionThickness = 4f;

        private UnityAction<SkillTreeNodeId> nodeSelectedAction;
        private Dictionary<SkillTreeNodeId, SkillTreeNodeView> nodeMap;

        private void Awake()
        {
            ValidateReferences();
            RebuildNodeMap();
            InitializeBonusSummaryToggle();
            Hide();
        }

        /// <inheritdoc />
        public void Show()
        {
            if (canvas == null)
            {
                return;
            }

            if (panZoom != null)
            {
                panZoom.ResetView();
            }

            canvas.enabled = true;
            ApplyBonusSummaryCanvasVisibility();
            Canvas.ForceUpdateCanvases();
            RefreshConnectionLayoutOnly();
        }

        /// <inheritdoc />
        public void Hide()
        {
            if (canvas == null)
            {
                return;
            }

            canvas.enabled = false;
            // 入れ子Canvasは親無効でも単独描画されるため明示的に閉じる
            if (bonusSummaryCanvas != null)
            {
                bonusSummaryCanvas.enabled = false;
            }
        }

        /// <inheritdoc />
        public void SetPoints(int points)
        {
            if (pointsText == null)
            {
                return;
            }

            pointsText.text = $"{Mathf.Max(0, points)} ポイント";
        }

        /// <inheritdoc />
        public void RefreshNodes(Action<SkillTreeNodeView> bindNode)
        {
            if (nodeViews == null || bindNode == null)
            {
                return;
            }

            RebuildNodeMap();
            for (int i = 0; i < nodeViews.Length; i++)
            {
                SkillTreeNodeView node = nodeViews[i];
                if (node == null)
                {
                    continue;
                }

                bindNode(node);
            }
        }

        /// <inheritdoc />
        public void RefreshConnections(Func<SkillTreeNodeId, int> getLevel)
        {
            if (connectionLines == null || getLevel == null)
            {
                return;
            }

            RebuildNodeMap();
            IReadOnlyList<SkillTreeEdge> edges = SkillTreeCatalog.PrerequisiteEdges;
            int count = Mathf.Min(connectionLines.Length, edges.Count);
            for (int i = 0; i < count; i++)
            {
                Image line = connectionLines[i];
                if (line == null)
                {
                    continue;
                }

                SkillTreeEdge edge = edges[i];
                if (!TryGetNode(edge.From, out SkillTreeNodeView fromNode)
                    || !TryGetNode(edge.To, out SkillTreeNodeView toNode))
                {
                    line.enabled = false;
                    continue;
                }

                bool fromVisible = IsNodeVisible(edge.From, getLevel);
                bool toVisible = IsNodeVisible(edge.To, getLevel);
                if (!fromVisible || !toVisible)
                {
                    line.enabled = false;
                    continue;
                }

                line.enabled = true;
                LayoutConnectionLine(line.rectTransform, fromNode.RectTransform, toNode.RectTransform);
                bool fromUnlocked = getLevel(edge.From) > 0;
                line.color = fromUnlocked ? UnlockedConnectionColor : LockedConnectionColor;
            }

            for (int i = count; i < connectionLines.Length; i++)
            {
                if (connectionLines[i] != null)
                {
                    connectionLines[i].enabled = false;
                }
            }
        }

        /// <inheritdoc />
        public void SetDetail(
            string name,
            string description,
            string actionLabel,
            string statusLabel,
            bool canUnlock)
        {
            if (detailNameText != null)
            {
                detailNameText.text = name ?? string.Empty;
            }

            if (detailDescriptionText != null)
            {
                detailDescriptionText.text = description ?? string.Empty;
            }

            if (unlockButtonLabel != null)
            {
                unlockButtonLabel.text = actionLabel ?? string.Empty;
            }

            if (detailStatusText != null)
            {
                detailStatusText.text = statusLabel ?? string.Empty;
            }

            if (unlockButton != null)
            {
                unlockButton.interactable = canUnlock;
            }
        }

        /// <inheritdoc />
        public void SetBonusSummary(string summaryText)
        {
            if (bonusSummaryText == null)
            {
                Debug.LogError("[SkillTreeView] bonusSummaryTextが未配線です", this);
                return;
            }

            bonusSummaryText.text = summaryText ?? string.Empty;
        }

        /// <inheritdoc />
        public IDisposable SubscribeCloseButtonClick(UnityAction action)
        {
            if (closeButton == null)
            {
                Debug.LogError("[SkillTreeView] closeButtonが未配線です", this);
                return new EmptyDisposable();
            }

            return closeButton.SubscribeOnClick(action);
        }

        /// <inheritdoc />
        public IDisposable SubscribeUnlockButtonClick(UnityAction action)
        {
            if (unlockButton == null)
            {
                Debug.LogError("[SkillTreeView] unlockButtonが未配線です", this);
                return new EmptyDisposable();
            }

            return unlockButton.SubscribeOnClick(action);
        }

        /// <inheritdoc />
        public void SubscribeNodeSelected(UnityAction<SkillTreeNodeId> action)
        {
            nodeSelectedAction = action;
            if (nodeViews == null)
            {
                return;
            }

            for (int i = 0; i < nodeViews.Length; i++)
            {
                SkillTreeNodeView node = nodeViews[i];
                if (node == null)
                {
                    continue;
                }

                SkillTreeNodeId id = node.NodeId;
                node.SubscribeClick(() => nodeSelectedAction?.Invoke(id));
            }
        }

        private void RefreshConnectionLayoutOnly()
        {
            if (connectionLines == null)
            {
                return;
            }

            RebuildNodeMap();
            IReadOnlyList<SkillTreeEdge> edges = SkillTreeCatalog.PrerequisiteEdges;
            int count = Mathf.Min(connectionLines.Length, edges.Count);
            for (int i = 0; i < count; i++)
            {
                Image line = connectionLines[i];
                if (line == null || !line.enabled)
                {
                    continue;
                }

                SkillTreeEdge edge = edges[i];
                if (!TryGetNode(edge.From, out SkillTreeNodeView fromNode)
                    || !TryGetNode(edge.To, out SkillTreeNodeView toNode))
                {
                    continue;
                }

                LayoutConnectionLine(line.rectTransform, fromNode.RectTransform, toNode.RectTransform);
            }
        }

        private static bool IsNodeVisible(SkillTreeNodeId nodeId, Func<SkillTreeNodeId, int> getLevel)
        {
            if (getLevel == null)
            {
                return false;
            }

            if (getLevel(nodeId) > 0)
            {
                return true;
            }

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
                if (getLevel(prerequisites[i]) <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void LayoutConnectionLine(
            RectTransform line,
            RectTransform from,
            RectTransform to)
        {
            if (line == null || from == null || to == null)
            {
                return;
            }

            Vector2 fromPos = from.anchoredPosition;
            Vector2 toPos = to.anchoredPosition;
            Vector2 delta = toPos - fromPos;
            float length = delta.magnitude;
            if (length < 1f)
            {
                line.gameObject.SetActive(false);
                return;
            }

            line.gameObject.SetActive(true);
            line.anchorMin = new Vector2(0.5f, 0.5f);
            line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = (fromPos + toPos) * 0.5f;
            line.sizeDelta = new Vector2(length, connectionThickness);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            line.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private bool TryGetNode(SkillTreeNodeId id, out SkillTreeNodeView node)
        {
            if (nodeMap != null && nodeMap.TryGetValue(id, out node) && node != null)
            {
                return true;
            }

            node = null;
            return false;
        }

        private void RebuildNodeMap()
        {
            if (nodeViews == null)
            {
                nodeMap = new Dictionary<SkillTreeNodeId, SkillTreeNodeView>();
                return;
            }

            nodeMap = new Dictionary<SkillTreeNodeId, SkillTreeNodeView>(nodeViews.Length);
            for (int i = 0; i < nodeViews.Length; i++)
            {
                SkillTreeNodeView node = nodeViews[i];
                if (node == null)
                {
                    continue;
                }

                nodeMap[node.NodeId] = node;
            }
        }

        private void InitializeBonusSummaryToggle()
        {
            if (bonusSummaryToggle == null || bonusSummaryCanvas == null)
            {
                return;
            }

            bonusSummaryToggle.onValueChanged.AddListener(OnBonusSummaryToggleChanged);
            // 起動時はスキルツリー非表示なので一覧も閉じる
            bonusSummaryCanvas.enabled = false;
        }

        private void OnBonusSummaryToggleChanged(bool isOn)
        {
            if (bonusSummaryCanvas == null)
            {
                return;
            }

            // 本体が閉じている間はトグルONでも出さない
            bonusSummaryCanvas.enabled = isOn && canvas != null && canvas.enabled;
        }

        private void ApplyBonusSummaryCanvasVisibility()
        {
            if (bonusSummaryCanvas == null)
            {
                return;
            }

            bool toggleOn = bonusSummaryToggle == null || bonusSummaryToggle.isOn;
            bonusSummaryCanvas.enabled = toggleOn;
        }

        private void ValidateReferences()
        {
            if (canvas == null)
            {
                Debug.LogError("[SkillTreeView] canvasが未配線です", this);
            }

            if (nodeViews == null || nodeViews.Length == 0)
            {
                Debug.LogError("[SkillTreeView] nodeViewsが未配線です", this);
            }

            if (connectionLines == null || connectionLines.Length == 0)
            {
                Debug.LogError(
                    "[SkillTreeView] connectionLinesが未配線です。前提接続線をTreeAreaへ配置してください",
                    this);
            }

            if (bonusSummaryCanvas == null)
            {
                Debug.LogError("[SkillTreeView] bonusSummaryCanvasが未配線です", this);
            }

            if (bonusSummaryToggle == null)
            {
                Debug.LogError("[SkillTreeView] bonusSummaryToggleが未配線です", this);
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
