namespace Audio.Interface
{
    /// <summary>
    /// UI効果音再生を提供する
    /// </summary>
    public interface IUiSoundService
    {
        /// <summary>
        /// ボタンホバー音を再生する
        /// </summary>
        void PlayHover();

        /// <summary>
        /// ボタン押下音を再生する
        /// </summary>
        void PlayClick();

        /// <summary>
        /// 効果音音量を設定する
        /// </summary>
        /// <param name="volume">0〜1</param>
        void SetSoundEffectVolume(float volume);
    }
}
