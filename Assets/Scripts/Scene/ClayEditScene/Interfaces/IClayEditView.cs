using System;

namespace Scene.ClayEditScene.Interfaces
{
    public interface IClayEditView
    {
        IDisposable SubscribeReturnButtonClick(Action action);
    }
}