using System;

namespace Scene.BattleNpcScene.Interfaces
{
    public interface IBattleNpcView
    {
        IDisposable SubscribeReturnButtonClick(Action action);
    }
}