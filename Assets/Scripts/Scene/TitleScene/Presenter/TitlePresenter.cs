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
            titleView.SubscribeNewGameButtonClick(OnClickNewGameButton);
            titleView.SubscribeContinueButtonClick(OnClickContinueButton);
            titleView.SubscribeOptionButtonClick(OnClickOptionButton);
            titleView.SubscribeQuitGameButtonClick(OnClickQuitGameButton);
        }

        void OnClickNewGameButton()
        {
            if (sceneManager.IsTransition) return;
            sceneManager.TransitionScene(new ModeSelectScene.ModeSelectScene.ModeSelectTransitionData()).Forget();
        }

        void OnClickContinueButton()
        {
            if (sceneManager.IsTransition) return;
            sceneManager.TransitionScene(new ModeSelectScene.ModeSelectScene.ModeSelectTransitionData()).Forget();
        }

        void OnClickOptionButton() => optionPresenter.Show();

        void OnClickQuitGameButton()
        {
#if UNITY_EDITOR
            // Unityエディタ上でのプレイモードを終了する
            UnityEditor.EditorApplication.isPlaying = false;
#else
    // ビルドされた実際のアプリを終了する
    UnityEngine.Application.Quit();
#endif
        }
    }
}