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
        private enum ConfirmIntent
        {
            None = 0,
            DesktopPet = 1,
            DesktopPetZOrder = 2,
            NpcTournament = 3,
            NpcTournamentContinue = 4,
            QuitGame = 5,
        }

        private readonly IClayMonsterSceneManager sceneManager;
        private readonly IClayModelSaveService saveService;
        private readonly IPointsService pointsService;
        private readonly INpcTournamentProgressService tournamentProgress;
        private readonly IPvpLobby pvpLobby;
        private readonly ITitleView titleView;
        private readonly ITitleMessageWindowView messageWindowView;
        private readonly ITitleConfirmWindowView confirmWindowView;
        private readonly ITitleDesktopPetSlotSelectView desktopPetSlotSelectView;
        private readonly ITitleNpcBattleMenuView npcBattleMenuView;
        private readonly IDesktopPetLauncher desktopPetLauncher;
        private readonly IOptionPresenter optionPresenter;
        private readonly ISkillTreePresenter skillTreePresenter;
        private readonly IModelGalleryPresenter modelGalleryPresenter;

        private System.IDisposable pointsSubscription;
        private System.IDisposable desktopPetSelectionSubscription;
        private System.IDisposable desktopPetCancelSubscription;
        private int[] pendingDesktopPetSlotIndices = System.Array.Empty<int>();
        private ConfirmIntent confirmIntent = ConfirmIntent.None;
        private NpcTournamentDifficulty pendingTournamentDifficulty = NpcTournamentDifficulty.Normal;

        private bool IsTitleInputBlocked =>
            sceneManager.IsTransition || ApplicationQuitGuard.IsQuitting;

        [Inject]
        public TitlePresenter(
            IClayMonsterSceneManager sceneManager,
            IClayModelSaveService saveService,
            IPointsService pointsService,
            INpcTournamentProgressService tournamentProgress,
            IPvpLobby pvpLobby,
            ITitleView titleView,
            ITitleMessageWindowView messageWindowView,
            ITitleConfirmWindowView confirmWindowView,
            ITitleDesktopPetSlotSelectView desktopPetSlotSelectView,
            ITitleNpcBattleMenuView npcBattleMenuView,
            IDesktopPetLauncher desktopPetLauncher,
            IOptionPresenter optionPresenter,
            ISkillTreePresenter skillTreePresenter,
            IModelGalleryPresenter modelGalleryPresenter)
        {
            this.sceneManager = sceneManager;
            this.saveService = saveService;
            this.pointsService = pointsService;
            this.tournamentProgress = tournamentProgress;
            this.pvpLobby = pvpLobby;
            this.titleView = titleView;
            this.messageWindowView = messageWindowView;
            this.confirmWindowView = confirmWindowView;
            this.desktopPetSlotSelectView = desktopPetSlotSelectView;
            this.npcBattleMenuView = npcBattleMenuView;
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
            confirmWindowView.SubscribeYesButtonClick(OnClickConfirmYes);
            confirmWindowView.SubscribeNoButtonClick(OnClickConfirmNo);
            npcBattleMenuView.SubscribeTournamentButtonClick(OnClickNpcTournament);
            npcBattleMenuView.SubscribeFreeBattleButtonClick(OnClickNpcFreeBattle);
            npcBattleMenuView.SubscribeModeBackButtonClick(OnClickNpcModeBack);
            npcBattleMenuView.SubscribeEasyButtonClick(() => OnClickNpcDifficulty(NpcTournamentDifficulty.Easy));
            npcBattleMenuView.SubscribeNormalButtonClick(() => OnClickNpcDifficulty(NpcTournamentDifficulty.Normal));
            npcBattleMenuView.SubscribeHardButtonClick(() => OnClickNpcDifficulty(NpcTournamentDifficulty.Hard));
            npcBattleMenuView.SubscribeVeryHardButtonClick(
                () => OnClickNpcDifficulty(NpcTournamentDifficulty.VeryHard));
            npcBattleMenuView.SubscribeDifficultyBackButtonClick(OnClickNpcDifficultyBack);
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
            saveService.Reload();
            pointsService.Reload();
            titleView.SetPoints(pointsService.Points);
        }

        /// <inheritdoc/>
        void ITitlePresenter.OnLeave()
        {
            messageWindowView.Hide();
            confirmWindowView.Hide();
            desktopPetSlotSelectView.Hide();
            npcBattleMenuView.Hide();
            pendingDesktopPetSlotIndices = System.Array.Empty<int>();
            confirmIntent = ConfirmIntent.None;
            optionPresenter.Hide();
            skillTreePresenter.Hide();
            modelGalleryPresenter.Hide();
        }

        private void OnClickClayEditButton()
        {
            if (IsTitleInputBlocked)
            {
                return;
            }

            sceneManager.TransitionScene(new ClayEditScene.ClayEditScene.ClayEditTransitionData()).Forget();
        }

        private void OnClickBattleNpcButton()
        {
            if (IsTitleInputBlocked)
            {
                return;
            }

            TryTransitionToBattle(OpenNpcBattleMenuOrFreeBattle);
        }

        private void OpenNpcBattleMenuOrFreeBattle()
        {
            messageWindowView.Hide();
            confirmWindowView.Hide();
            confirmIntent = ConfirmIntent.None;
            desktopPetSlotSelectView.Hide();

            if (!npcBattleMenuView.IsConfigured)
            {
                TransitionToFreeBattle();
                return;
            }

            npcBattleMenuView.ShowModeSelect();
        }

        private void OnClickNpcTournament()
        {
            if (IsTitleInputBlocked)
            {
                return;
            }

            tournamentProgress?.Reload();
            if (tournamentProgress != null && tournamentProgress.HasProgress)
            {
                NpcTournamentProgressSaveData progress = tournamentProgress.GetProgressOrNull();
                if (progress != null)
                {
                    pendingTournamentDifficulty = (NpcTournamentDifficulty)progress.difficulty;
                    confirmIntent = ConfirmIntent.NpcTournamentContinue;
                    npcBattleMenuView.Hide();
                    confirmWindowView.ShowLocalized(
                        GameTextKeys.TitleNpcTournamentContinueConfirm,
                        "中断したトーナメントの続きから行いますか？");
                    return;
                }
            }

            confirmWindowView.Hide();
            confirmIntent = ConfirmIntent.None;
            npcBattleMenuView.ShowDifficultySelect();
        }

        private void OnClickNpcFreeBattle()
        {
            if (IsTitleInputBlocked)
            {
                return;
            }

            npcBattleMenuView.Hide();
            confirmWindowView.Hide();
            confirmIntent = ConfirmIntent.None;
            TransitionToFreeBattle();
        }

        private void OnClickNpcModeBack()
        {
            confirmWindowView.Hide();
            confirmIntent = ConfirmIntent.None;
            npcBattleMenuView.Hide();
        }

        private void OnClickNpcDifficulty(NpcTournamentDifficulty difficulty)
        {
            if (IsTitleInputBlocked)
            {
                return;
            }

            pendingTournamentDifficulty = difficulty;
            confirmIntent = ConfirmIntent.NpcTournament;
            npcBattleMenuView.Hide();
            confirmWindowView.ShowLocalized(
                GameTextKeys.TitleNpcTournamentConfirm,
                "難易度「{difficulty}」でトーナメントを開始して良いですか？",
                "difficulty",
                ResolveDifficultyLabel(difficulty));
        }

        private void OnClickNpcDifficultyBack()
        {
            confirmWindowView.Hide();
            confirmIntent = ConfirmIntent.None;
            npcBattleMenuView.ShowModeSelect();
        }

        private void TransitionToFreeBattle()
        {
            sceneManager.TransitionScene(
                new BattleNpcScene.BattleNpcScene.BattleNpcTransitionData()).Forget();
        }

        private void TransitionToTournament(NpcTournamentDifficulty difficulty, bool resume = false)
        {
            sceneManager.TransitionScene(
                new BattleNpcScene.BattleNpcScene.BattleNpcTransitionData
                {
                    IsTournament = true,
                    TournamentDifficulty = difficulty,
                    ResumeTournament = resume,
                }).Forget();
        }

        private static string ResolveDifficultyLabel(NpcTournamentDifficulty difficulty)
        {
            switch (difficulty)
            {
                case NpcTournamentDifficulty.Easy:
                    return LocalizedText.GetOrFallback(
                        GameTextKeys.TitleNpcTournamentEasy,
                        "イージー");
                case NpcTournamentDifficulty.Hard:
                    return LocalizedText.GetOrFallback(
                        GameTextKeys.TitleNpcTournamentHard,
                        "ハード");
                case NpcTournamentDifficulty.VeryHard:
                    return LocalizedText.GetOrFallback(
                        GameTextKeys.TitleNpcTournamentVeryHard,
                        "ベリーハード");
                case NpcTournamentDifficulty.Normal:
                default:
                    return LocalizedText.GetOrFallback(
                        GameTextKeys.TitleNpcTournamentNormal,
                        "ノーマル");
            }
        }

        private void OnClickBattlePvpButton()
        {
            if (IsTitleInputBlocked)
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
            if (IsTitleInputBlocked)
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
            if (IsTitleInputBlocked)
            {
                return;
            }

            messageWindowView.Hide();
            confirmWindowView.Hide();
            confirmIntent = ConfirmIntent.None;
            pendingDesktopPetSlotIndices = System.Array.Empty<int>();
            npcBattleMenuView.Hide();

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
            confirmIntent = ConfirmIntent.DesktopPet;
            confirmWindowView.ShowLocalized(
                GameTextKeys.TitleDesktopPetConfirm,
                "ゲームを閉じてモンスターをデスクトップへ表示しますか？\n（経過時間に応じて自動でポイントを獲得することができます）");
        }

        private void OnDesktopPetSlotSelectCancelled()
        {
            pendingDesktopPetSlotIndices = System.Array.Empty<int>();
            desktopPetSlotSelectView.Hide();
            confirmWindowView.Hide();
            confirmIntent = ConfirmIntent.None;
        }

        private void OnClickConfirmYes()
        {
            switch (confirmIntent)
            {
                case ConfirmIntent.DesktopPet:
                    OnDesktopPetConfirmYes();
                    break;
                case ConfirmIntent.DesktopPetZOrder:
                    LaunchDesktopPet(stayOnTop: true);
                    break;
                case ConfirmIntent.NpcTournament:
                    OnNpcTournamentConfirmYes();
                    break;
                case ConfirmIntent.NpcTournamentContinue:
                    OnNpcTournamentContinueConfirmYes();
                    break;
                case ConfirmIntent.QuitGame:
                    OnQuitGameConfirmYes();
                    break;
                default:
                    confirmWindowView.Hide();
                    break;
            }
        }

        private void OnClickConfirmNo()
        {
            switch (confirmIntent)
            {
                case ConfirmIntent.DesktopPet:
                    OnDesktopPetConfirmNo();
                    break;
                case ConfirmIntent.DesktopPetZOrder:
                    LaunchDesktopPet(stayOnTop: false);
                    break;
                case ConfirmIntent.NpcTournament:
                    OnNpcTournamentConfirmNo();
                    break;
                case ConfirmIntent.NpcTournamentContinue:
                    OnNpcTournamentContinueConfirmNo();
                    break;
                case ConfirmIntent.QuitGame:
                    OnQuitGameConfirmNo();
                    break;
                default:
                    confirmWindowView.Hide();
                    break;
            }
        }

        private void OnDesktopPetConfirmYes()
        {
            if (pendingDesktopPetSlotIndices == null || pendingDesktopPetSlotIndices.Length == 0)
            {
                confirmWindowView.Hide();
                confirmIntent = ConfirmIntent.None;
                return;
            }

            confirmIntent = ConfirmIntent.DesktopPetZOrder;
            confirmWindowView.ShowLocalizedChoice(
                GameTextKeys.TitleDesktopPetZOrderChoice,
                "表示の重ね順を選んでください",
                GameTextKeys.TitleDesktopPetTopmost,
                "最前面表示",
                GameTextKeys.TitleDesktopPetBottommost,
                "最背面表示");
        }

        private void OnDesktopPetConfirmNo()
        {
            confirmWindowView.Hide();
            confirmIntent = ConfirmIntent.None;
            pendingDesktopPetSlotIndices = System.Array.Empty<int>();
            desktopPetSlotSelectView.Show();
        }

        private void LaunchDesktopPet(bool stayOnTop)
        {
            if (pendingDesktopPetSlotIndices == null || pendingDesktopPetSlotIndices.Length == 0)
            {
                confirmWindowView.Hide();
                confirmIntent = ConfirmIntent.None;
                return;
            }

            int[] slotIndices = pendingDesktopPetSlotIndices;
            pendingDesktopPetSlotIndices = System.Array.Empty<int>();
            confirmIntent = ConfirmIntent.None;
            confirmWindowView.Hide();
            desktopPetSlotSelectView.Hide();
            optionPresenter.Hide();
            skillTreePresenter.Hide();
            modelGalleryPresenter.Hide();
            if (!desktopPetLauncher.Launch(slotIndices, stayOnTop))
            {
                messageWindowView.ShowLocalized(
                    GameTextKeys.DesktopPetCacheNotFound,
                    "デスクトップペットのキャッシュが見つかりません。");
            }
        }

        private void OnNpcTournamentConfirmYes()
        {
            NpcTournamentDifficulty difficulty = pendingTournamentDifficulty;
            confirmIntent = ConfirmIntent.None;
            confirmWindowView.Hide();
            npcBattleMenuView.Hide();
            tournamentProgress?.ClearProgress();
            TransitionToTournament(difficulty, resume: false);
        }

        private void OnNpcTournamentConfirmNo()
        {
            confirmIntent = ConfirmIntent.None;
            confirmWindowView.Hide();
            npcBattleMenuView.ShowDifficultySelect();
        }

        private void OnNpcTournamentContinueConfirmYes()
        {
            NpcTournamentDifficulty difficulty = pendingTournamentDifficulty;
            confirmIntent = ConfirmIntent.None;
            confirmWindowView.Hide();
            npcBattleMenuView.Hide();
            TransitionToTournament(difficulty, resume: true);
        }

        private void OnNpcTournamentContinueConfirmNo()
        {
            confirmIntent = ConfirmIntent.None;
            confirmWindowView.Hide();
            npcBattleMenuView.ShowDifficultySelect();
        }

        private void OnClickQuitGameButton()
        {
            if (IsTitleInputBlocked)
            {
                return;
            }

            messageWindowView.Hide();
            desktopPetSlotSelectView.Hide();
            npcBattleMenuView.Hide();
            confirmIntent = ConfirmIntent.QuitGame;
            confirmWindowView.ShowLocalized(
                GameTextKeys.TitleQuitConfirm,
                "ゲームを終了しますか？");
        }

        private void OnQuitGameConfirmYes()
        {
            confirmIntent = ConfirmIntent.None;
            confirmWindowView.Hide();
            ApplicationQuitGuard.RequestQuit();
        }

        private void OnQuitGameConfirmNo()
        {
            confirmIntent = ConfirmIntent.None;
            confirmWindowView.Hide();
        }
    }
}
