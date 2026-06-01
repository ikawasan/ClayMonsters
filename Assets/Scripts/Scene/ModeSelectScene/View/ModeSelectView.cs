using Extensions;
using LighthouseExtends.UIComponent.Button;
using Scene.ModeSelectScene.Interface;
using System;
using UnityEngine;

namespace Scene.ModeSelectScene.View
{
    public class ModeSelectView : MonoBehaviour, IModeSelectView
    {
        [SerializeField] LHButton clayEditButton;
        [SerializeField] LHButton battleNpcButton;
        [SerializeField] LHButton battlePvpButton;
        [SerializeField] LHButton backToTitleButton;

        IDisposable IModeSelectView.SubscribeClayEditButtonClick(Action action) => clayEditButton.SubscribeOnClick(action);
        IDisposable IModeSelectView.SubscribeBattleNpcButtonClick(Action action) => battleNpcButton.SubscribeOnClick(action);
        IDisposable IModeSelectView.SubscribeBattlePVPButtonClick(Action action) => battlePvpButton.SubscribeOnClick(action);
        IDisposable IModeSelectView.SubscribeBackToTitleButtonClick(Action action) => backToTitleButton.SubscribeOnClick(action);
    }
}