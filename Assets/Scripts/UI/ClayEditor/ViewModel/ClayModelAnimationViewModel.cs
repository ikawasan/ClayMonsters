using ClayEditor.Interface;
using UnityEngine;
using VContainer;

namespace UI.ClayEditor.ViewModel
{
    /// <summary>
    /// アニメーション再生のためのViewModel
    /// </summary>
    public sealed class ClayModelAnimationViewModel
    {
        private readonly IClaySceneContext context;

        [Inject]
        public ClayModelAnimationViewModel(IClaySceneContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// 現在のモデルの Animator に指定トリガーを送る
        /// </summary>
        /// <param name="triggerName">トリガー名</param>
        public void PlayTrigger(string triggerName)
        {
            var currentModel = context.CurrentModel.Value;
            if (currentModel != null && currentModel.Animator != null)
            {
                currentModel.Animator.SetTrigger(triggerName);
            }
            else
            {
                Debug.LogWarning("対象のAnimatorが存在しません。");
            }
        }
    }

}
