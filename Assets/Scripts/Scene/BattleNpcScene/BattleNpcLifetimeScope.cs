using Battle.Interface;

using Camera.Model;

using Camera.Presenter;

using Camera.View;

using SaveData;

using SaveData.Interface;

using SaveData.Service;

using Scene.BattleNpcScene.Interface;

using Scene.BattleNpcScene.View;

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

        [SerializeField] BattleNpcView battleNpcView;



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



        protected override void Configure(IContainerBuilder builder)

        {

            EnsureSerializedReferences();

            loadSlotView?.ConfigureSavePool(ModelSavePool.TrainedPlayer, "未育成");



            builder.RegisterComponent(battleNpcScene);

            builder.RegisterComponent(battleNpcView).AsImplementedInterfaces();

            builder.Register<BattleNpcSceneCoordinator>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<MonsterSelectionSession>(Lifetime.Singleton).AsImplementedInterfaces();



            builder.RegisterComponent(cameraView).AsImplementedInterfaces();

            builder.Register<ClayEditCameraPresenter>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<ClayEditCameraModel>(Lifetime.Singleton).AsImplementedInterfaces();



            builder.RegisterComponent(battleFlowRunner).AsImplementedInterfaces();

            builder.RegisterComponent(postProcessView).AsImplementedInterfaces();

            builder.RegisterComponent(classroomLighting);

            builder.RegisterComponent(matchupBackground);

            builder.Register<BattleCanvasTransition>(Lifetime.Singleton).As<IBattleCanvasTransition>();

            builder.RegisterComponentInHierarchy<BattleStartOverlayView>();



            builder.Register<ClayModelSaveService>(Lifetime.Singleton).As<IClayModelSaveService>();

            builder.Register<ClayModelGltfImporter>(Lifetime.Singleton).As<IClayModelImporter>();

            builder.Register<ClayModelGltfExporter>(Lifetime.Singleton).As<IClayModelExporter>();

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
        }

    }

}

