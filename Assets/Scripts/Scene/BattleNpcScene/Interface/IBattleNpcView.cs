using System;
using Battle.Interface;
using UnityEngine.Events;

namespace Scene.BattleNpcScene.Interface
{
    public interface IBattleNpcView : IBattleVictoryReturnView
    {
        IDisposable SubscribeReturnButtonClick(UnityAction action);
    }
}
