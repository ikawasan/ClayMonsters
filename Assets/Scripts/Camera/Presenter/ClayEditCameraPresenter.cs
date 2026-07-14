using Camera.Interface;
using UnityEngine;
using VContainer;

namespace Camera.Presenter
{
    public class ClayEditCameraPresenter : IClayEditCameraPresenter
    {
        readonly IClayEditCameraView cameraView;

        [Inject]
        public ClayEditCameraPresenter(IClayEditCameraView cameraView)
        {
            this.cameraView = cameraView;
        }

        /// <inheritdoc />
        void IClayEditCameraPresenter.Setup()
        {
            cameraView.SetFocusPosition(Vector3.zero);
            cameraView.SetInitializeView();
        }

        /// <inheritdoc />
        void IClayEditCameraPresenter.OnEnter()
        {
            cameraView.SetFocusPosition(Vector3.zero);
            cameraView.SetInitializeView();
            cameraView.SetCameraEnable(true);
            cameraView.SetCameraOperatable(false);
        }

        /// <inheritdoc />
        void IClayEditCameraPresenter.OnEnterAfterFadeIn()
        {
            cameraView.SetCameraOperatable(true);
        }

        /// <inheritdoc />
        void IClayEditCameraPresenter.OnExit()
        {
            cameraView.SetCameraEnable(false);
        }
    }
}
