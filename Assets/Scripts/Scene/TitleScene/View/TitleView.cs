using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.TitleScene.Interface;
using System;
using UnityEngine;

namespace Scene.TitleScene.View
{
    public class TitleView : MonoBehaviour, ITitleView
    {
        [SerializeField] LHButton newGameButton;

        [SerializeField] LHButton continueButton;

        [SerializeField] LHButton optionButton;

        [SerializeField] LHButton quitGameButton;

        public IDisposable SubscribeNewGameButtonClick(Action action) => newGameButton.SubscribeOnClick(action);
        public IDisposable SubscribeContinueButtonClick(Action action) => continueButton.SubscribeOnClick(action);
        public IDisposable SubscribeOptionButtonClick(Action action) => optionButton.SubscribeOnClick(action);
        public IDisposable SubscribeQuitGameButtonClick(Action action) => quitGameButton.SubscribeOnClick(action);
    }
}
