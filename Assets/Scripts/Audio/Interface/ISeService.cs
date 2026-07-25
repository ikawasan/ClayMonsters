namespace Audio.Interface
{
    /// <summary>
    /// 戦闘SE再生を提供する
    /// </summary>
    public interface ISeService
    {
        /// <summary>
        /// 指定SEを再生する
        /// </summary>
        /// <param name="trackId">SEトラック</param>
        void Play(SeTrackId trackId);

        /// <summary>
        /// 指定SEを目標尺に合わせてピッチ調整して再生する
        /// </summary>
        /// <param name="trackId">SEトラック</param>
        /// <param name="durationSeconds">目標再生秒数</param>
        void PlayTimed(SeTrackId trackId, float durationSeconds);

        /// <summary>
        /// 指定SEの再生を停止する
        /// </summary>
        /// <param name="trackId">SEトラック</param>
        void Stop(SeTrackId trackId);

        /// <summary>
        /// 攻撃ヒットSEを再生する
        /// </summary>
        void PlayAttackHit();

        /// <summary>
        /// 攻撃ミスSEを再生する
        /// </summary>
        void PlayAttackMiss();

        /// <summary>
        /// パーツ破壊ととどめのSEを再生する
        /// </summary>
        void PlayPartsBreak();

        /// <summary>
        /// 効果音音量を設定する
        /// </summary>
        /// <param name="volume">0〜1</param>
        void SetSoundEffectVolume(float volume);
    }
}
