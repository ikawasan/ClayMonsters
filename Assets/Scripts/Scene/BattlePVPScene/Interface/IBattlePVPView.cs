using Scene.BattlePVPScene.Interface;
using Scene.BattlePVPScene;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Scene.BattlePVPScene.Interface
{
    /// <summary>
    /// BattlePVPシーンのマッチングと戻る操作を提供する
    /// </summary>
    public interface IBattlePVPView
    {
        /// <summary>
        /// タイトルへ戻るボタン押下を購読する
        /// </summary>
        IDisposable SubscribeReturnButtonClick(UnityAction action);

        /// <summary>
        /// 特定の相手と対戦ボタン押下を購読する
        /// </summary>
        IDisposable SubscribeDirectMatchButtonClick(UnityAction action);

        /// <summary>
        /// 不特定の相手と対戦ボタン押下を購読する
        /// </summary>
        IDisposable SubscribeRandomMatchButtonClick(UnityAction action);

        /// <summary>
        /// ルーム作成ボタン押下を購読する
        /// </summary>
        IDisposable SubscribeCreateRoomButtonClick(UnityAction action);

        /// <summary>
        /// ルーム参加ボタン押下を購読する
        /// </summary>
        IDisposable SubscribeJoinRoomButtonClick(UnityAction action);

        /// <summary>
        /// ランダムマッチ開始ボタン押下を購読する
        /// </summary>
        IDisposable SubscribeStartRandomMatchButtonClick(UnityAction action);

        /// <summary>
        /// マッチングキャンセルボタン押下を購読する
        /// </summary>
        IDisposable SubscribeCancelMatchButtonClick(UnityAction action);

        /// <summary>
        /// マッチング画面の戻るボタン押下を購読する
        /// </summary>
        IDisposable SubscribeMatchBackButtonClick(UnityAction action);

        /// <summary>
        /// 参加コードコピーボタン押下を購読する
        /// </summary>
        IDisposable SubscribeCopyJoinCodeButtonClick(UnityAction action);

        /// <summary>
        /// 参加コードコピーボタンの表示を切り替える
        /// </summary>
        void SetCopyJoinCodeButtonVisible(bool isVisible);

        /// <summary>
        /// 入力された参加コードを返す
        /// </summary>
        string GetJoinCodeInput();

        /// <summary>
        /// 表示パネルを切り替える
        /// </summary>
        void ShowPanel(BattlePvpUiPanel panel);

        /// <summary>
        /// ステータス文言を更新する
        /// </summary>
        void SetStatusText(string message);

        /// <summary>
        /// 参加コード表示を更新する
        /// </summary>
        void SetJoinCodeText(string joinCode);
    }
}
