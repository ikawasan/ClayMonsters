using UnityEngine;
using UnityEngine.UI;

namespace Extensions
{
    /// <summary>
    /// シーン配置済みButtonへ押下色リセットを自動付与する
    /// </summary>
    internal static class UiButtonPressedVisualResetBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToSceneButtons()
        {
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].EnsurePressedVisualReset();
            }
        }
    }
}
