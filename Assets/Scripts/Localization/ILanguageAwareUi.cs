using System;

namespace Localization
{
    /// <summary>
    /// 言語切替時にUI文言を再適用する対象
    /// </summary>
    public interface ILanguageAwareUi
    {
        /// <summary>
        /// 現在言語の文言を再適用する
        /// </summary>
        void RefreshLocalizedUi();
    }
}
