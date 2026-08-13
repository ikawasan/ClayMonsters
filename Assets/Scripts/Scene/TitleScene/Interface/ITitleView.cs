using System;
using UnityEngine.Events;

namespace Scene.TitleScene.Interface
{
    public interface ITitleView
    {
        /// <summary>
        /// ClayEditボタン押下を購読する
        /// </summary>
        IDisposable SubscribeClayEditButtonClick(UnityAction action);

        /// <summary>
        /// BattleNpcボタン押下を購読する
        /// </summary>
        IDisposable SubscribeBattleNpcButtonClick(UnityAction action);

        /// <summary>
        /// BattlePVPボタン押下を購読する
        /// </summary>
        IDisposable SubscribeBattlePvpButtonClick(UnityAction action);

        /// <summary>
        /// 育成モードボタン押下を購読する
        /// </summary>
        IDisposable SubscribeTrainingButtonClick(UnityAction action);

        /// <summary>
        /// スキルツリーボタン押下を購読する
        /// </summary>
        IDisposable SubscribeSkillTreeButtonClick(UnityAction action);

        /// <summary>
        /// 展示室ボタン押下を購読する
        /// </summary>
        IDisposable SubscribeModelGalleryButtonClick(UnityAction action);

        /// <summary>
        /// オプションボタン押下を購読する
        /// </summary>
        IDisposable SubscribeOptionButtonClick(UnityAction action);

        /// <summary>
        /// デスクトップペットボタン押下を購読する
        /// </summary>
        IDisposable SubscribeDesktopPetButtonClick(UnityAction action);

        /// <summary>
        /// ゲーム終了ボタン押下を購読する
        /// </summary>
        IDisposable SubscribeQuitGameButtonClick(UnityAction action);

        /// <summary>
        /// 右上のポイント表示を更新する
        /// </summary>
        /// <param name="points">現在ポイント</param>
        void SetPoints(int points);
    }
}
