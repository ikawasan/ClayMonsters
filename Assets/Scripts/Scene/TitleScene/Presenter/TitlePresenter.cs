using Cysharp.Threading.Tasks;
using R3;
using SaveData;
using SaveData.Interface;
using Scene.Core.Interface;
using Scene.PvpLobby.Interface;
using Scene.TitleScene.Interface;
using UI.Option.Interface;
using VContainer;

namespace Scene.TitleScene.Presenter
{
    public class TitlePresenter : ITitlePresenter
    {
        private const string NoUntrainedModelMessage = "モンスターを作成してください";
        private const string NoTrainedModelMessage = "育成済みのモンスターがありません";

        private readonly IClayMonsterSceneManager sceneManager;
        private readonly IClayModelSaveService saveService;
        private readonly IPointsService pointsService;
        private readonly IPvpLobby pvpLobby;
        private readonly ITitleView titleView;
        private readonly ITitleMessageWindowView messageWindowView;
        private readonly IOptionPresenter optionPresenter;

        private System.IDisposable pointsSubscription;

        [Inject]
        public TitlePresenter(
            IClayMonsterSceneManager sceneManager,
            IClayModelSaveService saveService,
            IPointsService pointsService,
            IPvpLobby pvpLobby,
            ITitleView titleView,
            ITitleMessageWindowView messageWindowView,
            IOptionPresenter optionPresenter)
        {
            this.sceneManager = sceneManager;
            this.saveService = saveService;
            this.pointsService = pointsService;
            this.pvpLobby = pvpLobby;
            this.titleView = titleView;
            this.messageWindowView = messageWindowView;
            this.optionPresenter = optionPresenter;
        }

        void ITitlePresenter.Setup()
        {
            titleView.SubscribeClayEditButtonClick(OnClickClayEditButton);
            titleView.SubscribeBattleNpcButtonClick(OnClickBattleNpcButton);
            titleView.SubscribeBattlePvpButtonClick(OnClickBattlePvpButton);
            titleView.SubscribeTrainingButtonClick(OnClickTrainingButton);
            titleView.SubscribeOptionButtonClick(OnClickOptionButton);
            titleView.SubscribeQuitGameButtonClick(OnClickQuitGameButton);
            messageWindowView.SubscribeOkButtonClick(OnClickMessageWindowOk);

            pointsSubscription?.Dispose();
            pointsSubscription = pointsService.PointsObservable
                .Subscribe(points => titleView.SetPoints(points));
            titleView.SetPoints(pointsService.Points);
        }

        /// <inheritdoc/>
        void ITitlePresenter.OnEnter()
        {
            pointsService.Reload();
            titleView.SetPoints(pointsService.Points);
        }

        /// <inheritdoc/>
        void ITitlePresenter.OnLeave()
        {
            messageWindowView.Hide();
            optionPresenter.Hide();
        }

        private void OnClickClayEditButton()
        {
            if (sceneManager.IsTransition)
            {
                return;
            }

            sceneManager.TransitionScene(new ClayEditScene.ClayEditScene.ClayEditTransitionData()).Forget();
        }

        private void OnClickBattleNpcButton()
        {
            if (sceneManager.IsTransition)
            {
                return;
            }

            TryTransitionToBattle(() =>
                sceneManager.TransitionScene(new BattleNpcScene.BattleNpcScene.BattleNpcTransitionData()).Forget());
        }

        private void OnClickBattlePvpButton()
        {
            if (sceneManager.IsTransition)
            {
                return;
            }

            TryTransitionToBattle(() => pvpLobby.Show());
        }

        private void TryTransitionToBattle(System.Action transition)
        {
            if (saveService.HasAnySavedModel(ModelSavePool.TrainedPlayer))
            {
                transition();
                return;
            }

            messageWindowView.Show(NoTrainedModelMessage);
        }

        private void OnClickTrainingButton()
        {
            if (sceneManager.IsTransition)
            {
                return;
            }

            if (saveService.HasAnySavedModel(ModelSavePool.Player))
            {
                sceneManager.TransitionScene(new TrainingScene.TrainingScene.TrainingTransitionData()).Forget();
                return;
            }

            messageWindowView.Show(NoUntrainedModelMessage);
        }

        private void OnClickMessageWindowOk()
        {
            messageWindowView.Hide();
        }

        private void OnClickOptionButton()
        {
            optionPresenter.Show();
        }

        private void OnClickQuitGameButton()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
