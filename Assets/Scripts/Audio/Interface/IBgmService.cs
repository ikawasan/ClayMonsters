using Audio;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Audio.Interface
{
    /// <summary>
    /// BGM再生を提供する
    /// </summary>
    public interface IBgmService
    {
        /// <summary>
        /// BGMが再生中か
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// 再生中のBGMトラックを取得する
        /// </summary>
        /// <param name="trackId">再生中トラック</param>
        /// <returns>再生中ならtrue</returns>
        bool TryGetCurrentTrack(out BgmTrackId trackId);

        /// <summary>
        /// 指定トラックを再生する
        /// 同一トラック再生中は何もしない
        /// </summary>
        /// <param name="trackId">再生するBGM</param>
        void Play(BgmTrackId trackId);

        /// <summary>
        /// 指定トラックをクロスフェード再生し完了を待つ
        /// </summary>
        /// <param name="trackId">再生するBGM</param>
        /// <param name="forceSwitch">trueなら同一トラックでも切替える</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask PlayAsync(
            BgmTrackId trackId,
            bool forceSwitch = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BGMをフェードアウトして停止し完了を待つ
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        UniTask FadeOutAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// BGMを即時停止する
        /// </summary>
        void Stop();

        /// <summary>
        /// BGM音量を設定する
        /// </summary>
        /// <param name="volume">0〜1</param>
        void SetMusicVolume(float volume);
    }
}
