using System;
using System.Collections.Generic;
using UI.SkillTree.View;
using UnityEngine.Events;

namespace UI.SkillTree.Interface
{
    /// <summary>
    /// スキルツリー画面の表示契約
    /// </summary>
    public interface ISkillTreeView
    {
        /// <summary>
        /// 画面を表示する
        /// </summary>
        void Show();

        /// <summary>
        /// 画面を非表示にする
        /// </summary>
        void Hide();

        /// <summary>
        /// 所持ポイントを更新する
        /// </summary>
        /// <param name="points">ポイント</param>
        void SetPoints(int points);

        /// <summary>
        /// ノード一覧の表示を更新する
        /// </summary>
        /// <param name="bindNode">各ノードへ渡す更新処理</param>
        void RefreshNodes(Action<SkillTreeNodeView> bindNode);

        /// <summary>
        /// 前提接続線の表示を更新する
        /// </summary>
        /// <param name="getLevel">ノードレベル取得</param>
        void RefreshConnections(Func<SaveData.SkillTreeNodeId, int> getLevel);

        /// <summary>
        /// ノード解放時の演出を再生する
        /// </summary>
        /// <param name="unlockedNodeId">解放したノード</param>
        /// <param name="revealedNodeIds">新たに出現したノード</param>
        void PlayUnlockFeedback(
            SaveData.SkillTreeNodeId unlockedNodeId,
            IReadOnlyList<SaveData.SkillTreeNodeId> revealedNodeIds);

        /// <summary>
        /// 詳細パネルを更新する
        /// </summary>
        /// <param name="name">名前</param>
        /// <param name="description">説明</param>
        /// <param name="actionLabel">ボタン文言</param>
        /// <param name="statusLabel">補足文言</param>
        /// <param name="canUnlock">解放可能か</param>
        void SetDetail(
            string name,
            string description,
            string actionLabel,
            string statusLabel,
            bool canUnlock);

        /// <summary>
        /// 獲得効果一覧を更新する
        /// </summary>
        /// <param name="summaryText">一覧文言</param>
        void SetBonusSummary(string summaryText);

        /// <summary>
        /// 閉じるボタン購読
        /// </summary>
        /// <param name="action">押下時</param>
        IDisposable SubscribeCloseButtonClick(UnityAction action);

        /// <summary>
        /// 解放ボタン購読
        /// </summary>
        /// <param name="action">押下時</param>
        IDisposable SubscribeUnlockButtonClick(UnityAction action);

        /// <summary>
        /// ノード選択購読
        /// </summary>
        /// <param name="action">選択ノード</param>
        void SubscribeNodeSelected(UnityAction<SaveData.SkillTreeNodeId> action);
    }
}
