using Cysharp.Threading.Tasks;
using Scene.Core.Interfaces;
using Scene.TitleScene.Interfaces;
using VContainer;

namespace Scene.TitleScene.Presenter
{
    public class TitlePresenter : ITitlePresenter
    {
        IClayMonsterSceneManager sceneManager;
        ITitleView titleView;

        [Inject]
        public void Construct(IClayMonsterSceneManager sceneManager, ITitleView titleView)
        {
            this.sceneManager = sceneManager;
            this.titleView = titleView;
        }

        void ITitlePresenter.Setup()
        {
            // Viewからボタンクリックイベントを購読
            titleView.SubscribeScreenButtonClick(OnClickScreenButton);
        }

        void OnClickScreenButton()
        {
            if (sceneManager.IsTransition) return;

            // モード選択シーン（ModeSelect）へ遷移する
            sceneManager.TransitionScene(new ModeSelectScene.ModeSelectScene.ModeSelectTransitionData()).Forget();
        }
    }
}