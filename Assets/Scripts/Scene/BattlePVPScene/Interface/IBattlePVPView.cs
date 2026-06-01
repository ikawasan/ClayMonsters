using System;

namespace Scene.BattlePVPScene.Interface
{
    public interface IBattlePVPView
    {
        IDisposable SubscribeReturnButtonClick(Action action);
    }
}