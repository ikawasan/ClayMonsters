using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Extensions
{
    /// <summary>
    /// アプリケーション終了中かどうかを共有し終了時クリーンアップと強制終了フォールバックを行う
    /// </summary>
    public static class ApplicationQuitGuard
    {
        private const int ForceExitDelayMilliseconds = 2500;

        private static int forceExitScheduled;
        private static int cleanupDone;

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
            ExitCleanup = null;
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
            Application.Quit();
            ScheduleStandaloneForceExit();
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

            // Application.Quitが完了せずNetcode等のネイティブスレッドでプロセスが残る場合に備える
            Thread forceExitThread = new Thread(ForceExitWorker)
            {
                IsBackground = true,
                Name = "ClayMonsters.ForceExit"
            };
            forceExitThread.Start();
        }

        private static void ForceExitWorker()
        {
            try
            {
                Thread.Sleep(ForceExitDelayMilliseconds);
                Process.GetCurrentProcess().Kill();
            }
            catch
            {
                // 既に終了済みの場合は無視する
            }
        }
    }
}
