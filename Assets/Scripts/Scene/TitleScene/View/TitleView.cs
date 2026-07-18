using Extensions;

using LighthouseExtends.UIComponent.Button;

using Scene.TitleScene.Interface;

using System;

using UnityEngine;

using UnityEngine.Events;

using UnityEngine.UI;



namespace Scene.TitleScene.View

{

    /// <summary>

    /// タイトル画面のメニューとロゴを表示する

    /// UIはTitleシーンのCanvas上に配置する

    /// </summary>

    public class TitleView : MonoBehaviour, ITitleView

    {

        [SerializeField] private LHButton clayEditButton;

        [SerializeField] private LHButton battleNpcButton;

        [SerializeField] private LHButton battlePvpButton;

        [SerializeField] private LHButton trainingButton;

        [SerializeField] private LHButton optionButton;

        [SerializeField] private LHButton quitGameButton;

        [SerializeField] private Image titleLogoImage;



        private void Awake()

        {

            ValidateSceneUi();

        }



        private void ValidateSceneUi()

        {

            if (titleLogoImage == null || battlePvpButton == null)

            {

                Debug.LogError(

                    "[TitleView] シーン上のUI参照が未設定です。HierarchyでUI参照を確認してください",

                    this);

            }

        }



        public IDisposable SubscribeClayEditButtonClick(UnityAction action) => clayEditButton.SubscribeOnClick(action);



        public IDisposable SubscribeBattleNpcButtonClick(UnityAction action) => battleNpcButton.SubscribeOnClick(action);



        public IDisposable SubscribeBattlePvpButtonClick(UnityAction action) => battlePvpButton.SubscribeOnClick(action);



        public IDisposable SubscribeTrainingButtonClick(UnityAction action)
        {
            if (trainingButton == null)
            {
                return new EmptyDisposable();
            }

            return trainingButton.SubscribeOnClick(action);
        }



        public IDisposable SubscribeOptionButtonClick(UnityAction action) => optionButton.SubscribeOnClick(action);



        public IDisposable SubscribeQuitGameButtonClick(UnityAction action) => quitGameButton.SubscribeOnClick(action);

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

    }

}

