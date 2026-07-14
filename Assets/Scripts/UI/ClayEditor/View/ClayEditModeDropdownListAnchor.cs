using TMPro;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// ClayEditモード選択ドロップダウン展開時にリストをボタン直下へ固定する
    /// レイアウト値はシーン上のTemplateから複製する
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Dropdown))]
    public sealed class ClayEditModeDropdownListAnchor : MonoBehaviour
    {
        private TMP_Dropdown dropdown;
        private RectTransform dropdownRect;

        private void Awake()
        {
            dropdown = GetComponent<TMP_Dropdown>();
            dropdownRect = transform as RectTransform;
        }

        private void LateUpdate()
        {
            if (dropdown == null || dropdownRect == null || dropdown.template == null)
            {
                return;
            }

            Transform parent = dropdown.template.parent;
            if (parent == null)
            {
                return;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name != "Dropdown List")
                {
                    continue;
                }

                if (child is RectTransform listRect)
                {
                    AnchorListBelowDropdown(listRect);
                }
            }
        }

        private void OnDisable()
        {
            if (dropdown != null)
            {
                ClayEditModeDropdownLayout.Collapse(dropdown);
            }
        }

        private void AnchorListBelowDropdown(RectTransform listRect)
        {
            RectTransform template = dropdown.template;
            if (listRect.parent != dropdownRect)
            {
                listRect.SetParent(dropdownRect, false);
            }

            listRect.anchorMin = template.anchorMin;
            listRect.anchorMax = template.anchorMax;
            listRect.pivot = template.pivot;
            listRect.anchoredPosition = template.anchoredPosition;
            listRect.sizeDelta = template.sizeDelta;
            listRect.localScale = Vector3.one;
        }
    }
}
