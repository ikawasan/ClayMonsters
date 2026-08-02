namespace Localization
{
    /// <summary>
    /// 言語設定の永続化を抽象化する
    /// SaveDataアセンブリへの直接依存を避ける
    /// </summary>
    public interface ILanguageOptionStore
    {
        /// <summary>
        /// 保存済み言語コードを返す未設定時は空文字
        /// </summary>
        /// <returns>言語コード</returns>
        string LoadLanguageCode();

        /// <summary>
        /// 言語コードを保存する
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        void SaveLanguageCode(string languageCode);
    }
}
