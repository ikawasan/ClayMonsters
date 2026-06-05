using System;

namespace Scene.TitleScene.Interface
{
    public interface ITitleView
    {
        public IDisposable SubscribeNewGameButtonClick(Action action);
        public IDisposable SubscribeContinueButtonClick(Action action);
        public IDisposable SubscribeOptionButtonClick(Action action);
        public IDisposable SubscribeQuitGameButtonClick(Action action);
    }
}
