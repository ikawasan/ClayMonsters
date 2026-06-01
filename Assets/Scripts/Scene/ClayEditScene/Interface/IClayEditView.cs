using System;

namespace Scene.ClayEditScene.Interface
{
    public interface IClayEditView
    {
        IDisposable SubscribeReturnButtonClick(Action action);
    }
}