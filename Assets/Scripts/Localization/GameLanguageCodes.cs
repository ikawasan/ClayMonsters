namespace Localization
{
    /// <summary>
    /// 対応言語コードと既定値
    /// </summary>
    public static class GameLanguageCodes
    {
        public const string Japanese = "ja";
        public const string English = "en";
        public const string ChineseSimplified = "zh-Hans";
        public const string ChineseTraditional = "zh-Hant";
        public const string Korean = "ko";
        public const string German = "de";
        public const string French = "fr";
        public const string Spanish = "es";
        public const string PortugueseBrazil = "pt-BR";

        public const string Default = English;

        public static readonly string[] All =
        {
            Japanese,
            English,
            ChineseSimplified,
            ChineseTraditional,
            Korean,
            German,
            French,
            Spanish,
            PortugueseBrazil,
        };
    }
}
