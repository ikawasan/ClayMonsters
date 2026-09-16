using Battle.Interface;
using Camera.Model;
using Camera.Presenter;
using Camera.View;
using SaveData;
using Scene.BattleNpcScene.Tournament;
using Scene.BattleNpcScene.View;
using Scene.BattlePVPScene.View;
using UI.ClayEditor.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scene.BattleNpcScene
{
    public class BattleNpcLifetimeScope : LifetimeScope
    {
        [Header("Scene Views")]
        [SerializeField] BattleNpcScene battleNpcScene;
        [SerializeField] BattlePvpVictoryReturnView victoryDualReturnView;

        [Header("Camera")]
        [SerializeField] ClayEditCameraView cameraView;

        [Header("Battle")]
        [SerializeField] BattleFlowRunner battleFlowRunner;

        [Header("Post Process")]
        [SerializeField] BattleNpcPostProcessView postProcessView;
        [SerializeField] BattleClassroomLighting classroomLighting;
        [SerializeField] BattleMatchupBackgroundView matchupBackground;

        [Header("UI Views")]
        [SerializeField] LoadSlotView loadSlotView;
        [SerializeField] NpcTournamentBracketView tournamentBracketView;

        protected override void Configure(IContainerBuilder builder)
        {
            EnsureSerializedReferences();

            loadSlotView?.ConfigureSavePool(
                ModelSavePool.TrainedPlayer,
                Localization.LocalizedText.GetOrFallback(
                    Localization.GameTextKeys.SaveUntrainedPool,
                    "未育成"));

            builder.RegisterComponent(battleNpcScene);
            builder.RegisterComponent(victoryDualReturnView).AsImplementedInterfaces();
            builder.Register<BattleNpcSceneCoordinator>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<MonsterSelectionSession>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<NpcTournamentEntryState>(Lifetime.Singleton).As<INpcTournamentEntryState>();

            if (tournamentBracketView == null)
            {
                Debug.LogError(
                    "[BattleNpcLifetimeScope] tournamentBracketViewが未配線です"
                    + "トーナメントUIを配置し接続してください",
                    this);
                builder.Register<NullNpcTournamentBracketView>(Lifetime.Singleton)
                    .As<INpcTournamentBracketView>();
            }
            else
            {
                builder.RegisterComponent(tournamentBracketView).AsImplementedInterfaces();
            }

            builder.RegisterComponent(cameraView).AsImplementedInterfaces();
            builder.Register<ClayEditCameraPresenter>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.RegisterComponent(battleFlowRunner).AsImplementedInterfaces();
            builder.RegisterComponent(postProcessView).AsImplementedInterfaces();
            builder.RegisterComponent(classroomLighting);
            builder.RegisterComponent(matchupBackground);
            builder.Register<BattleCanvasTransition>(Lifetime.Singleton).As<IBattleCanvasTransition>();
            builder.RegisterComponentInHierarchy<BattleStartOverlayView>();

            builder.RegisterComponent(loadSlotView);
        }

        private void EnsureSerializedReferences()
        {
            if (classroomLighting == null)
            {
                Debug.LogError(
                    "[BattleNpcLifetimeScope] classroomLightingが未配線です",
                    this);
            }

            if (matchupBackground == null)
            {
                Debug.LogError("[BattleNpcLifetimeScope] matchupBackgroundが未配線です", this);
            }

            if (victoryDualReturnView == null)
            {
                Debug.LogError(
                    "[BattleNpcLifetimeScope] victoryDualReturnViewが未配線です",
                    this);
            }
        }
    }
}
