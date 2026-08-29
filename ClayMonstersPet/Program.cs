using System.Text;

namespace ClayMonstersPet;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        List<string> cacheDirectories = new();
        string? gameExe = null;
        string? language = null;
        string? textTablesDirectory = null;
        bool stayOnTop = true;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--cache" && i + 1 < args.Length)
            {
                string cacheDirectory = args[++i].Trim('"');
                if (!string.IsNullOrWhiteSpace(cacheDirectory) && Directory.Exists(cacheDirectory))
                {
                    cacheDirectories.Add(cacheDirectory);
                }
            }
            else if (args[i] == "--game" && i + 1 < args.Length)
            {
                gameExe = args[++i].Trim('"');
            }
            else if (args[i] == "--lang" && i + 1 < args.Length)
            {
                language = args[++i].Trim('"');
            }
            else if (args[i] == "--text-dir" && i + 1 < args.Length)
            {
                textTablesDirectory = args[++i].Trim('"');
            }
            else if (args[i] == "--topmost" && i + 1 < args.Length)
            {
                stayOnTop = args[++i].Trim('"') != "0";
            }
        }

        PetWindowOrder.StayOnTop = stayOnTop;

        if (string.IsNullOrWhiteSpace(language))
        {
            language = TryReadSavedLanguage();
        }

        PetLocalizedText.Initialize(language, textTablesDirectory);

        if (cacheDirectories.Count == 0)
        {
            cacheDirectories.AddRange(TryReadActiveCacheDirectories());
        }

        if (cacheDirectories.Count == 0)
        {
            MessageBox.Show(
                PetLocalizedText.CacheNotFound,
                "ClayMonstersPet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        List<PetForm> forms = new(cacheDirectories.Count);
        for (int i = 0; i < cacheDirectories.Count; i++)
        {
            try
            {
                forms.Add(new PetForm(cacheDirectories[i], gameExe));
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    exception.Message,
                    "ClayMonstersPet",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        if (forms.Count == 0)
        {
            return;
        }

        // ペットプロセス自体が生存合図になるので引き継ぎマーカーは消す
        ClearLauncherKeepAliveMarker(cacheDirectories);

        PetGroupBrain brain = new(forms);

        ApplicationContext context = new ApplicationContext();
        int remaining = forms.Count;
        for (int i = 0; i < forms.Count; i++)
        {
            PetForm form = forms[i];
            form.FormClosed += (_, _) =>
            {
                remaining--;
                if (remaining <= 0)
                {
                    ClearLauncherKeepAliveMarker(cacheDirectories);
                    MarkPointSessionEnded(cacheDirectories);
                    brain.Dispose();
                    context.ExitThread();
                }
            };
            form.Show();
        }

        Application.ApplicationExit += (_, _) => ClearLauncherKeepAliveMarker(cacheDirectories);
        Application.Run(context);
    }

    /// <summary>
    /// Steamランチャー向け引き継ぎマーカーを消す
    /// </summary>
    private static void ClearLauncherKeepAliveMarker(IReadOnlyList<string> cacheDirectories)
    {
        string? path = ResolveLauncherKeepAlivePath(cacheDirectories);
        if (string.IsNullOrEmpty(path))
        {
            path = TryFindLauncherKeepAlivePath();
        }

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
            // 削除失敗は終了を妨げない
        }
    }

    private static string? ResolveLauncherKeepAlivePath(IReadOnlyList<string> cacheDirectories)
    {
        if (cacheDirectories == null || cacheDirectories.Count == 0)
        {
            return null;
        }

        string cacheDirectory = cacheDirectories[0]
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? root = Path.GetDirectoryName(cacheDirectory);
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        return Path.Combine(root, "launcher_keepalive.txt");
    }

    private static string? TryFindLauncherKeepAlivePath()
    {
        string localLow = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow");
        if (!Directory.Exists(localLow))
        {
            return null;
        }

        try
        {
            foreach (string companyDir in Directory.GetDirectories(localLow))
            {
                foreach (string productDir in Directory.GetDirectories(companyDir))
                {
                    string marker = Path.Combine(productDir, "DesktopPetCache", "launcher_keepalive.txt");
                    if (File.Exists(marker))
                    {
                        return marker;
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// 外部ペット終了時刻をセッションへ書き残す
    /// Unity側が次回起動時に時間精算する
    /// </summary>
    private static void MarkPointSessionEnded(IReadOnlyList<string> cacheDirectories)
    {
        string? sessionPath = ResolvePointSessionPath(cacheDirectories);
        if (string.IsNullOrEmpty(sessionPath) || !File.Exists(sessionPath))
        {
            return;
        }

        try
        {
            long lastSettledUtcTicks = 0;
            string[] lines = File.ReadAllLines(sessionPath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int separator = line.IndexOf('=');
                if (separator <= 0 || separator >= line.Length - 1)
                {
                    continue;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                if (string.Equals(key, "lastSettledUtcTicks", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(value, out long parsed))
                {
                    lastSettledUtcTicks = parsed;
                }
            }

            if (lastSettledUtcTicks <= 0)
            {
                return;
            }

            string content =
                "active=0\n"
                + "lastSettledUtcTicks="
                + lastSettledUtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\n"
                + "endedUtcTicks="
                + DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\n";
            File.WriteAllText(sessionPath, content, Encoding.UTF8);
        }
        catch
        {
            // 終了時の書込失敗は次回起動精算に委ねる
        }
    }

    private static string? ResolvePointSessionPath(IReadOnlyList<string> cacheDirectories)
    {
        if (cacheDirectories == null || cacheDirectories.Count == 0)
        {
            return null;
        }

        string cacheDirectory = cacheDirectories[0]
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? root = Path.GetDirectoryName(cacheDirectory);
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        return Path.Combine(root, "points_session.txt");
    }

    private static string? TryReadSavedLanguage()
    {
        string marker = TryFindLanguageMarkerPath();
        if (string.IsNullOrEmpty(marker) || !File.Exists(marker))
        {
            return null;
        }

        try
        {
            string[] lines = File.ReadAllLines(marker, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    return line;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? TryFindLanguageMarkerPath()
    {
        string localLow = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow");
        if (!Directory.Exists(localLow))
        {
            return null;
        }

        foreach (string companyDir in Directory.GetDirectories(localLow))
        {
            foreach (string productDir in Directory.GetDirectories(companyDir))
            {
                string marker = Path.Combine(productDir, "DesktopPetCache", "language.txt");
                if (File.Exists(marker))
                {
                    return marker;
                }
            }
        }

        return null;
    }

    private static List<string> TryReadActiveCacheDirectories()
    {
        List<string> directories = new();
        string localLow = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow");
        if (!Directory.Exists(localLow))
        {
            return directories;
        }

        foreach (string companyDir in Directory.GetDirectories(localLow))
        {
            foreach (string productDir in Directory.GetDirectories(companyDir))
            {
                string marker = Path.Combine(productDir, "DesktopPetCache", "active.txt");
                if (!File.Exists(marker))
                {
                    continue;
                }

                foreach (string raw in File.ReadAllLines(marker, Encoding.UTF8))
                {
                    string path = raw.Trim();
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !directories.Contains(path))
                    {
                        directories.Add(path);
                    }
                }

                if (directories.Count > 0)
                {
                    return directories;
                }
            }
        }

        return directories;
    }
}
