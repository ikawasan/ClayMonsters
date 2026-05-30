using Scene.BattleNpcScene.Presenter;
using Scene.BattleNpcScene.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.BattleNpcScene
{
    public class BattleNpcLifetimeScope : LifetimeScope
    {
        [SerializeField] BattleNpcScene battleNpcScene;
        [SerializeField] BattleNpcView battleNpcView;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(battleNpcScene);
            builder.RegisterComponent(battleNpcView).AsImplementedInterfaces();
            builder.Register<BattleNpcPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}