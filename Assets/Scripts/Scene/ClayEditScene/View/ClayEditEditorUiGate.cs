using Scene.ClayEditScene.Interface;
using UnityEngine;

namespace Scene.ClayEditScene.View
{
    /// <summary>
    /// ClayEdit入場フロー完了まで編集UIルートを隠す
    /// </summary>
    public sealed class ClayEditEditorUiGate : MonoBehaviour, IClayEditEditorUiGate
    {
        [Tooltip("入場フロー完了まで非表示にする編集UIルート")]
        [SerializeField] private GameObject[] editorUiRoots;

        private void Awake()
        {
            SetEditorVisible(false);
        }

        /// <inheritdoc />
        public void SetEditorVisible(bool isVisible)
        {
            if (editorUiRoots == null)
            {
                return;
            }

            for (int i = 0; i < editorUiRoots.Length; i++)
            {
                if (editorUiRoots[i] != null)
                {
                    editorUiRoots[i].SetActive(isVisible);
                }
            }
        }
    }
}
