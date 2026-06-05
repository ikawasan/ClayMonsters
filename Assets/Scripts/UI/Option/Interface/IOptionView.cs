using System;

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
        /// 非表示ボタンクリックイベントの購読
        /// </summary>
        /// <param name="action">アクション</param>
        /// <returns>イベントの購読を解除</returns>
        IDisposable SubscribeCloseButtonClick(Action action);

        /// <summary>
        /// ビデオ関連のUIを初期化
        /// </summary>
        /// <param name="resolutionMax">解像度最大値</param>
        /// <param name="resolutionCurrent">解像度</param>
        /// <param name="isFullScreen">フルスクリーン</param>
        /// <param name="brightness">明るさ</param>
        /// <param name="isVSync">同期フラグ</param>
        void InitVideoSettings(int resolutionMax, int resolutionCurrent, bool isFullScreen, float brightness, bool isVSync);

        /// <summary>
        /// サウンド関連のUIを初期化
        /// </summary>
        /// <param name="musicVolume">音楽音量</param>
        /// <param name="soundEffectVolume">効果音音量</param>
        void InitSoundSettings(float musicVolume, float soundEffectVolume);

        /// <summary>
        /// 解像度設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeResolutionChanged(Action<float> action);

        /// <summary>
        /// フルスクリーン設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeFullScreenChanged(Action<bool> action);

        /// <summary>
        /// 明るさ設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeBrightnessChanged(Action<float> action);

        /// <summary>
        /// 同期設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeVSyncChanged(Action<bool> action);


        /// <summary>
        /// 音楽設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeMusicVolumeChanged(Action<float> action);

        /// <summary>
        /// 効果音設定の変更イベントを購読
        /// </summary>
        /// <param name="action">アクション</param>
        void SubscribeSoundEffectVolumeChanged(Action<float> action);
    }
}
