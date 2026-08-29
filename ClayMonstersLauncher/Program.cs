using System.Diagnostics;
using System.Text;

namespace ClayMonstersLauncher;

/// <summary>
/// Steam起動用の極小ランチャー
/// 本編またはデスクトップペットが動いている間プロセスを維持しプレイ時間を継続する
/// </summary>
internal static class Program
{
    private const string DefaultGameFileName = "ClayMonsters.exe";
    private const string DefaultGameProcessName = "ClayMonsters";
    private const string DefaultPetProcessName = "ClayMonstersPet";
    private const string MutexName = "Local\\ClayMonstersLauncher.Singleton";
    private const int DefaultGraceMilliseconds = 8000;
    private const int PollMilliseconds = 500;
    // 本編終了からペット起動までの短い隙間だけマーカーでつなぐ
    private const double KeepAliveMarkerMaxAgeSeconds = 45;

    [STAThread]
    private static int Main(string[] args)
    {
        string? gamePath = null;
        string gameProcessName = DefaultGameProcessName;
        string petProcessName = DefaultPetProcessName;
        int graceMilliseconds = DefaultGraceMilliseconds;
        List<string> forwardedArgs = new();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--game" && i + 1 < args.Length)
            {
                gamePath = args[++i].Trim('"');
            }
            else if (arg == "--game-process" && i + 1 < args.Length)
            {
                gameProcessName = args[++i].Trim('"');
            }
            else if (arg == "--pet-process" && i + 1 < args.Length)
            {
                petProcessName = args[++i].Trim('"');
            }
            else if (arg == "--grace-ms" && i + 1 < args.Length
                     && int.TryParse(args[++i], out int parsedGrace))
            {
                graceMilliseconds = Math.Clamp(parsedGrace, 1000, 60000);
            }
            else if (arg == "--help" || arg == "-h" || arg == "/?")
            {
                ShowHelp();
                return 0;
            }
            else
            {
                forwardedArgs.Add(arg);
            }
        }

        gamePath ??= ResolveDefaultGamePath();
        if (string.IsNullOrEmpty(gamePath) || !File.Exists(gamePath))
        {
            MessageBoxFallback(
                "ClayMonsters.exe が見つかりません。\n"
                + "Steamの起動オプションを ClayMonstersLauncher.exe にし、\n"
                + "同じフォルダに ClayMonsters.exe を置いてください。");
            return 1;
        }

        using Mutex mutex = new(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // 既存ランチャー維持中の再Play: 本編だけ起こして終了する
            if (!IsProcessRunning(gameProcessName) && !IsProcessRunning(petProcessName))
            {
                try
                {
                    StartGame(gamePath, forwardedArgs);
                }
                catch (Exception exception)
                {
                    MessageBoxFallback("ゲームの起動に失敗しました。\n" + exception.Message);
                    return 1;
                }
            }

            return 0;
        }

        try
        {
            StartGame(gamePath, forwardedArgs);
        }
        catch (Exception exception)
        {
            MessageBoxFallback("ゲームの起動に失敗しました。\n" + exception.Message);
            return 1;
        }

        return RunKeepAliveLoop(gameProcessName, petProcessName, graceMilliseconds);
    }

    private static int RunKeepAliveLoop(
        string gameProcessName,
        string petProcessName,
        int graceMilliseconds)
    {
        // 本編終了直後のペット引き継ぎやペットからの再起動に耐える
        DateTime? bothDeadSinceUtc = null;

        while (true)
        {
            bool gameAlive = IsProcessRunning(gameProcessName);
            bool petAlive = IsProcessRunning(petProcessName);
            bool keepAliveMarker = HasFreshKeepAliveMarker();

            if (gameAlive || petAlive || keepAliveMarker)
            {
                bothDeadSinceUtc = null;
                Thread.Sleep(PollMilliseconds);
                continue;
            }

            if (bothDeadSinceUtc == null)
            {
                bothDeadSinceUtc = DateTime.UtcNow;
            }

            double deadMs = (DateTime.UtcNow - bothDeadSinceUtc.Value).TotalMilliseconds;
            if (deadMs >= graceMilliseconds)
            {
                ClearKeepAliveMarker();
                return 0;
            }

            Thread.Sleep(PollMilliseconds);
        }
    }

    private static void StartGame(string gamePath, List<string> forwardedArgs)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = gamePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(gamePath) ?? Environment.CurrentDirectory
        };

        if (forwardedArgs.Count > 0)
        {
            startInfo.Arguments = string.Join(" ", forwardedArgs.ConvertAll(QuoteIfNeeded));
        }

        Process? process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Process.Start が null を返しました");
        }
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            Process[] processes = Process.GetProcessesByName(processName);
            try
            {
                for (int i = 0; i < processes.Length; i++)
                {
                    if (!processes[i].HasExited)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                for (int i = 0; i < processes.Length; i++)
                {
                    processes[i].Dispose();
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveDefaultGamePath()
    {
        string baseDir = AppContext.BaseDirectory;
        string beside = Path.Combine(baseDir, DefaultGameFileName);
        if (File.Exists(beside))
        {
            return beside;
        }

        // 開発時: リポジトリ直下の一般的なPlayerビルド先を探す
        string[] candidates =
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Build", "Steam", DefaultGameFileName)),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Builds", "Windows", DefaultGameFileName))
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    private static bool HasFreshKeepAliveMarker()
    {
        string? path = TryFindKeepAliveMarkerPath();
        if (path == null || !File.Exists(path))
        {
            return false;
        }

        try
        {
            string text = File.ReadAllText(path, Encoding.UTF8).Trim();
            if (!long.TryParse(text, out long ticks))
            {
                return false;
            }

            DateTime writtenUtc = new(ticks, DateTimeKind.Utc);
            return (DateTime.UtcNow - writtenUtc).TotalSeconds < KeepAliveMarkerMaxAgeSeconds;
        }
        catch
        {
            return File.Exists(path);
        }
    }

    private static void ClearKeepAliveMarker()
    {
        string? path = TryFindKeepAliveMarkerPath();
        if (path == null || !File.Exists(path))
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

    private static string? TryFindKeepAliveMarkerPath()
    {
        // Unityの persistentDataPath 相当:
        // %UserProfile%\AppData\LocalLow\<Company>\<Product>\DesktopPetCache\launcher_keepalive.txt
        string localLow = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow");
        if (!Directory.Exists(localLow))
        {
            return null;
        }

        const string markerRelative = "DesktopPetCache\\launcher_keepalive.txt";
        try
        {
            foreach (string companyDir in Directory.GetDirectories(localLow))
            {
                foreach (string productDir in Directory.GetDirectories(companyDir))
                {
                    string marker = Path.Combine(productDir, markerRelative);
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

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (value.Contains(' ') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        return value;
    }

    private static void ShowHelp()
    {
        MessageBoxFallback(
            "ClayMonstersLauncher\n"
            + "Steam起動exeとして使い本編/ペット稼働中はプロセスを維持します。\n\n"
            + "--game <path>           ClayMonsters.exe のパス\n"
            + "--game-process <name>   本編プロセス名(拡張子なし)\n"
            + "--pet-process <name>    ペットプロセス名(拡張子なし)\n"
            + "--grace-ms <int>        両方終了後の猶予(ミリ秒)");
    }

    private static void MessageBoxFallback(string message)
    {
        try
        {
            // User32 MessageBoxW でForms依存を避ける
            MessageBoxW(IntPtr.Zero, message, "ClayMonstersLauncher", 0x00000010);
        }
        catch
        {
            Console.Error.WriteLine(message);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
