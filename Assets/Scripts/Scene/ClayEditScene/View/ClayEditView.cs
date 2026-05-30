using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.ClayEditScene.Interfaces;
using System;
using UnityEngine;

namespace Scene.ClayEditScene.View
{
    public class ClayEditView : MonoBehaviour, IClayEditView
    {
        [SerializeField] LHButton returnButton;

        IDisposable IClayEditView.SubscribeReturnButtonClick(Action action) => returnButton.SubscribeOnClick(action);
    }
}