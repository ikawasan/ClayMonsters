using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.Core
{
    public class BootstrapLifetimeScope : LifetimeScope
    {
        [SerializeField] BootstrapLifetimeScopeSettings bootstrapLifetimeScopeSettings;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<Bootstrap>();
            builder.RegisterInstance(bootstrapLifetimeScopeSettings);
        }
    }
}
