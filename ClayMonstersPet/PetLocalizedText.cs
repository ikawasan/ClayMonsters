using System.Text;

namespace ClayMonstersPet;

/// <summary>
/// UnityのUi TextTableからデスクトップペット文言を読む
/// </summary>
internal static class PetLocalizedText
{
    public const string QuitMenuKey = "DesktopPet.QuitMenu";
    public const string LaunchGameMenuKey = "DesktopPet.LaunchGameMenu";
    public const string CacheNotFoundKey = "DesktopPet.CacheNotFound";

    private static readonly Dictionary<string, string> table = new(StringComparer.Ordinal);
    private static string languageCode = "en";

    /// <summary>
    /// 言語とTextTablesディレクトリから文言を読み込む
    /// </summary>
    public static void Initialize(string? language, string? textTablesDirectory)
    {
        table.Clear();
        languageCode = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
        string? textDir = ResolveTextTablesDirectory(textTablesDirectory);
        if (string.IsNullOrEmpty(textDir))
        {
            return;
        }

        TryLoadFile(Path.Combine(textDir, "Ui." + languageCode + ".tsv"));
        if (!string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase))
        {
            // 欠損キーを英語で埋める
            TryLoadFile(Path.Combine(textDir, "Ui.en.tsv"), fillMissingOnly: true);
        }
    }

    public static string QuitMenu =>
        Get(QuitMenuKey, "デスクトップペットを終了");

    public static string LaunchGameMenu =>
        Get(LaunchGameMenuKey, "ClayMonstersを起動");

    public static string CacheNotFound =>
        Get(CacheNotFoundKey, "デスクトップペットのキャッシュが見つかりません。");

    private static string Get(string key, string fallback)
    {
        if (table.TryGetValue(key, out string? value) && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        return fallback;
    }

    private static void TryLoadFile(string path, bool fillMissingOnly = false)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('#'))
            {
                continue;
            }

            int tab = raw.IndexOf('\t');
            if (tab <= 0 || tab >= raw.Length - 1)
            {
                continue;
            }

            string key = raw.Substring(0, tab).Trim();
            string value = raw.Substring(tab + 1);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (fillMissingOnly && table.ContainsKey(key))
            {
                continue;
            }

            table[key] = value;
        }
    }

    private static string? ResolveTextTablesDirectory(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested) && Directory.Exists(requested))
        {
            return Path.GetFullPath(requested);
        }

        // StreamingAssets/ClayMonstersPet から見て隣の TextTables
        string sibling = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "TextTables"));
        if (Directory.Exists(sibling))
        {
            return sibling;
        }

        return null;
    }
}
