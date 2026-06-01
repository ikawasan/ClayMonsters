using Cysharp.Threading.Tasks;
using Scene.BattlePVPScene.Interface;
using Scene.Core.Interface;
using VContainer;

namespace Scene.BattlePVPScene.Presenter
{
    public class BattlePVPPresenter : IBattlePVPPresenter
    {
        IClayMonsterSceneManager sceneManager;
        IBattlePVPView battlePVPView;

        [Inject]
        public void Construct(IClayMonsterSceneManager sceneManager, IBattlePVPView battlePVPView)
        {
            this.sceneManager = sceneManager;
            this.battlePVPView = battlePVPView;
        }

        void IBattlePVPPresenter.Setup()
        {
            // バトル終了（または戻るボタン）のイベントを登録
            battlePVPView.SubscribeReturnButtonClick(OnClickReturnButton);
        }

        void OnClickReturnButton()
        {
            // モード選択シーンの履歴がスタックに残っているため、BackSceneで戻る
            sceneManager.BackScene().Forget();
        }
    }
}