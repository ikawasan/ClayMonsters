using Cysharp.Threading.Tasks;
using Scene.Core.Interface;
using Scene.TitleScene.Interface;
using UI.Option.Interface;
using VContainer;

namespace Scene.TitleScene.Presenter
{
    public class TitlePresenter : ITitlePresenter
    {
        readonly IClayMonsterSceneManager sceneManager;
        readonly ITitleView titleView;
        readonly IOptionPresenter optionPresenter;

        [Inject]
        public TitlePresenter(
            IClayMonsterSceneManager sceneManager,
            ITitleView titleView,
            IOptionPresenter optionPresenter)
        {
            this.sceneManager = sceneManager;
            this.titleView = titleView;
            this.optionPresenter = optionPresenter;
        }

        void ITitlePresenter.Setup()
        {
            titleView.SubscribeScreenButtonClick(OnClickScreenButton);
            titleView.SubscribeOptionButtonClick(OnClickOptionButton);
        }

        void OnClickScreenButton()
        {
            if (sceneManager.IsTransition) return;
            sceneManager.TransitionScene(new ModeSelectScene.ModeSelectScene.ModeSelectTransitionData()).Forget();
        }

        void OnClickOptionButton() => optionPresenter.Show();
    }
}