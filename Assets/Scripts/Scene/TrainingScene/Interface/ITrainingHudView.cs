using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData;
using Scene.TrainingScene.Domain;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Events;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 育成HUDの表示と週次コマンド入力
    /// </summary>
    public interface ITrainingHudView
    {
        /// <summary>
        /// 育成HUDを表示する
        /// </summary>
        void Show();

        /// <summary>
        /// オーバーレイ表示用にHUDルートだけ有効化する
        /// </summary>
        void ShowOverlayHost();

        /// <summary>
        /// オーバーレイ上にメッセージだけ表示する
        /// </summary>
        /// <param name="message">メッセージ</param>
        void ShowOverlayMessage(string message);

        /// <summary>
        /// オーバーレイ用メッセージを消す
        /// </summary>
        void ClearOverlayMessage();

        /// <summary>
        /// モーダルウィンドウ表示用にCanvasだけ有効化する
        /// </summary>
        void ShowModalOverlayHost();

        /// <summary>
        /// 育成HUDを隠す
        /// </summary>
        void Hide();

        /// <summary>
        /// セッション状態を画面へ反映する
        /// </summary>
        /// <param name="session">育成セッション</param>
        void BindSession(TrainingSession session);

        /// <summary>
        /// 互換用のセッション反映
        /// </summary>
        void BindSession(TrainingSession session, TrainingPeriod period, int turnNumber);

        /// <summary>
        /// 週次コマンド候補を表示する
        /// </summary>
        /// <param name="commands">候補</param>
        /// <param name="currentStamina">現在体力</param>
        void ShowCommandChoices(
            IReadOnlyList<TrainingCommandType> commands,
            int currentStamina);

        /// <summary>
        /// 売店の商品候補を表示する
        /// </summary>
        /// <param name="items">表示する商品</param>
        /// <param name="hasNextPage">次ページがあるか</param>
        /// <param name="currentMoney">所持金</param>
        /// <param name="showOpenInventory">所持アイテムへ進むボタンを出すか</param>
        void ShowShopChoices(
            IReadOnlyList<TrainingShopItem> items,
            bool hasNextPage,
            int currentMoney,
            bool showOpenInventory);

        /// <summary>
        /// 所持アイテム候補を表示する
        /// </summary>
        /// <param name="entries">所持一覧</param>
        /// <param name="hasNextPage">次ページがあるか</param>
        void ShowInventoryChoices(
            IReadOnlyList<TrainingInventoryEntryView> entries,
            bool hasNextPage);

        /// <summary>
        /// 訓練主ステ候補を表示する
        /// </summary>
        /// <param name="command">訓練または特訓</param>
        /// <param name="focuses">今ターンの主ステ候補</param>
        void ShowFocusChoices(
            TrainingCommandType command,
            IReadOnlyList<TrainingFocus> focuses);

        /// <summary>
        /// 互換用の行き先候補表示
        /// </summary>
        void ShowLocationChoices(TrainingLocation[] choices, int currentStamina);

        /// <summary>
        /// 行き先ボタンを隠す
        /// </summary>
        void HideLocationChoices();

        /// <summary>
        /// ログ文言を表示する
        /// </summary>
        /// <param name="message">メッセージ</param>
        void SetLogMessage(string message);

        /// <summary>
        /// 週次コマンドが選ばれるまで待機する
        /// </summary>
        UniTask<TrainingCommandType> WaitCommandChoiceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 売店の選択が終わるまで待機する
        /// </summary>
        /// <returns>商品index / NextPage / OpenInventory / RefreshOffer / Back</returns>
        UniTask<int> WaitShopChoiceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 所持アイテムの選択が終わるまで待機する
        /// </summary>
        /// <returns>所持index / NextPage / Back</returns>
        UniTask<int> WaitInventoryChoiceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 主ステが選ばれるまで待機する
        /// </summary>
        /// <returns>選ばれた主ステ。戻る操作ならnull</returns>
        UniTask<TrainingFocus?> WaitFocusChoiceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 互換用の行き先選択待機
        /// </summary>
        UniTask<TrainingTurnChoice> WaitTurnChoiceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 続行ボタンが押されるまで待機する
        /// </summary>
        UniTask WaitContinueAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 攻撃入れ替え候補を表示する
        /// </summary>
        /// <param name="newAttack">習得する攻撃</param>
        /// <param name="currentAttacks">現在の攻撃一覧</param>
        /// <param name="showSessionPanels">所持金とステータスを表示するか</param>
        void ShowAttackSwapChoices(
            MotionType newAttack,
            IReadOnlyList<MotionType> currentAttacks,
            bool showSessionPanels = true);

        /// <summary>
        /// 攻撃入れ替え候補を隠す
        /// </summary>
        void HideAttackSwapChoices();

        /// <summary>
        /// 入れ替える攻撃スロットが選ばれるまで待機する
        /// </summary>
        UniTask<int> WaitAttackSwapChoiceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// タイトルへ戻るボタン押下を購読する
        /// </summary>
        IDisposable SubscribeBackToTitleClick(UnityAction action);

        /// <summary>
        /// 育成中断ボタンの表示を切り替える
        /// </summary>
        void SetInterruptButtonVisible(bool visible);

        /// <summary>
        /// 育成中断ボタン押下を購読する
        /// </summary>
        IDisposable SubscribeInterruptClick(UnityAction action);

        /// <summary>
        /// 育成再開か最初からかの選択肢を表示する
        /// </summary>
        void ShowResumeChoices(TrainingResumeProgressPresentation presentation);

        /// <summary>
        /// 育成再開選択肢を隠す
        /// </summary>
        void HideResumeChoices();

        /// <summary>
        /// 育成再開か最初からかが選ばれるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>再開選択結果</returns>
        UniTask<TrainingResumeChoice> WaitResumeChoiceAsync(CancellationToken cancellationToken);
    }
}
