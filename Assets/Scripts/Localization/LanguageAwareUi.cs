using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Localization
{
    /// <summary>
    /// 言語切替時に登録済みUIとILanguageAwareUiを再適用する
    /// </summary>
    public static class LanguageAwareUi
    {
        private static readonly List<Action> registered = new();
        private static readonly List<ILanguageAwareUi> awareComponents = new();
        private static readonly object Gate = new();
        private static bool sceneHooked;
        private static bool awareCacheDirty = true;

        /// <summary>
        /// 言語切替時に呼ぶコールバックを登録する
        /// </summary>
        /// <param name="onRefresh">再適用処理</param>
        /// <returns>解除用</returns>
        public static IDisposable Register(Action onRefresh)
        {
            EnsureSceneHook();
            if (onRefresh == null)
            {
                return EmptyDisposable.Instance;
            }

            lock (Gate)
            {
                registered.Add(onRefresh);
            }

            return new Registration(onRefresh);
        }

        /// <summary>
        /// ILanguageAwareUi実装を登録する
        /// </summary>
        /// <param name="aware">再適用対象</param>
        /// <returns>解除用</returns>
        public static IDisposable RegisterAware(ILanguageAwareUi aware)
        {
            EnsureSceneHook();
            if (aware == null)
            {
                return EmptyDisposable.Instance;
            }

            lock (Gate)
            {
                if (!awareComponents.Contains(aware))
                {
                    awareComponents.Add(aware);
                }
            }

            return new AwareRegistration(aware);
        }

        /// <summary>
        /// シーン階層が変わったときキャッシュを無効化する
        /// </summary>
        public static void NotifyHierarchyChanged()
        {
            awareCacheDirty = true;
        }

        /// <summary>
        /// 登録済みILanguageAwareUiとコールバックだけを再適用する
        /// </summary>
        public static void RefreshAllLoaded()
        {
            EnsureSceneHook();
            RefreshAwareCacheIfNeeded();

            ILanguageAwareUi[] components;
            Action[] handlers;
            lock (Gate)
            {
                components = awareComponents.Count == 0
                    ? Array.Empty<ILanguageAwareUi>()
                    : awareComponents.ToArray();
                handlers = registered.Count == 0
                    ? Array.Empty<Action>()
                    : registered.ToArray();
            }

            int nullAware = 0;
            for (int i = 0; i < components.Length; i++)
            {
                ILanguageAwareUi aware = components[i];
                if (aware == null)
                {
                    nullAware++;
                    continue;
                }

                if (aware is MonoBehaviour behaviour && behaviour == null)
                {
                    nullAware++;
                    continue;
                }

                try
                {
                    aware.RefreshLocalizedUi();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, aware as UnityEngine.Object);
                }
            }

            if (nullAware > 0)
            {
                PruneDestroyedAware();
            }

            for (int i = 0; i < handlers.Length; i++)
            {
                Action handler = handlers[i];
                if (handler == null)
                {
                    continue;
                }

                try
                {
                    handler.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void EnsureSceneHook()
        {
            if (sceneHooked)
            {
                return;
            }

            sceneHooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            awareCacheDirty = true;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            awareCacheDirty = true;
            PruneDestroyedAware();
        }

        private static void RefreshAwareCacheIfNeeded()
        {
            if (!awareCacheDirty)
            {
                return;
            }

            // 未Registerのシーン配置ILanguageAwareUiを一度だけ集める
            // 以降はRegisterAwareとこのキャッシュを正とする
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            lock (Gate)
            {
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour is ILanguageAwareUi aware && !awareComponents.Contains(aware))
                    {
                        awareComponents.Add(aware);
                    }
                }
            }

            awareCacheDirty = false;
        }

        private static void PruneDestroyedAware()
        {
            lock (Gate)
            {
                for (int i = awareComponents.Count - 1; i >= 0; i--)
                {
                    ILanguageAwareUi aware = awareComponents[i];
                    if (aware == null || (aware is MonoBehaviour behaviour && behaviour == null))
                    {
                        awareComponents.RemoveAt(i);
                    }
                }
            }
        }

        private sealed class Registration : IDisposable
        {
            private Action action;

            public Registration(Action action)
            {
                this.action = action;
            }

            public void Dispose()
            {
                if (action == null)
                {
                    return;
                }

                lock (Gate)
                {
                    registered.Remove(action);
                }

                action = null;
            }
        }

        private sealed class AwareRegistration : IDisposable
        {
            private ILanguageAwareUi aware;

            public AwareRegistration(ILanguageAwareUi aware)
            {
                this.aware = aware;
            }

            public void Dispose()
            {
                if (aware == null)
                {
                    return;
                }

                lock (Gate)
                {
                    awareComponents.Remove(aware);
                }

                aware = null;
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
