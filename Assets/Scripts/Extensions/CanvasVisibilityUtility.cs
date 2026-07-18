using UnityEngine;
using UnityEngine.UI;

namespace Extensions
{
    /// <summary>
    /// CanvasとUIパネルの表示切替ヘルパー
    /// ルートCanvasはenabledで制御する
    /// GameObject.SetActiveは使わない
    /// </summary>
    public static class CanvasVisibilityUtility
    {
        private const string InputBlockerObjectName = "Blocker";

        /// <summary>
        /// Canvasの有効状態を設定する
        /// </summary>
        /// <param name="canvas">対象Canvas</param>
        /// <param name="visible">表示するか</param>
        public static void SetCanvasEnabled(Canvas canvas, bool visible)
        {
            if (canvas == null)
            {
                return;
            }

            // 非アクティブGOではCanvas.enabledだけでは表示されない
            if (visible && !canvas.gameObject.activeSelf)
            {
                canvas.gameObject.SetActive(true);
            }

            canvas.enabled = visible;

            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = visible;
            }
        }

        /// <summary>
        /// 同一GameObject上のCanvasを有効状態で設定する
        /// </summary>
        /// <param name="host">Canvasを持つGameObject</param>
        /// <param name="visible">表示するか</param>
        public static void SetCanvasEnabled(GameObject host, bool visible)
        {
            if (host == null)
            {
                return;
            }

            SetCanvasEnabled(host.GetComponent<Canvas>(), visible);
        }

        /// <summary>
        /// UI要素の表示状態を設定する
        /// CanvasまたはCanvasGroupを優先しGraphicのalphaで補う
        /// </summary>
        /// <param name="target">対象GameObject</param>
        /// <param name="visible">表示するか</param>
        public static void SetUiVisible(GameObject target, bool visible)
        {
            if (target == null)
            {
                return;
            }

            // 初期非アクティブのパネルを表示できるようにする
            if (visible && !target.activeSelf)
            {
                target.SetActive(true);
            }

            if (IsInputBlocker(target))
            {
                SetInputBlockerVisible(target, visible);
                return;
            }

            if (target.TryGetComponent(out Canvas canvas))
            {
                SetCanvasEnabled(canvas, visible);
                return;
            }

            if (target.TryGetComponent(out CanvasGroup group))
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
                return;
            }

            if (target.TryGetComponent(out Graphic graphic))
            {
                Color color = graphic.color;
                color.a = visible ? 1f : 0f;
                graphic.color = color;
                graphic.raycastTarget = visible;
            }

            Transform transform = target.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                SetUiVisible(transform.GetChild(i).gameObject, visible);
            }
        }

        /// <summary>
        /// 子パネルの表示状態を設定する
        /// SetUiVisibleへ委譲する
        /// </summary>
        /// <param name="panel">対象パネル</param>
        /// <param name="visible">表示するか</param>
        public static void SetPanelActive(GameObject panel, bool visible)
        {
            SetUiVisible(panel, visible);
        }

        /// <summary>
        /// Componentからパネル表示状態を設定する
        /// SetUiVisibleへ委譲する
        /// </summary>
        /// <param name="panelComponent">対象Component</param>
        /// <param name="visible">表示するか</param>
        public static void SetPanelActive(Component panelComponent, bool visible)
        {
            if (panelComponent == null)
            {
                return;
            }

            SetUiVisible(panelComponent.gameObject, visible);
        }

        private static bool IsInputBlocker(GameObject target)
        {
            return target != null && target.name == InputBlockerObjectName;
        }

        private static void SetInputBlockerVisible(GameObject target, bool blocksRaycasts)
        {
            if (!target.TryGetComponent(out Graphic graphic))
            {
                return;
            }

            Color color = graphic.color;
            color.a = 0f;
            graphic.color = color;
            graphic.raycastTarget = blocksRaycasts;
        }
    }
}
