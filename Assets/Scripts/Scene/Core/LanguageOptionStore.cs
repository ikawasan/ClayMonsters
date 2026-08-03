using Localization;
using SaveData;

namespace Scene.Core
{
    /// <summary>
    /// SaveDataへ言語コードを読み書きする
    /// </summary>
    public sealed class LanguageOptionStore : ILanguageOptionStore
    {
        /// <inheritdoc/>
        public string LoadLanguageCode()
        {
            SaveData.SaveData saveData = SaveDataManager.Load();
            if (saveData?.LanguageOptionData == null)
            {
                return string.Empty;
            }

            return saveData.LanguageOptionData.LanguageCode ?? string.Empty;
        }

        /// <inheritdoc/>
        public void SaveLanguageCode(string languageCode)
        {
            SaveDataManager.Update(data =>
            {
                data.LanguageOptionData ??= new LanguageOptionSaveData();
                data.LanguageOptionData.LanguageCode = languageCode ?? string.Empty;
            });
        }
    }
}
