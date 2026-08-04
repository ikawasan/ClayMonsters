using System;
using UnityEngine.Events;

namespace UI.Option.Interface
{
    /// <summary>
    /// ゲーム全体で常駐するオプション画面のView
    /// </summary>
    public interface IOptionView
    {
        /// <summary>
        /// オプションUIを表示する
        /// </summary>
        void Show();

        /// <summary>
        /// オプションUIを非表示にする
        /// </summary>
        void Hide();

        /// <summary>
        /// 映像設定をUIへ反映する
        /// </summary>
        /// <param name="isFullScreen">フルスクリーン</param>
        /// <param name="isVSync">垂直同期</param>
        void InitVideoSettings(bool isFullScreen, bool isVSync);

        /// <summary>
        /// 音声設定をUIへ反映する
        /// </summary>
        /// <param name="musicVolume">BGM音量</param>
        /// <param name="soundEffectVolume">効果音音量</param>
        void InitSoundSettings(float musicVolume, float soundEffectVolume);

        /// <summary>
        /// 言語設定をUIへ反映する
        /// </summary>
        /// <param name="languageDisplayName">言語表示名</param>
        void InitLanguageSetting(string languageDisplayName);

        /// <summary>
        /// 静的ラベル文言を反映する
        /// </summary>
        /// <param name="title">タイトル</param>
        /// <param name="videoTitle">ビデオ見出し</param>
        /// <param name="audioTitle">オーディオ見出し</param>
        /// <param name="languageTitle">言語見出し</param>
        /// <param name="fullScreen">フルスクリーン</param>
        /// <param name="vSync">垂直同期</param>
        /// <param name="music">BGM</param>
        /// <param name="soundEffect">効果音</param>
        /// <param name="close">閉じる</param>
        void ApplyLocalizedLabels(
            string title,
            string videoTitle,
            string audioTitle,
            string languageTitle,
            string fullScreen,
            string vSync,
            string music,
            string soundEffect,
            string close);

        /// <summary>
        /// シーン原文を採取したあと現在言語でラベルを適用する
        /// </summary>
        void ApplyCapturedLocalizedLabels();

        /// <summary>
        /// 閉じるボタン押下を購読する
        /// </summary>
        /// <param name="action">コールバック</param>
        /// <returns>購読解除</returns>
        IDisposable SubscribeCloseButtonClick(UnityAction action);

        /// <summary>
        /// フルスクリーン変更を購読する
        /// </summary>
        /// <param name="action">コールバック</param>
        void SubscribeFullScreenChanged(UnityAction<bool> action);

        /// <summary>
        /// 垂直同期変更を購読する
        /// </summary>
        /// <param name="action">コールバック</param>
        void SubscribeVSyncChanged(UnityAction<bool> action);

        /// <summary>
        /// BGM音量変更を購読する
        /// </summary>
        /// <param name="action">コールバック</param>
        void SubscribeMusicVolumeChanged(UnityAction<float> action);

        /// <summary>
        /// 効果音音量変更を購読する
        /// </summary>
        /// <param name="action">コールバック</param>
        void SubscribeSoundEffectVolumeChanged(UnityAction<float> action);

        /// <summary>
        /// 前の言語ボタン押下を購読する
        /// </summary>
        /// <param name="action">コールバック</param>
        /// <returns>購読解除</returns>
        IDisposable SubscribeLanguagePrevButtonClick(UnityAction action);

        /// <summary>
        /// 次の言語ボタン押下を購読する
        /// </summary>
        /// <param name="action">コールバック</param>
        /// <returns>購読解除</returns>
        IDisposable SubscribeLanguageNextButtonClick(UnityAction action);
    }
}
