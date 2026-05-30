using Scene.TitleScene.Presenter;
using Scene.TitleScene.View;
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

            // Viewをインターフェース経由でバインド
            builder.RegisterComponent(titleView).AsImplementedInterfaces();

            // Presenterをシングルトンライフサイクルで登録
            builder.Register<TitlePresenter>(Lifetime.Singleton).AsImplementedInterfaces();
        }
    }
}