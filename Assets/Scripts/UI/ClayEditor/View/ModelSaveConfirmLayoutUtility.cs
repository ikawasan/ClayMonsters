using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// ModelSaveConfirmView配下の手動RectTransform配置を維持する
    /// LayoutGroup系がPlay時に座標を上書きするのを防ぐ
    /// </summary>
    public static class ModelSaveConfirmLayoutUtility
    {
        /// <summary>
        /// 手動配置を壊すレイアウトドライバーを無効化する
        /// 攻撃スロット内部のLayoutGroupは表示維持のため残す
        /// RectTransformの値は変更しない
        /// </summary>
        /// <param name="root">ConfirmContentなどのルート</param>
        public static void FreezeManualLayoutDrivers(Transform root)
        {
            if (root == null)
            {
                return;
            }

            LayoutGroup[] layoutGroups = root.GetComponentsInChildren<LayoutGroup>(true);
            for (int i = 0; i < layoutGroups.Length; i++)
            {
                if (ShouldPreserveLayoutDriver(layoutGroups[i].transform))
                {
                    continue;
                }

                layoutGroups[i].enabled = false;
            }

            ContentSizeFitter[] contentSizeFitters = root.GetComponentsInChildren<ContentSizeFitter>(true);
            for (int i = 0; i < contentSizeFitters.Length; i++)
            {
                if (ShouldPreserveLayoutDriver(contentSizeFitters[i].transform))
                {
                    continue;
                }

                contentSizeFitters[i].enabled = false;
            }

            AspectRatioFitter[] aspectRatioFitters = root.GetComponentsInChildren<AspectRatioFitter>(true);
            for (int i = 0; i < aspectRatioFitters.Length; i++)
            {
                if (ShouldPreserveLayoutDriver(aspectRatioFitters[i].transform))
                {
                    continue;
                }

                aspectRatioFitters[i].enabled = false;
            }
        }

        private static bool ShouldPreserveLayoutDriver(Transform driverTransform)
        {
            if (driverTransform == null)
            {
                return false;
            }

            return driverTransform.GetComponentInParent<TrainingAttackSlotView>(true) != null
                || driverTransform.GetComponentInParent<TrainingResumeAttacksContentView>(true) != null;
        }
    }
}
