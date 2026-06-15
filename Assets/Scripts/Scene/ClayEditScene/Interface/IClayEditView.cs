using System;
using UnityEngine;

namespace Scene.ClayEditScene.Interface
{
    public interface IClayEditView
    {
        Canvas CheckToModeSelectSceneWindow { get; }
        Canvas CheckSaveWindow { get; }

        IDisposable SubscribeOpenToModeSelectSceneWindowButtonClick(Action action);
        IDisposable SubscribeToModeSelectSceneButtonClick(Action action);
        IDisposable SubscribeCancelToModeSelectSceneButtonClick(Action action);

        IDisposable SubscribeOpenSaveWindowButtonClick(Action action);
        IDisposable SubscribeSaveButtonClick(Action action);
        IDisposable SubscribeCancelSaveButtonClick(Action action);

        void Inisialize();
    }
}