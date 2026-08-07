using Camera.Interface;
using Cysharp.Threading.Tasks;
using Lighthouse.Scene;
using Scene.Core;
using Scene.TitleScene.Interface;
using Scene.TrainingScene;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Scene.TitleScene
{
    public class TitleScene : FadeInSceneBase<TitleScene.TitleTransitionData>
    {
        [SerializeField] private TitleSkyboxRotator skyboxRotator;

        ITitlePresenter titlePresenter;
        TitleModelDisplay titleModelDisplay;
        TitleSceneCamera titleSceneCamera;
        IClayEditCameraView cameraView;

        public override MainSceneId MainSceneId => ClayMonstersMainSceneId.Title;

        // クラス内にネストして遷移データを定義
        public class TitleTransitionData : TransitionDataBase
        {
            public override MainSceneId MainSceneId => ClayMonstersMainSceneId.Title;
        }

        [Inject]
        public void Construct(
            ITitlePresenter titlePresenter,
            TitleModelDisplay titleModelDisplay,
            TitleSceneCamera titleSceneCamera,
            IClayEditCameraView cameraView)
        {
            this.titlePresenter = titlePresenter;
            this.titleModelDisplay = titleModelDisplay;
            this.titleSceneCamera = titleSceneCamera;
            this.cameraView = cameraView;
        }

        protected override UniTask OnSetup()
        {
            titlePresenter.Setup();
            return UniTask.CompletedTask;
        }

        protected override async UniTask OnEnterCore(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            TrainingSceneContentCleanup.DestroyLeakedTrainingBackgrounds();
            titlePresenter.OnEnter();
            await PrepareSceneViewAsync(cancelToken);
            // 暗転明け後に一回だけ開始し入場直後の空差し替えと重ねない
            ActivateSkyboxRotator();
        }

        private void ActivateSkyboxRotator()
        {
            if (skyboxRotator == null)
            {
                skyboxRotator = GetComponent<TitleSkyboxRotator>();
            }

            if (skyboxRotator == null)
            {
                Debug.LogError(
                    "[TitleScene] TitleSkyboxRotatorが未配線ですHierarchyのTitleへ配置してください",
                    this);
                return;
            }

            if (!skyboxRotator.enabled)
            {
                skyboxRotator.enabled = true;
            }

            skyboxRotator.Begin();
        }

        private async UniTask PrepareSceneViewAsync(CancellationToken cancelToken)
        {
            await titleModelDisplay.RefreshAsync(cancelToken);
            if (titleSceneCamera.RotateModelAwayFromWindows)
            {
                titleModelDisplay.FaceAwayFromWindows(titleSceneCamera.WindowsReference);
            }

            titleModelDisplay.SnapAllModelsToGround();

            if (titleModelDisplay.TryGetVisualBounds(out Bounds bounds))
            {
                titleSceneCamera.ApplyView(cameraView, bounds.center, bounds);
            }
            else
            {
                titleSceneCamera.ApplyView(cameraView, titleModelDisplay.DefaultFocusCenter);
            }

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancelToken);
        }

        protected override UniTask OnLeave(ISceneTransitionContext context, CancellationToken cancelToken)
        {
            if (skyboxRotator != null)
            {
                skyboxRotator.End();
            }

            titlePresenter.OnLeave();
            titleModelDisplay.Clear();
            cameraView.SetCameraEnable(false);
            return base.OnLeave(context, cancelToken);
        }
    }
}