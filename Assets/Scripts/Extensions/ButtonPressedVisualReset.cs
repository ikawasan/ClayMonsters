using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Extensions
{
    /// <summary>
    /// 押下中にポインターがボタン外へ出たとき押下色が残る不具合を防ぐ
    /// PointerExitとPointerUpでSelectableの押下状態を解除する
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ButtonPressedVisualReset : MonoBehaviour, IPointerExitHandler, IPointerUpHandler
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        /// <inheritdoc />
        public void OnPointerExit(PointerEventData eventData)
        {
            ReleasePressedVisual(eventData);
        }

        /// <inheritdoc />
        public void OnPointerUp(PointerEventData eventData)
        {
            ReleasePressedVisual(eventData);
        }

        private void ReleasePressedVisual(PointerEventData eventData)
        {
            if (button == null || !button.isActiveAndEnabled || !button.interactable)
            {
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (eventData.pointerPress != button.gameObject)
            {
                return;
            }

            button.OnPointerUp(eventData);
        }
    }
}
