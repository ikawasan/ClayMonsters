using System;
using UnityEngine.Events;

namespace Scene.TitleScene.Interface
{
    /// <summary>
    /// CPU戦のモード選択とトーナメント難易度選択UI
    /// </summary>
    public interface ITitleNpcBattleMenuView
    {
        /// <summary>
        /// 必須UIが配線済みか
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// トーナメントとフリー対戦の選択を表示する
        /// </summary>
        void ShowModeSelect();

        /// <summary>
        /// トーナメント難易度選択を表示する
        /// </summary>
        void ShowDifficultySelect();

        /// <summary>
        /// モード選択と難易度選択を閉じる
        /// </summary>
        void Hide();

        /// <summary>
        /// トーナメントボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeTournamentButtonClick(UnityAction action);

        /// <summary>
        /// フリー対戦ボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeFreeBattleButtonClick(UnityAction action);

        /// <summary>
        /// モード選択の戻るボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeModeBackButtonClick(UnityAction action);

        /// <summary>
        /// イージーボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeEasyButtonClick(UnityAction action);

        /// <summary>
        /// ノーマルボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeNormalButtonClick(UnityAction action);

        /// <summary>
        /// ハードボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeHardButtonClick(UnityAction action);

        /// <summary>
        /// ベリーハードボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeVeryHardButtonClick(UnityAction action);

        /// <summary>
        /// 難易度選択の戻るボタン押下を購読する
        /// </summary>
        /// <param name="action">押下時の処理</param>
        /// <returns>購読解除用のDisposable</returns>
        IDisposable SubscribeDifficultyBackButtonClick(UnityAction action);
    }
}
