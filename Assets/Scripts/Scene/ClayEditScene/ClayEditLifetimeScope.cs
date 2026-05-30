using Scene.ClayEditScene.Presenter;
using Scene.ClayEditScene.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.ClayEditScene
{
    public class ClayEditLifetimeScope : LifetimeScope
    {
        [SerializeField] ClayEditScene clayEditScene;
        [SerializeField] ClayEditView clayEditView;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(clayEditScene);
            builder.RegisterComponent(clayEditView).AsImplementedInterfaces();
            builder.Register<ClayEditPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}