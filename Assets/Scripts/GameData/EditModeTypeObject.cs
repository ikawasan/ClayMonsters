using System.Collections.Generic;
using UnityEngine;

namespace GameData
{
    public enum EditModeType { Clay, Paint, Animation }

    [System.Serializable]
    public class EditModeTypeObject
    {
        [SerializeField] private EditModeType editModeType;
        [SerializeField] private List<GameObject> editModeObjects;
        [SerializeField] private List<Canvas> editModeUI;

        public void SetActive(int index)
        {
            bool isActive = (int)editModeType == index;
            foreach (var obj in editModeObjects)
            {
                obj.SetActive(isActive);
            }

            foreach (var ui in editModeUI)
            {
                ui.enabled = isActive;
            }
        }
    }
}
