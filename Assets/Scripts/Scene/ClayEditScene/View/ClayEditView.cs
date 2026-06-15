using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.ClayEditScene.Interface;
using System;
using UnityEngine;

namespace Scene.ClayEditScene.View
{
    public class ClayEditView : MonoBehaviour, IClayEditView
    {
        [SerializeField] LHButton openToModeSelectSceneButton;
        [SerializeField] LHButton toModeSelectSceneButton;
        [SerializeField] LHButton cancelToModeSelectSceneButton;

        [SerializeField] LHButton openSaveWindowButton;
        [SerializeField] LHButton saveButton;
        [SerializeField] LHButton cancelSaveButton;

        [SerializeField] Canvas checkToModeSelectSceneWindow;
        [SerializeField] Canvas checkSaveWindow;

        Canvas IClayEditView.CheckToModeSelectSceneWindow => checkToModeSelectSceneWindow;
        Canvas IClayEditView.CheckSaveWindow => checkSaveWindow;

        IDisposable IClayEditView.SubscribeOpenToModeSelectSceneWindowButtonClick(Action action) => openToModeSelectSceneButton.SubscribeOnClick(action);
        IDisposable IClayEditView.SubscribeToModeSelectSceneButtonClick(Action action) => toModeSelectSceneButton.SubscribeOnClick(action);
        IDisposable IClayEditView.SubscribeCancelToModeSelectSceneButtonClick(Action action) => cancelToModeSelectSceneButton.SubscribeOnClick(action);

        IDisposable IClayEditView.SubscribeOpenSaveWindowButtonClick(Action action) => openSaveWindowButton.SubscribeOnClick(action);
        IDisposable IClayEditView.SubscribeSaveButtonClick(Action action) => saveButton.SubscribeOnClick(action);
        IDisposable IClayEditView.SubscribeCancelSaveButtonClick(Action action) => cancelSaveButton.SubscribeOnClick(action);

        void IClayEditView.Inisialize()
        {
            checkToModeSelectSceneWindow.enabled = false;
            checkSaveWindow.enabled = false;
        }
    }
}