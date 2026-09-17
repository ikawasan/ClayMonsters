using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameData
{
    public enum EditModeType { Clay, Paint, Animation }

    [System.Serializable]
    public class EditModeTypeObject
    {
        [SerializeField] private EditModeType editModeType;
        [SerializeField] private List<GameObject> editModeObjects;
        [SerializeField] private List<Canvas> editModeUI;

        public void SetActive(int index, bool enableUi = true)
        {
            bool isActive = (int)editModeType == index;
            foreach (var obj in editModeObjects)
            {
                obj.SetActive(isActive);
            }

            bool visible = isActive && enableUi;
            foreach (var ui in editModeUI)
            {
                SetCanvasEnabled(ui, visible);
            }
        }

        // UI非表示ゲートはGraphicRaycasterも落とすためCanvas.enabledだけでは操作が戻らない
        private static void SetCanvasEnabled(Canvas canvas, bool visible)
        {
            if (canvas == null)
            {
                return;
            }

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
    }
}
