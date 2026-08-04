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
    /// <summary>
    /// 粘土編集シーンの戻り確認ウィンドウ表示
    /// </summary>
    public class ClayEditView : MonoBehaviour, IClayEditView, ILanguageAwareUi
    {
        [SerializeField] private LHButton openToModeSelectSceneButton;
        [SerializeField] private LHButton toModeSelectSceneButton;
        [SerializeField] private LHButton cancelToModeSelectSceneButton;
        [SerializeField] private Canvas checkToModeSelectSceneWindow;
        [SerializeField] private GameObject inputBlocker;

        private LocalizedBakedTextApplier bakedLabelApplier;
        private bool labelOriginalsCaptured;
        private string openBackOriginal = "戻る";
        private string leaveWithoutSaveOriginal = "保存せず戻る";
        private string cancelOriginal = "キャンセル";

        Canvas IClayEditView.CheckToModeSelectSceneWindow => checkToModeSelectSceneWindow;

        GameObject IClayEditView.InputBlocker => inputBlocker;

        private void Awake()
        {
            ApplyLocalizedLabels();
        }

        IDisposable IClayEditView.SubscribeOpenToModeSelectSceneWindowButtonClick(UnityAction action) =>
            openToModeSelectSceneButton.SubscribeOnClick(action);

        IDisposable IClayEditView.SubscribeToModeSelectSceneButtonClick(UnityAction<CancellationToken> action) =>
            toModeSelectSceneButton.SubscribeOnClick(() => action(this.GetCancellationTokenOnDestroy()));

        IDisposable IClayEditView.SubscribeCancelToModeSelectSceneButtonClick(UnityAction action) =>
            cancelToModeSelectSceneButton.SubscribeOnClick(action);

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
            CaptureLabelOriginalsIfNeeded();

            LhButtonLabelUtility.SetLabel(
                openToModeSelectSceneButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonReturn, openBackOriginal));
            LhButtonLabelUtility.SetLabel(
                toModeSelectSceneButton,
                SceneLocalizedLabel.Resolve(
                    GameTextKeys.ClayEditLeaveWithoutSaveButton,
                    leaveWithoutSaveOriginal));
            LhButtonLabelUtility.SetLabel(
                cancelToModeSelectSceneButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonCancel, cancelOriginal));
        }

        private void CaptureLabelOriginalsIfNeeded()
        {
            if (labelOriginalsCaptured)
            {
                return;
            }

            openBackOriginal = SceneLocalizedLabel.Capture(
                openToModeSelectSceneButton,
                openBackOriginal);
            leaveWithoutSaveOriginal = SceneLocalizedLabel.Capture(
                toModeSelectSceneButton,
                leaveWithoutSaveOriginal);
            cancelOriginal = SceneLocalizedLabel.Capture(
                cancelToModeSelectSceneButton,
                cancelOriginal);
            labelOriginalsCaptured = true;
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
