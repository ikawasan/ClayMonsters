using Cysharp.Threading.Tasks;
using Scene.ClayEditScene.Interface;
using Scene.Core.Interface;
using VContainer;

namespace Scene.ClayEditScene.Presenter
{
    public class ClayEditPresenter : IClayEditPresenter
    {
        IClayMonsterSceneManager sceneManager;
        IClayEditView clayEditView;

        [Inject]
        public void Construct(IClayMonsterSceneManager sceneManager, IClayEditView clayEditView)
        {
            this.sceneManager = sceneManager;
            this.clayEditView = clayEditView;
        }

        void IClayEditPresenter.Setup()
        {
            // ƒCƒxƒ“ƒg‚ð“o˜^
            clayEditView.SubscribeOpenToModeSelectSceneWindowButtonClick(OpenToModeSelectSceneButton);
            clayEditView.SubscribeToModeSelectSceneButtonClick(ToModeSelectSceneButton);
            clayEditView.SubscribeCancelToModeSelectSceneButtonClick(CancelToModeSelectSceneButton);
            clayEditView.SubscribeOpenSaveWindowButtonClick(OpenSaveWindowButton);
            clayEditView.SubscribeSaveButtonClick(SaveButton);
            clayEditView.SubscribeCancelSaveButtonClick(CancelSaveButton);
        }

        void IClayEditPresenter.OnEnter()
        {
            clayEditView.Inisialize();
        }

        void OpenToModeSelectSceneButton()
        {
            clayEditView.CheckToModeSelectSceneWindow.enabled = true;
        }
        void ToModeSelectSceneButton()
        {
            sceneManager.BackScene().Forget();
        }
        void CancelToModeSelectSceneButton()
        {
            clayEditView.CheckToModeSelectSceneWindow.enabled = false;
        }

        void OpenSaveWindowButton()
        {
            clayEditView.CheckSaveWindow.enabled = true;
        }
        void SaveButton()
        {
            sceneManager.BackScene().Forget();
        }
        void CancelSaveButton()
        {
            clayEditView.CheckSaveWindow.enabled = false;
        }
    }
}