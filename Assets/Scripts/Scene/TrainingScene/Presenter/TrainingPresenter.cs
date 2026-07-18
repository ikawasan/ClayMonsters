using Camera.Interface;
using Cysharp.Threading.Tasks;
using Scene.TrainingScene.Interface;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Scene.TrainingScene.Presenter
{
    /// <summary>
    /// 育成シーンの入退場処理を担当する
    /// </summary>
    public sealed class TrainingPresenter : ITrainingPresenter
    {
        private readonly IClayEditCameraView cameraView;
        private readonly ITrainingBackgroundView backgroundView;
        private readonly ITrainingHudView hudView;
        private readonly ITrainingTrainedSaveView trainedSaveView;
        private readonly TrainingBattleRunner battleRunner;
        private readonly TrainingDisplay trainingDisplay;

        [Inject]
        public TrainingPresenter(
            IClayEditCameraView cameraView,
            ITrainingBackgroundView backgroundView,
            ITrainingHudView hudView,
            ITrainingTrainedSaveView trainedSaveView,
            TrainingBattleRunner battleRunner,
            TrainingDisplay trainingDisplay)
        {
            this.cameraView = cameraView;
            this.backgroundView = backgroundView;
            this.hudView = hudView;
            this.trainedSaveView = trainedSaveView;
            this.battleRunner = battleRunner;
            this.trainingDisplay = trainingDisplay;
        }

        /// <inheritdoc/>
        public void Setup()
        {
        }

        /// <inheritdoc/>
        public UniTask OnEnterAsync(CancellationToken cancellationToken)
        {
            RestoreVisibleRoots();
            if (cameraView != null)
            {
                cameraView.SetCameraEnable(true);
                cameraView.SetCameraOperatable(false);
            }

            return UniTask.CompletedTask;
        }

        /// <inheritdoc/>
        public void OnLeave()
        {
            hudView?.Hide();
            trainedSaveView?.HideForLeave();
            backgroundView?.HideForLeave();
            battleRunner?.HideForLeave();
            trainingDisplay?.Clear();
            HideRootObject(trainingDisplay);
            if (cameraView != null)
            {
                cameraView.SetCameraEnable(false);
            }
        }

        private void RestoreVisibleRoots()
        {
            SetRootActive(trainingDisplay, true);
            if (backgroundView is MonoBehaviour backgroundBehaviour
                && backgroundBehaviour != null)
            {
                backgroundBehaviour.gameObject.SetActive(true);
            }
        }

        private static void HideRootObject(Component component)
        {
            SetRootActive(component, false);
        }

        private static void SetRootActive(Component component, bool active)
        {
            if (component != null)
            {
                component.gameObject.SetActive(active);
            }
        }
    }
}