using Cysharp.Threading.Tasks;
using Scene.BattleNpcScene.Interfaces;
using Scene.Core.Interfaces;
using VContainer;

namespace Scene.BattleNpcScene.Presenter
{
    public class BattleNpcPresenter : IBattleNpcPresenter
    {
        IClayMonsterSceneManager sceneManager;
        IBattleNpcView battleNpcView;

        [Inject]
        public void Construct(IClayMonsterSceneManager sceneManager, IBattleNpcView battleNpcView)
        {
            this.sceneManager = sceneManager;
            this.battleNpcView = battleNpcView;
        }

        void IBattleNpcPresenter.Setup()
        {
            // バトル終了（または戻るボタン）のイベントを登録
            battleNpcView.SubscribeReturnButtonClick(OnClickReturnButton);
        }

        void OnClickReturnButton()
        {
            // モード選択シーンの履歴がスタックに残っているため、BackSceneで戻る
            sceneManager.BackScene().Forget();
        }
    }
}