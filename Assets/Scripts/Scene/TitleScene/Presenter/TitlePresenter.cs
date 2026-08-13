using Cysharp.Threading.Tasks;
using Extensions;
using Localization;
using R3;
using SaveData;
using SaveData.Interface;
using Scene.Core.Interface;
using Scene.DesktopPet.Interface;
using Scene.PvpLobby.Interface;
using Scene.TitleScene.Interface;
using System.Collections.Generic;
using UI.ModelGallery.Interface;
using UI.Option.Interface;
using UI.SkillTree.Interface;
using VContainer;

namespace Scene.TitleScene.Presenter
{
    public class TitlePresenter : ITitlePresenter
    {
        private readonly IClayMonsterSceneManager sceneManager;
        private readonly IClayModelSaveService saveService;
        private readonly IPointsService pointsService;
        private readonly IPvpLobby pvpLobby;
        private readonly ITitleView titleView;
        private readonly ITitleMessageWindowView messageWindowView;
        private readonly ITitleConfirmWindowView confirmWindowView;
        private readonly ITitleDesktopPetSlotSelectView desktopPetSlotSelectView;
        private readonly IDesktopPetLauncher desktopPetLauncher;
        private readonly IOptionPresenter optionPresenter;
        private readonly ISkillTreePresenter skillTreePresenter;
        private readonly IModelGalleryPresenter modelGalleryPresenter;

        private System.IDisposable pointsSubscription;
        private System.IDisposable desktopPetSelectionSubscription;
        private System.IDisposable desktopPetCancelSubscription;
        private int[] pendingDesktopPetSlotIndices = System.Array.Empty<int>();

        [Inject]
        public TitlePresenter(
            IClayMonsterSceneManager sceneManager,
            IClayModelSaveService saveService,
            IPointsService pointsService,
            IPvpLobby pvpLobby,
            ITitleView titleView,
            ITitleMessageWindowView messageWindowView,
            ITitleConfirmWindowView confirmWindowView,
            ITitleDesktopPetSlotSelectView desktopPetSlotSelectView,
            IDesktopPetLauncher desktopPetLauncher,
            IOptionPresenter optionPresenter,
            ISkillTreePresenter skillTreePresenter,
            IModelGalleryPresenter modelGalleryPresenter)
        {
            this.sceneManager = sceneManager;
            this.saveService = saveService;
            this.pointsService = pointsService;
            this.pvpLobby = pvpLobby;
            this.titleView = titleView;
            this.messageWindowView = messageWindowView;
            this.confirmWindowView = confirmWindowView;
            this.desktopPetSlotSelectView = desktopPetSlotSelectView;
            this.desktopPetLauncher = desktopPetLauncher;
            this.optionPresenter = optionPresenter;
            this.skillTreePresenter = skillTreePresenter;
            this.modelGalleryPresenter = modelGalleryPresenter;
        }

        void ITitlePresenter.Setup()
        {
            titleView.SubscribeClayEditButtonClick(OnClickClayEditButton);
            titleView.SubscribeBattleNpcButtonClick(OnClickBattleNpcButton);
            titleView.SubscribeBattlePvpButtonClick(OnClickBattlePvpButton);
            titleView.SubscribeTrainingButtonClick(OnClickTrainingButton);
            titleView.SubscribeSkillTreeButtonClick(OnClickSkillTreeButton);
            titleView.SubscribeModelGalleryButtonClick(OnClickModelGalleryButton);
            titleView.SubscribeOptionButtonClick(OnClickOptionButton);
            titleView.SubscribeDesktopPetButtonClick(OnClickDesktopPetButton);
            titleView.SubscribeQuitGameButtonClick(OnClickQuitGameButton);
            messageWindowView.SubscribeOkButtonClick(OnClickMessageWindowOk);
            confirmWindowView.SubscribeYesButtonClick(OnClickDesktopPetConfirmYes);
            confirmWindowView.SubscribeNoButtonClick(OnClickDesktopPetConfirmNo);
            skillTreePresenter.Setup();
            modelGalleryPresenter.Setup();

            desktopPetSelectionSubscription?.Dispose();
            desktopPetSelectionSubscription = desktopPetSlotSelectView.OnSelectionConfirmed
                .Subscribe(OnDesktopPetSelectionConfirmed);
            desktopPetCancelSubscription?.Dispose();
            desktopPetCancelSubscription = desktopPetSlotSelectView.OnCancelled
                .Subscribe(_ => OnDesktopPetSlotSelectCancelled());

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
            confirmWindowView.Hide();
            desktopPetSlotSelectView.Hide();
            pendingDesktopPetSlotIndices = System.Array.Empty<int>();
            optionPresenter.Hide();
            skillTreePresenter.Hide();
            modelGalleryPresenter.Hide();
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

            messageWindowView.ShowLocalized(
                GameTextKeys.TitleNoTrainedModel,
                "育成済みのモンスターがありません");
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

            messageWindowView.ShowLocalized(
                GameTextKeys.TitleNoUntrainedModel,
                "モンスターを作成してください");
        }

        private void OnClickSkillTreeButton()
        {
            skillTreePresenter.Show();
        }

        private void OnClickModelGalleryButton()
        {
            modelGalleryPresenter.Show();
        }

        private void OnClickMessageWindowOk()
        {
            messageWindowView.Hide();
        }

        private void OnClickOptionButton()
        {
            optionPresenter.Show();
        }

        private void OnClickDesktopPetButton()
        {
            if (sceneManager.IsTransition)
            {
                return;
            }

            messageWindowView.Hide();
            confirmWindowView.Hide();
            pendingDesktopPetSlotIndices = System.Array.Empty<int>();

            if (!saveService.HasAnySavedModel(ModelSavePool.Player))
            {
                messageWindowView.ShowLocalized(
                    GameTextKeys.TitleNoUntrainedModel,
                    "モンスターを作成してください");
                return;
            }

            desktopPetSlotSelectView.Show();
        }

        private void OnDesktopPetSelectionConfirmed(IReadOnlyList<int> slotIndices)
        {
            if (slotIndices == null || slotIndices.Count == 0)
            {
                return;
            }

            pendingDesktopPetSlotIndices = new int[slotIndices.Count];
            for (int i = 0; i < slotIndices.Count; i++)
            {
                pendingDesktopPetSlotIndices[i] = slotIndices[i];
            }

            desktopPetSlotSelectView.Hide();
            confirmWindowView.ShowLocalized(
                GameTextKeys.TitleDesktopPetConfirm,
                "ゲームを閉じてモンスターをデスクトップに表示しますか？");
        }

        private void OnDesktopPetSlotSelectCancelled()
        {
            pendingDesktopPetSlotIndices = System.Array.Empty<int>();
            desktopPetSlotSelectView.Hide();
            confirmWindowView.Hide();
        }

        private void OnClickDesktopPetConfirmYes()
        {
            if (pendingDesktopPetSlotIndices == null || pendingDesktopPetSlotIndices.Length == 0)
            {
                confirmWindowView.Hide();
                return;
            }

            int[] slotIndices = pendingDesktopPetSlotIndices;
            pendingDesktopPetSlotIndices = System.Array.Empty<int>();
            confirmWindowView.Hide();
            desktopPetSlotSelectView.Hide();
            optionPresenter.Hide();
            skillTreePresenter.Hide();
            modelGalleryPresenter.Hide();
            desktopPetLauncher.Launch(slotIndices);
        }

        private void OnClickDesktopPetConfirmNo()
        {
            confirmWindowView.Hide();
            pendingDesktopPetSlotIndices = System.Array.Empty<int>();
            desktopPetSlotSelectView.Show();
        }

        private void OnClickQuitGameButton()
        {
            ApplicationQuitGuard.RequestQuit();
        }
    }
}
