using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.TitleScene.Interface;
using System;
using UnityEngine;

namespace Scene.TitleScene.View
{
    public class TitleView : MonoBehaviour, ITitleView
    {
        [SerializeField]
        LHButton screenButton;

        [SerializeField]
        LHButton optionButton;

        IDisposable ITitleView.SubscribeScreenButtonClick(Action action) => screenButton.SubscribeOnClick(action);
        public IDisposable SubscribeOptionButtonClick(Action action) => optionButton.SubscribeOnClick(action);
    }
}
