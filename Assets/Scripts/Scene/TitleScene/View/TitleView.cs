using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.TitleScene.Interfaces;
using System;
using UnityEngine;

namespace Scene.TitleScene.View
{
    public class TitleView : MonoBehaviour, ITitleView
    {
        [SerializeField] LHButton screenButton;

        IDisposable ITitleView.SubscribeScreenButtonClick(Action action) => screenButton.SubscribeOnClick(action);
    }
}
