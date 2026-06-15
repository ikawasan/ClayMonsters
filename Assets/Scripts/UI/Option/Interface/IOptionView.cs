using System;
using UnityEngine.Events;

namespace UI.Option.Interface
{
    public interface IOptionView
    {
        /// <summary>
        /// UI表示
        /// </summary>
        void Show();

        /// <summary>
        /// UI非表示
        /// </summary>
        void Hide();

        /// <summary>
        /// ビデオ関連のUIを初期化
        /// </summary>
        /// <param name="isFullScreen">フルスクリーン</param>
        /// <param name="brightness">明るさ</param>
        /// <param name="isVSync">同期フラグ</param>
        void InitVideoSettings(bool isFullScreen, float brightness, bool isVSync);

        /// <summary>
        /// サウンド関連のUIを初期化
        /// </summary>
        /// <param name="musicVolume">音楽音量</param>
        /// <param name="soundEffectVolume">効果音音量</param>
        void InitSoundSettings(float musicVolume, float soundEffectVolume);

        /// <summary>
        /// 非表示ボタンクリックイベントの購読
        /// </summary>
        /// <param name="action">アクション</param>
        /// <returns>イベントの購読を解除</returns>
        IDisposable SubscribeCloseButtonClick(Action action);

        /// <summary>
        /// フルスクリーン設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeFullScreenChanged(UnityAction<bool> action);

        /// <summary>
        /// 明るさ設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeBrightnessChanged(UnityAction<float> action);

        /// <summary>
        /// 同期設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeVSyncChanged(UnityAction<bool> action);


        /// <summary>
        /// 音楽設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeMusicVolumeChanged(UnityAction<float> action);

        /// <summary>
        /// 効果音設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeSoundEffectVolumeChanged(UnityAction<float> action);
    }
}
