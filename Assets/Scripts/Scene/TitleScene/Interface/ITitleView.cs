using System;

namespace Scene.TitleScene.Interface
{
    public interface ITitleView
    {
        public IDisposable SubscribeScreenButtonClick(Action action);
        public IDisposable SubscribeOptionButtonClick(Action action);
    }
}
