using Scene.ModeSelectScene.Presenter;
using Scene.ModeSelectScene.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.ModeSelectScene
{
    public class ModeSelectLifetimeScope : LifetimeScope
    {
        [SerializeField] ModeSelectScene modeSelectScene;
        [SerializeField] ModeSelectView modeSelectView;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(modeSelectScene);
            builder.RegisterComponent(modeSelectView).AsImplementedInterfaces();
            builder.Register<ModeSelectPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}