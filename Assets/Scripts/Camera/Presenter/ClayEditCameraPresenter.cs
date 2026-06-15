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
        }

        /// <inheritdoc />
        void IClayEditCameraPresenter.OnEnter()
        {
            Vector3 volumeCenter = Vector3.zero;
            cameraView.SetFocusPosition(volumeCenter);
            cameraView.SetInitializeView();
            cameraView.SetCameraEnable(true);
        }

        /// <inheritdoc />
        void IClayEditCameraPresenter.OnExit()
        {
            cameraView.SetCameraEnable(false);
        }
    }
}
