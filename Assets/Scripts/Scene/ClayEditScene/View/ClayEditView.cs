using Cysharp.Threading.Tasks;
using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.ClayEditScene.Interface;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace Scene.ClayEditScene.View
{
    public class ClayEditView : MonoBehaviour, IClayEditView
    {
        [SerializeField] LHButton openToModeSelectSceneButton;
        [SerializeField] LHButton toModeSelectSceneButton;
        [SerializeField] LHButton cancelToModeSelectSceneButton;

        [SerializeField] Canvas checkToModeSelectSceneWindow;

        [SerializeField] GameObject inputBlocker;

        Canvas IClayEditView.CheckToModeSelectSceneWindow => checkToModeSelectSceneWindow;

        GameObject IClayEditView.InputBlocker => inputBlocker;

        IDisposable IClayEditView.SubscribeOpenToModeSelectSceneWindowButtonClick(UnityAction action) => openToModeSelectSceneButton.SubscribeOnClick(action);
        IDisposable IClayEditView.SubscribeToModeSelectSceneButtonClick(UnityAction<CancellationToken> action)
            => toModeSelectSceneButton.SubscribeOnClick(() => action(this.GetCancellationTokenOnDestroy()));
        IDisposable IClayEditView.SubscribeCancelToModeSelectSceneButtonClick(UnityAction action) => cancelToModeSelectSceneButton.SubscribeOnClick(action);

        void IClayEditView.Inisialize()
        {
            checkToModeSelectSceneWindow.enabled = false;
        }
    }
}