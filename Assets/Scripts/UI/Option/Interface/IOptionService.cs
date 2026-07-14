namespace UI.Option.Interface
{
    /// <summary>
    /// オプション設定の取得と更新を行う
    /// </summary>
    public interface IOptionService
    {
        /// <summary>
        /// フルスクリーン設定を返す
        /// </summary>
        bool GetFullScreen { get; }

        /// <summary>
        /// 垂直同期設定を返す
        /// </summary>
        bool GetVSync { get; }

        /// <summary>
        /// BGM音量を返す
        /// </summary>
        float GetMusicVolume { get; }

        /// <summary>
        /// 効果音音量を返す
        /// </summary>
        float GetSoundEffectVolume { get; }

        /// <summary>
        /// フルスクリーン設定を更新する
        /// </summary>
        /// <param name="isFullScreen">フルスクリーン</param>
        void SetFullScreen(bool isFullScreen);

        /// <summary>
        /// 垂直同期設定を更新する
        /// </summary>
        /// <param name="isVSync">垂直同期</param>
        void SetVSync(bool isVSync);

        /// <summary>
        /// BGM音量を更新する
        /// </summary>
        /// <param name="volume">音量</param>
        void SetMusicVolume(float volume);

        /// <summary>
        /// 効果音音量を更新する
        /// </summary>
        /// <param name="volume">音量</param>
        void SetSoundEffectVolume(float volume);
    }
}
