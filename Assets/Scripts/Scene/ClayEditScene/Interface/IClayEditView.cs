using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace Scene.ClayEditScene.Interface
{
    public interface IClayEditView
    {
        Canvas CheckToModeSelectSceneWindow { get; }

        GameObject InputBlocker { get; }

        IDisposable SubscribeOpenToModeSelectSceneWindowButtonClick(UnityAction action);
        IDisposable SubscribeToModeSelectSceneButtonClick(UnityAction<CancellationToken> action);
        IDisposable SubscribeCancelToModeSelectSceneButtonClick(UnityAction action);

        void Inisialize();
    }
}