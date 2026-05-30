using Cysharp.Threading.Tasks;
using Scene.ClayEditScene.Interfaces;
using Scene.Core.Interfaces;
using VContainer;

namespace Scene.ClayEditScene.Presenter
{
    public class ClayEditPresenter : IClayEditPresenter
    {
        IClayMonsterSceneManager sceneManager;
        IClayEditView clayEditView;

        [Inject]
        public void Construct(IClayMonsterSceneManager sceneManager, IClayEditView clayEditView)
        {
            this.sceneManager = sceneManager;
            this.clayEditView = clayEditView;
        }

        void IClayEditPresenter.Setup()
        {
            // バトル終了（または戻るボタン）のイベントを登録
            clayEditView.SubscribeReturnButtonClick(OnClickReturnButton);
        }

        void OnClickReturnButton()
        {
            // モード選択シーンの履歴がスタックに残っているため、BackSceneで戻る
            sceneManager.BackScene().Forget();
        }
    }
}