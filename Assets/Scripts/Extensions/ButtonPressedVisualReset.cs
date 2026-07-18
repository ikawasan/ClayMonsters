using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Extensions
{
    /// <summary>
    /// 押下中にポインターがボタン外へ出たとき押下色が残る不具合を防ぐ
    /// PointerExitとPointerUpでSelectableの押下状態を強制解除する
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ButtonPressedVisualReset : MonoBehaviour, IPointerExitHandler, IPointerUpHandler
    {
        private static readonly PropertyInfo IsPointerDownProperty = typeof(Selectable).GetProperty(
            "isPointerDown",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo EvaluateAndTransitionMethod = typeof(Selectable).GetMethod(
            "EvaluateAndTransitionToSelectionState",
            BindingFlags.Instance | BindingFlags.NonPublic);

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

            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (eventData.pointerPress != null && eventData.pointerPress != button.gameObject)
            {
                return;
            }

            button.OnPointerUp(eventData);
            ClearSelectablePointerDown(button);
            InvokeEvaluateAndTransition(button);
        }

        private static void ClearSelectablePointerDown(Selectable selectable)
        {
            if (selectable == null || IsPointerDownProperty == null)
            {
                return;
            }

            IsPointerDownProperty.SetValue(selectable, false);
        }

        private static void InvokeEvaluateAndTransition(Selectable selectable)
        {
            if (selectable == null || EvaluateAndTransitionMethod == null)
            {
                return;
            }

            EvaluateAndTransitionMethod.Invoke(selectable, null);
        }
    }
}
