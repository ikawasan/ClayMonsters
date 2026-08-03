using Cysharp.Threading.Tasks;

using Extensions;

using LighthouseExtends.UIComponent.Button;

using Localization;

using Scene.ClayEditScene.Interface;

using System;

using System.Threading;

using UnityEngine;

using UnityEngine.Events;



namespace Scene.ClayEditScene.View

{

    public class ClayEditView : MonoBehaviour, IClayEditView, ILanguageAwareUi

    {

        [SerializeField] LHButton openToModeSelectSceneButton;

        [SerializeField] LHButton toModeSelectSceneButton;

        [SerializeField] LHButton cancelToModeSelectSceneButton;



        [SerializeField] Canvas checkToModeSelectSceneWindow;



        [SerializeField] GameObject inputBlocker;



        private LocalizedBakedTextApplier bakedLabelApplier;



        Canvas IClayEditView.CheckToModeSelectSceneWindow => checkToModeSelectSceneWindow;



        GameObject IClayEditView.InputBlocker => inputBlocker;



        private void Awake()

        {

            ApplyLocalizedLabels();

        }



        IDisposable IClayEditView.SubscribeOpenToModeSelectSceneWindowButtonClick(UnityAction action) => openToModeSelectSceneButton.SubscribeOnClick(action);

        IDisposable IClayEditView.SubscribeToModeSelectSceneButtonClick(UnityAction<CancellationToken> action)

            => toModeSelectSceneButton.SubscribeOnClick(() => action(this.GetCancellationTokenOnDestroy()));

        IDisposable IClayEditView.SubscribeCancelToModeSelectSceneButtonClick(UnityAction action) => cancelToModeSelectSceneButton.SubscribeOnClick(action);



        void IClayEditView.Inisialize()

        {

            ApplyLocalizedLabels();

            checkToModeSelectSceneWindow.enabled = false;

        }




        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedLabels();
        }

        private void ApplyLocalizedLabels()

        {

            EnsureBakedLabels();

            bakedLabelApplier?.Apply();



            LhButtonLabelUtility.SetLabel(

                openToModeSelectSceneButton,

                LocalizedText.GetOrFallback(GameTextKeys.CommonReturn, "戻る"));

            LhButtonLabelUtility.SetLabel(

                toModeSelectSceneButton,

                LocalizedText.GetOrFallback(GameTextKeys.ClayEditLeaveWithoutSaveButton, "保存せず"));

            LhButtonLabelUtility.SetLabel(

                cancelToModeSelectSceneButton,

                LocalizedText.GetOrFallback(GameTextKeys.CommonCancel, "キャンセル"));

        }



        private void EnsureBakedLabels()

        {

            if (bakedLabelApplier != null)

            {

                return;

            }



            bakedLabelApplier = new LocalizedBakedTextApplier();

            bakedLabelApplier.Register(

                GameTextKeys.ClayEditLeaveWithoutSave,

                "保存せずに戻りますか？");

            Transform root = checkToModeSelectSceneWindow != null

                ? checkToModeSelectSceneWindow.transform

                : transform;

            bakedLabelApplier.Capture(root);

        }

    }

}

