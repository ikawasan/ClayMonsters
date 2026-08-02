using System.Threading;
using Cysharp.Threading.Tasks;

namespace Localization
{
    /// <summary>
    /// 起動時およびオプションからの言語適用
    /// </summary>
    public interface ILanguageInitializer
    {
        /// <summary>
        /// 保存言語またはOS言語で初期化する
        /// </summary>
        /// <param name="cancellationToken">取消トークン</param>
        UniTask InitializeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 指定言語へ切り替え保存する
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        /// <param name="cancellationToken">取消トークン</param>
        UniTask SetLanguageAsync(string languageCode, CancellationToken cancellationToken);

        /// <summary>
        /// 現在の言語コードを返す
        /// </summary>
        string CurrentLanguageCode { get; }
    }
}
