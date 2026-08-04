using System.Threading;
using Cysharp.Threading.Tasks;

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
        /// BGM音量を返す
        /// </summary>
        float GetMusicVolume { get; }

        /// <summary>
        /// 効果音音量を返す
        /// </summary>
        float GetSoundEffectVolume { get; }

        /// <summary>
        /// 現在の言語コードを返す
        /// </summary>
        string GetLanguageCode { get; }

        /// <summary>
        /// 現在の言語表示名を返す
        /// </summary>
        string GetLanguageDisplayName { get; }

        /// <summary>
        /// フルスクリーン設定を更新する
        /// </summary>
        /// <param name="isFullScreen">フルスクリーン</param>
        void SetFullScreen(bool isFullScreen);

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

        /// <summary>
        /// 言語を前後に切り替える
        /// </summary>
        /// <param name="delta">移動量</param>
        /// <param name="cancellationToken">取消トークン</param>
        UniTask CycleLanguageAsync(int delta, CancellationToken cancellationToken);
    }
}
