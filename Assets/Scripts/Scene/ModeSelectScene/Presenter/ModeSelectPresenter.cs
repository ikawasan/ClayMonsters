using Cysharp.Threading.Tasks;
using Scene.Core.Interfaces;
using Scene.ModeSelectScene.Interfaces;
using VContainer;

namespace Scene.ModeSelectScene.Presenter
{
    public class ModeSelectPresenter : IModeSelectPresenter
    {
        IClayMonsterSceneManager sceneManager;
        IModeSelectView modeSelectView;

        [Inject]
        public void Construct(IClayMonsterSceneManager sceneManager, IModeSelectView modeSelectView)
        {
            this.sceneManager = sceneManager;
            this.modeSelectView = modeSelectView;
        }

        void IModeSelectPresenter.Setup()
        {
            // Viewの各ボタンに遷移処理を紐付け
            modeSelectView.SubscribeClayEditButtonClick(OnClickClayEditButton);
            modeSelectView.SubscribeBattleNpcButtonClick(OnClickBattleNpcButton);
            modeSelectView.SubscribeBattlePVPButtonClick(OnClickBattlePVPButton);
            modeSelectView.SubscribeBackToTitleButtonClick(OnClickBackToTitleButton);
        }

        void OnClickClayEditButton() => sceneManager.TransitionScene(new ClayEditScene.ClayEditScene.ClayEditTransitionData()).Forget();
        void OnClickBattleNpcButton() => sceneManager.TransitionScene(new BattleNpcScene.BattleNpcScene.BattleNpcTransitionData()).Forget();
        void OnClickBattlePVPButton() => sceneManager.TransitionScene(new BattlePVPScene.BattlePVPScene.BattlePVPTransitionData()).Forget();

        // タイトルへはスタックを戻る処理とする
        void OnClickBackToTitleButton() => sceneManager.BackScene().Forget();
    }
}