using UnityEngine;

namespace Extensions
{
    /// <summary>
    /// アプリケーション終了中かどうかを共有する
    /// </summary>
    public static class ApplicationQuitGuard
    {
        /// <summary>
        /// 終了処理中ならtrue
        /// </summary>
        public static bool IsQuitting { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetQuitFlag()
        {
            IsQuitting = false;
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            IsQuitting = true;
        }
    }
}
