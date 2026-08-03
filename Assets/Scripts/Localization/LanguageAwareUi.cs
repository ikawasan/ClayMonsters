using System;
using System.Collections.Generic;
using UnityEngine;

namespace Localization
{
    /// <summary>
    /// 言語切替時に登録済みUIとILanguageAwareUiを再適用する
    /// </summary>
    public static class LanguageAwareUi
    {
        private static readonly List<Action> registered = new();
        private static readonly object Gate = new();

        /// <summary>
        /// 言語切替時に呼ぶコールバックを登録する
        /// </summary>
        /// <param name="onRefresh">再適用処理</param>
        /// <returns>解除用</returns>
        public static IDisposable Register(Action onRefresh)
        {
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
        /// シーン上のILanguageAwareUiと登録済みコールバックを再適用する
        /// </summary>
        public static void RefreshAllLoaded()
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour is not ILanguageAwareUi aware)
                {
                    continue;
                }

                try
                {
                    aware.RefreshLocalizedUi();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, behaviour);
                }
            }

            Action[] handlers;
            lock (Gate)
            {
                handlers = registered.ToArray();
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

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
