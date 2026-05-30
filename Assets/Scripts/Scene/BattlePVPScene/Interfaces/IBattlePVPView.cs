using System;

namespace Scene.BattlePVPScene.Interfaces
{
    public interface IBattlePVPView
    {
        IDisposable SubscribeReturnButtonClick(Action action);
    }
}