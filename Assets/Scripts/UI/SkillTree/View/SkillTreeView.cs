using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Localization;
using SaveData;
using System;
using System.Collections.Generic;
using System.Threading;
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
    public sealed class SkillTreeView : MonoBehaviour, ISkillTreeView, ILanguageAwareUi
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

        private static readonly Color ConnectionUnlockFlashColor = new(1.35f, 1.05f, 0.25f, 1f);
        private const float ConnectionFlashDurationSeconds = 0.7f;
        private const float ConnectionFlashThicknessMul = 2.6f;
        private const float RevealStaggerSeconds = 0.07f;

        private UnityAction<SkillTreeNodeId> nodeSelectedAction;
        private Dictionary<SkillTreeNodeId, SkillTreeNodeView> nodeMap;
        private LocalizedBakedTextApplier bakedLabelApplier;
        private int cachedPoints;
        private CancellationTokenSource connectionFlashCts;
        private readonly Dictionary<Image, Color> connectionBaseColors = new();
        private readonly Dictionary<Image, Vector2> connectionBaseSizes = new();

        private void Awake()
        {
            ValidateReferences();
            RebuildNodeMap();
            EnsureConnectionsBehindNodes();
            ApplyGridLayout();
            InitializeBonusSummaryToggle();
            EnsureBakedLabels();
            Hide();
        }

        private void OnDisable()
        {
            CancelConnectionFlash(restore: true);
        }

        private void OnDestroy()
        {
            CancelConnectionFlash(restore: false);
        }

        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyChromeLabels();
            SetPoints(cachedPoints);
        }

        private void EnsureBakedLabels()
        {
            if (bakedLabelApplier != null)
            {
                return;
            }

            bakedLabelApplier = new LocalizedBakedTextApplier();
            bakedLabelApplier.Register(GameTextKeys.SkillTreeTitle, "スキルツリー");
            bakedLabelApplier.Register(GameTextKeys.SkillTreeSkill, "スキル");
            bakedLabelApplier.Register(GameTextKeys.SkillTreeBonusEffects, "獲得効果");
            bakedLabelApplier.Register(GameTextKeys.SkillTreeBonusEffectsHeader, "【獲得効果】");
            bakedLabelApplier.Register(GameTextKeys.SkillTreeSelectNode, "ノードを選択してください");
            bakedLabelApplier.Register(GameTextKeys.SkillTreeUnlock, "解放");
            bakedLabelApplier.Register(GameTextKeys.CommonClose, "閉じる");
            bakedLabelApplier.Capture(transform);
        }

        private void ApplyChromeLabels()
        {
            EnsureBakedLabels();
            bakedLabelApplier.Apply();
            CaptureChromeOriginalsIfNeeded();
            LhButtonLabelUtility.SetLabel(
                closeButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, closeOriginal));
            if (unlockButtonLabel != null
                && string.IsNullOrEmpty(unlockButtonLabel.text))
            {
                LhButtonLabelUtility.SetLabel(
                    unlockButtonLabel,
                    SceneLocalizedLabel.Resolve(GameTextKeys.SkillTreeUnlock, unlockOriginal));
            }
        }

        private bool chromeOriginalsCaptured;
        private string closeOriginal = "閉じる";
        private string unlockOriginal = "解放";
        private string pointsTemplateOriginal = "{points} ポイント";

        private void CaptureChromeOriginalsIfNeeded()
        {
            if (chromeOriginalsCaptured)
            {
                return;
            }

            closeOriginal = SceneLocalizedLabel.Capture(closeButton, closeOriginal);
            unlockOriginal = SceneLocalizedLabel.Capture(unlockButtonLabel, unlockOriginal);
            if (pointsText != null && !string.IsNullOrWhiteSpace(pointsText.text))
            {
                // 9999 ポイントなどをテンプレートへ戻す
                string sample = pointsText.text.Trim();
                string template = System.Text.RegularExpressions.Regex.Replace(
                    sample,
                    @"\d+",
                    "{points}");
                if (template.Contains("{points}"))
                {
                    pointsTemplateOriginal = template;
                }
            }

            chromeOriginalsCaptured = true;
        }

        /// <inheritdoc />
        public void Show()
        {
            if (canvas == null)
            {
                return;
            }

            ApplyChromeLabels();
            ApplyGridLayout();
            if (panZoom != null)
            {
                panZoom.ResetView();
            }

            canvas.enabled = true;
            CloseBonusSummary();
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
            CloseBonusSummary();
        }

        /// <inheritdoc />
        public void SetPoints(int points)
        {
            if (pointsText == null)
            {
                return;
            }

            cachedPoints = Mathf.Max(0, points);
            CaptureChromeOriginalsIfNeeded();
            pointsText.text = SceneLocalizedLabel.Resolve(
                GameTextKeys.SkillTreePoints,
                pointsTemplateOriginal,
                "points",
                cachedPoints);
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
        public void PlayUnlockFeedback(
            SkillTreeNodeId unlockedNodeId,
            IReadOnlyList<SkillTreeNodeId> revealedNodeIds)
        {
            RebuildNodeMap();
            if (TryGetNode(unlockedNodeId, out SkillTreeNodeView unlockedNode))
            {
                unlockedNode.PlayUnlockAnimation();
            }

            PlayConnectionUnlockFlash(unlockedNodeId);

            if (revealedNodeIds == null || revealedNodeIds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < revealedNodeIds.Count; i++)
            {
                SkillTreeNodeId revealedId = revealedNodeIds[i];
                if (revealedId == unlockedNodeId)
                {
                    continue;
                }

                if (TryGetNode(revealedId, out SkillTreeNodeView revealedNode))
                {
                    float delay = 0.12f + (i * RevealStaggerSeconds);
                    revealedNode.PlayRevealAnimation(delay);
                }
            }
        }

        private void PlayConnectionUnlockFlash(SkillTreeNodeId unlockedNodeId)
        {
            if (connectionLines == null || connectionLines.Length == 0)
            {
                return;
            }

            CancelConnectionFlash(restore: true);
            connectionFlashCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            PlayConnectionUnlockFlashAsync(unlockedNodeId, connectionFlashCts.Token).Forget();
        }

        private async UniTaskVoid PlayConnectionUnlockFlashAsync(
            SkillTreeNodeId unlockedNodeId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<SkillTreeEdge> edges = SkillTreeCatalog.PrerequisiteEdges;
            int count = Mathf.Min(connectionLines.Length, edges.Count);
            List<Image> flashTargets = new();

            for (int i = 0; i < count; i++)
            {
                Image line = connectionLines[i];
                if (line == null || !line.enabled)
                {
                    continue;
                }

                SkillTreeEdge edge = edges[i];
                if (edge.From != unlockedNodeId && edge.To != unlockedNodeId)
                {
                    continue;
                }

                if (!connectionBaseColors.ContainsKey(line))
                {
                    connectionBaseColors[line] = line.color;
                }

                if (!connectionBaseSizes.ContainsKey(line))
                {
                    connectionBaseSizes[line] = line.rectTransform.sizeDelta;
                }

                flashTargets.Add(line);
            }

            if (flashTargets.Count == 0)
            {
                return;
            }

            try
            {
                float elapsed = 0f;
                while (elapsed < ConnectionFlashDurationSeconds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / ConnectionFlashDurationSeconds);
                    float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 5.5f));
                    float envelope = 1f - Mathf.Pow(Mathf.Clamp01((t - 0.55f) / 0.45f), 2f);
                    float strength = pulse * envelope;

                    for (int i = 0; i < flashTargets.Count; i++)
                    {
                        Image line = flashTargets[i];
                        if (line == null)
                        {
                            continue;
                        }

                        if (!connectionBaseColors.TryGetValue(line, out Color baseColor)
                            || !connectionBaseSizes.TryGetValue(line, out Vector2 baseSize))
                        {
                            continue;
                        }

                        line.color = Color.Lerp(baseColor, ConnectionUnlockFlashColor, strength);
                        float thickness = Mathf.Lerp(
                            baseSize.y,
                            baseSize.y * ConnectionFlashThicknessMul,
                            strength);
                        line.rectTransform.sizeDelta = new Vector2(baseSize.x, thickness);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                RestoreConnectionVisuals(flashTargets);
                if (connectionFlashCts != null)
                {
                    connectionFlashCts.Dispose();
                    connectionFlashCts = null;
                }
            }
            catch (OperationCanceledException)
            {
                // 中断時はCancelConnectionFlash側で戻す
            }
        }

        private void CancelConnectionFlash(bool restore)
        {
            if (connectionFlashCts != null)
            {
                connectionFlashCts.Cancel();
                connectionFlashCts.Dispose();
                connectionFlashCts = null;
            }

            if (restore)
            {
                List<Image> all = new();
                if (connectionLines != null)
                {
                    for (int i = 0; i < connectionLines.Length; i++)
                    {
                        if (connectionLines[i] != null)
                        {
                            all.Add(connectionLines[i]);
                        }
                    }
                }

                RestoreConnectionVisuals(all);
                return;
            }

            connectionBaseColors.Clear();
            connectionBaseSizes.Clear();
        }

        private void RestoreConnectionVisuals(List<Image> targets)
        {
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                Image line = targets[i];
                if (line == null)
                {
                    continue;
                }

                if (connectionBaseColors.TryGetValue(line, out Color color))
                {
                    line.color = color;
                }

                if (connectionBaseSizes.TryGetValue(line, out Vector2 size))
                {
                    line.rectTransform.sizeDelta = size;
                }
            }

            connectionBaseColors.Clear();
            connectionBaseSizes.Clear();
        }

        /// <inheritdoc />
        public void RefreshConnections(Func<SkillTreeNodeId, int> getLevel)
        {
            CancelConnectionFlash(restore: true);
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

            EnsureConnectionsBehindNodes();
        }

        /// <summary>
        /// 接続線をノードより背面に描画する
        /// Hierarchyで線が途中に挟まると一部アイコンより手前になるため補正する
        /// </summary>
        private void EnsureConnectionsBehindNodes()
        {
            if (connectionLines == null || connectionLines.Length == 0)
            {
                return;
            }

            // SetAsFirstSiblingは先頭へ移すため配列末尾から適用し順序を保つ
            for (int i = connectionLines.Length - 1; i >= 0; i--)
            {
                Image line = connectionLines[i];
                if (line == null)
                {
                    continue;
                }

                line.rectTransform.SetAsFirstSibling();
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

        private void ApplyGridLayout()
        {
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

                if (!SkillTreeLayoutCatalog.TryGetGrid(node.NodeId, out Vector2Int grid))
                {
                    Debug.LogError(
                        $"[SkillTreeView] マス配置が未定義です id={node.NodeId}",
                        node);
                    continue;
                }

                RectTransform rect = node.RectTransform;
                if (rect == null)
                {
                    continue;
                }

                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = SkillTreeLayoutCatalog.ToAnchoredPosition(grid);
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
            CloseBonusSummary();
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

        private void CloseBonusSummary()
        {
            if (bonusSummaryToggle != null)
            {
                bonusSummaryToggle.SetIsOnWithoutNotify(false);
            }

            if (bonusSummaryCanvas != null)
            {
                bonusSummaryCanvas.enabled = false;
            }
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
