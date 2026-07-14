using System;
using UnityEngine;

namespace Extensions
{
    /// <summary>
    /// 旧CanvasGroup表示切替ヘルパー
    /// CanvasVisibilityUtilityへ移行済み
    /// </summary>
    [Obsolete("CanvasVisibilityUtilityを使用してください")]
    public static class CanvasGroupVisibilityUtility
    {
        /// <summary>
        /// CanvasGroupの表示状態を設定する
        /// </summary>
        /// <param name="group">対象CanvasGroup</param>
        /// <param name="visible">表示するか</param>
        public static void SetVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
            {
                return;
            }

            Canvas canvas = group.GetComponent<Canvas>();
            if (canvas != null)
            {
                CanvasVisibilityUtility.SetCanvasEnabled(canvas, visible);
                return;
            }

            CanvasVisibilityUtility.SetUiVisible(group.gameObject, visible);
        }
    }
}
