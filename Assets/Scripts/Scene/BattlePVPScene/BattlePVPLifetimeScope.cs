using Scene.BattlePVPScene.Presenter;
using Scene.BattlePVPScene.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.BattlePVPScene
{
    public class BattlePVPLifetimeScope : LifetimeScope
    {
        [SerializeField] BattlePVPScene battlePVPScene;
        [SerializeField] BattlePVPView battlePVPView;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(battlePVPScene);
            builder.RegisterComponent(battlePVPView).AsImplementedInterfaces();
            builder.Register<BattlePVPPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}