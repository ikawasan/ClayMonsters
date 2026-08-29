using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Extensions
{
    /// <summary>
    /// アプリケーション終了中かどうかを共有し終了時クリーンアップと強制終了フォールバックを行う
    /// </summary>
    public static class ApplicationQuitGuard
    {
        // Quit後に残留した場合の待ち時間
        private const int ForceExitDelayMilliseconds = 1200;
        // 外部taskkill用 本体より長めに待つ
        private const int ExternalWatchdogDelaySeconds = 3;

        private static int forceExitScheduled;
        private static int cleanupDone;
        private static int desktopPetHandoffActive;

        /// <summary>
        /// 終了処理中ならtrue
        /// </summary>
        public static bool IsQuitting { get; private set; }

        /// <summary>
        /// 終了直前に一度だけ呼ばれるクリーンアップ
        /// NetworkManager停止などを購読側で行う
        /// </summary>
        public static event Action ExitCleanup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetQuitFlag()
        {
            IsQuitting = false;
            forceExitScheduled = 0;
            cleanupDone = 0;
            desktopPetHandoffActive = 0;
            ExitCleanup = null;
            HookApplicationEvents();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureApplicationHooks()
        {
            // ドメインリロード経路差でもフックを必ず張る
            HookApplicationEvents();
        }

        private static void HookApplicationEvents()
        {
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
            Application.wantsToQuit -= OnWantsToQuit;
            Application.wantsToQuit += OnWantsToQuit;
        }

        /// <summary>
        /// ゲーム終了を要求する
        /// クリーンアップ後にApplication.QuitしROMでは残留プロセスを強制終了する
        /// </summary>
        public static void RequestQuit()
        {
            PerformExitCleanup();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            ScheduleStandaloneForceExit();
            Application.Quit();
#endif
        }

        /// <summary>
        /// 外部ペット引き継ぎ中の入力ロックを開始する
        /// </summary>
        public static void BeginDesktopPetHandoff()
        {
            Interlocked.Exchange(ref desktopPetHandoffActive, 1);
            IsQuitting = true;
        }

        /// <summary>
        /// 外部ペット引き継ぎ失敗時に入力ロックを戻す
        /// </summary>
        public static void CancelDesktopPetHandoff()
        {
            if (Interlocked.CompareExchange(ref desktopPetHandoffActive, 0, 1) != 1)
            {
                return;
            }

            IsQuitting = false;
        }

        /// <summary>
        /// 外部プロセスへ引き継いだあとUnityを即終了する
        /// </summary>
        public static void RequestImmediateQuit()
        {
            Interlocked.Exchange(ref desktopPetHandoffActive, 0);
            IsQuitting = true;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // 引き継ぎ済みなのでクリーンアップ待ちせずプロセスを殺す
            TerminateCurrentProcess();
#endif
        }

        private static bool OnWantsToQuit()
        {
            PerformExitCleanup();
#if !UNITY_EDITOR
            ScheduleStandaloneForceExit();
#endif
            return true;
        }

        private static void OnApplicationQuitting()
        {
            PerformExitCleanup();
#if !UNITY_EDITOR
            ScheduleStandaloneForceExit();
#endif
        }

        private static void PerformExitCleanup()
        {
            if (Interlocked.Exchange(ref cleanupDone, 1) == 1)
            {
                return;
            }

            IsQuitting = true;

            try
            {
                ExitCleanup?.Invoke();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"[ApplicationQuitGuard] ExitCleanup failed: {exception.Message}");
            }
        }

        private static void ScheduleStandaloneForceExit()
        {
            if (Interlocked.Exchange(ref forceExitScheduled, 1) == 1)
            {
                return;
            }

            // 本体が落ちきれなくても外部タスクがPIDを殺せるように先に仕掛ける
            TryStartExternalWatchdog();

            // フォアグラウンドにしプロセスがネイティブスレッドで生き残ってもKillまで走り切る
            Thread forceExitThread = new Thread(ForceExitWorker)
            {
                IsBackground = false,
                Name = "ClayMonsters.ForceExit"
            };
            forceExitThread.Start();
        }

        private static void TryStartExternalWatchdog()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                int pid = Process.GetCurrentProcess().Id;
                // /Tは使わないデスクトップペットを子として巻き込むため
                string arguments =
                    $"/c timeout /t {ExternalWatchdogDelaySeconds} /nobreak >nul "
                    + $"& taskkill /F /PID {pid} >nul 2>&1";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(startInfo);
            }
            catch
            {
                // 失敗しても内部Killで代替する
            }
#endif
        }

        private static void ForceExitWorker()
        {
            try
            {
                Thread.Sleep(ForceExitDelayMilliseconds);
                TerminateCurrentProcess();
            }
            catch
            {
                // 既に終了済みの場合は無視する
            }
        }

        private static void TerminateCurrentProcess()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                TerminateProcess(GetCurrentProcess(), 0);
            }
            catch
            {
                // fallthrough
            }
#endif

            try
            {
                Process.GetCurrentProcess().Kill();
            }
            catch
            {
                // fallthrough
            }

            try
            {
                Environment.Exit(0);
            }
            catch
            {
                // ignore
            }
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
#endif
    }
}
