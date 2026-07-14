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
    /// 育成HUDの表示と行き先選択入力
    /// </summary>
    public interface ITrainingHudView
    {
        /// <summary>
        /// 育成HUDを表示する
        /// </summary>
        void Show();

        /// <summary>
        /// オーバーレイ表示用にHUDルートだけ有効化する
        /// 方式選択や再開確認など子ウィンドウ表示時に使う
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
        /// <param name="period">現在の時間割</param>
        /// <param name="turnNumber">当日のターン番号(1始まり)</param>
        void BindSession(TrainingSession session, TrainingPeriod period, int turnNumber);

        /// <summary>
        /// 行き先候補を表示する
        /// </summary>
        /// <param name="choices">候補</param>
        /// <param name="currentStamina">現在体力</param>
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
        /// 行き先が選ばれるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask<TrainingTurnChoice> WaitTurnChoiceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 続行ボタンが押されるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask WaitContinueAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 攻撃入れ替え候補を表示する
        /// </summary>
        /// <param name="newAttack">習得した攻撃</param>
        /// <param name="currentAttacks">現在の攻撃</param>
        void ShowAttackSwapChoices(MotionType newAttack, IReadOnlyList<MotionType> currentAttacks);

        /// <summary>
        /// 攻撃入れ替え候補を隠す
        /// </summary>
        void HideAttackSwapChoices();

        /// <summary>
        /// 入れ替える攻撃スロットが選ばれるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>選択スロット(0〜3) 入れ替えない場合は-1</returns>
        UniTask<int> WaitAttackSwapChoiceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// タイトルへ戻るボタン押下を購読する
        /// </summary>
        /// <param name="action">コールバック</param>
        IDisposable SubscribeBackToTitleClick(UnityAction action);

        /// <summary>
        /// 育成中断ボタンの表示を切り替える
        /// </summary>
        /// <param name="visible">表示するか</param>
        void SetInterruptButtonVisible(bool visible);

        /// <summary>
        /// 育成中断ボタン押下を購読する
        /// </summary>
        /// <param name="action">コールバック</param>
        IDisposable SubscribeInterruptClick(UnityAction action);

        /// <summary>
        /// 育成再開か最初からかの選択肢を表示する
        /// </summary>
        /// <param name="presentation">表示データ</param>
        void ShowResumeChoices(TrainingResumeProgressPresentation presentation);

        /// <summary>
        /// 育成再開選択肢を隠す
        /// </summary>
        void HideResumeChoices();

        /// <summary>
        /// 育成再開か最初からかが選ばれるまで待機する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>続きからならtrue</returns>
        UniTask<bool> WaitResumeChoiceAsync(CancellationToken cancellationToken);
    }
}
