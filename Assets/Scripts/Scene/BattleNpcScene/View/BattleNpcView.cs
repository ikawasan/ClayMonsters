using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.BattleNpcScene.Interface;
using System;
using UnityEngine;

namespace Scene.BattleNpcScene.View
{
    public class BattleNpcView : MonoBehaviour, IBattleNpcView
    {
        [SerializeField] LHButton returnButton;

        IDisposable IBattleNpcView.SubscribeReturnButtonClick(Action action) => returnButton.SubscribeOnClick(action);
    }
}