using System;

namespace Scene.BattleNpcScene.Interface
{
    public interface IBattleNpcView
    {
        IDisposable SubscribeReturnButtonClick(Action action);
    }
}