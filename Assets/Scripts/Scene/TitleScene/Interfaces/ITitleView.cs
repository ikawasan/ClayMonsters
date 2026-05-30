using System;

namespace Scene.TitleScene.Interfaces
{
    public interface ITitleView
    {
        public IDisposable SubscribeScreenButtonClick(Action action);
    }
}
