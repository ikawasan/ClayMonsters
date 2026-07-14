using Audio.Interface;
using System;
using R3;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Extensions
{
    public static class ButtonRxExtensions
    {
        static readonly TimeSpan DefaultClickInterval = TimeSpan.FromMilliseconds(300);

        /// <summary>
        /// UI効果音サービスを設定する
        /// </summary>
        /// <param name="uiSoundService">再生サービス</param>
        public static void ConfigureUiSound(IUiSoundService uiSoundService)
        {
            ButtonUiSoundFeedback.Configure(uiSoundService);
        }

        /// <summary>
        /// ボタンにホバーと押下のUI効果音を付与する
        /// </summary>
        /// <param name="button">対象ボタン</param>
        public static void EnsureUiSoundFeedback(this Button button)
        {
            if (button == null)
            {
                return;
            }

            button.EnsurePressedVisualReset();

            if (button.GetComponent<ButtonUiSoundFeedback>() != null)
            {
                return;
            }

            button.gameObject.AddComponent<ButtonUiSoundFeedback>();
        }

        /// <summary>
        /// 押下中にポインターが外れたとき押下色が残る不具合を防ぐ
        /// </summary>
        /// <param name="button">対象ボタン</param>
        public static void EnsurePressedVisualReset(this Button button)
        {
            if (button == null || button.GetComponent<ButtonPressedVisualReset>() != null)
            {
                return;
            }

            button.gameObject.AddComponent<ButtonPressedVisualReset>();
        }

        public static IDisposable SubscribeOnClick(this Button button, UnityAction onClick)
        {
            button.EnsureUiSoundFeedback();

            return button.OnClickAsObservable()
                .Where(_ => button != null)
                .Where(_ => button.isActiveAndEnabled)
                .Where(_ => button.interactable)
                .ThrottleFirst(DefaultClickInterval)
                .Subscribe(_ => onClick())
                .AddTo(button);
        }
    }
}