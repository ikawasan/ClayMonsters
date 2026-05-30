using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.BattlePVPScene.Interfaces;
using System;
using UnityEngine;

namespace Scene.BattlePVPScene.View
{
    public class BattlePVPView : MonoBehaviour, IBattlePVPView
    {
        [SerializeField] LHButton returnButton;

        IDisposable IBattlePVPView.SubscribeReturnButtonClick(Action action) => returnButton.SubscribeOnClick(action);
    }
}