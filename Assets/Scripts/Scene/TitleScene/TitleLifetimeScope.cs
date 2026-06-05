using Scene.TitleScene.Presenter;
using Scene.TitleScene.View;
using UI.Option.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.TitleScene
{
    public class TitleLifetimeScope : LifetimeScope
    {
        [SerializeField] TitleScene titleScene;
        [SerializeField] TitleView titleView;

        protected override void Configure(IContainerBuilder builder)
        {
            // Scene本体の登録
            builder.RegisterComponent(titleScene);

            builder.RegisterComponent(titleView).AsImplementedInterfaces();

            // Presenterをシングルトンライフサイクルで登録
            builder.Register<TitlePresenter>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}