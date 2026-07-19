using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.Core
{
    /// <summary>
    /// LifetimeScope向けのコンポーネント登録ヘルパー
    /// </summary>
    public static class VContainerComponentRegistration
    {
        /// <summary>
        /// コンポーネントをRegisterComponentする
        /// </summary>
        /// <param name="builder">DIビルダー</param>
        /// <param name="component">登録対象</param>
        /// <param name="scopeTag">ログ用スコープ名</param>
        /// <typeparam name="T">コンポーネント型</typeparam>
        public static void RegisterComponent<T>(
            IContainerBuilder builder,
            T component,
            string scopeTag)
            where T : Component
        {
            if (component == null)
            {
                Debug.LogError($"[{scopeTag}] {typeof(T).Name} が未設定です");
                return;
            }

            builder.RegisterComponent(component);
        }

        /// <summary>
        /// コンポーネントを実装インターフェース付きでRegisterComponentする
        /// </summary>
        /// <param name="builder">DIビルダー</param>
        /// <param name="component">登録対象</param>
        /// <param name="scopeTag">ログ用スコープ名</param>
        /// <typeparam name="T">コンポーネント型</typeparam>
        public static void RegisterComponentAsInterfaces<T>(
            IContainerBuilder builder,
            T component,
            string scopeTag)
            where T : Component
        {
            if (component == null)
            {
                Debug.LogError($"[{scopeTag}] {typeof(T).Name} が未設定です");
                return;
            }

            builder.RegisterComponent(component).AsImplementedInterfaces();
        }
    }
}
