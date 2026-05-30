using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.Core
{
    public class Bootstrap : IStartable
    {
        readonly BootstrapLifetimeScope bootstrapLifetimeScope;
        readonly BootstrapLifetimeScopeSettings bootstrapLifetimeScopeSettings;

        [Inject]
        public Bootstrap(BootstrapLifetimeScope bootstrapLifetimeScope, BootstrapLifetimeScopeSettings bootstrapLifetimeScopeSettings)
        {
            this.bootstrapLifetimeScope = bootstrapLifetimeScope;
            this.bootstrapLifetimeScopeSettings = bootstrapLifetimeScopeSettings;
        }

        public void Start()
        {
            using (LifetimeScope.EnqueueParent(bootstrapLifetimeScope))
            {
                var instance = Object.Instantiate(bootstrapLifetimeScopeSettings.ClayMonstersLifetimeScopePrefab);
                Object.DontDestroyOnLoad(instance.gameObject);
            }
        }
    }
}