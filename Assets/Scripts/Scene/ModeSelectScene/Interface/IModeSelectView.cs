using System;

namespace Scene.ModeSelectScene.Interface
{
    public interface IModeSelectView
    {
        IDisposable SubscribeClayEditButtonClick(Action action);
        IDisposable SubscribeBattleNpcButtonClick(Action action);
        IDisposable SubscribeBattlePVPButtonClick(Action action);
        IDisposable SubscribeBackToTitleButtonClick(Action action);
    }
}