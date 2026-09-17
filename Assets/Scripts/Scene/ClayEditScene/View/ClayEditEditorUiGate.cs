using Extensions;
using Scene.ClayEditScene.Interface;
using System.Collections.Generic;
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

        private readonly Dictionary<Canvas, bool> cachedCanvasVisible = new();
        private Canvas[] editorCanvases;
        private bool isCanvasesHidden;

        private void Awake()
        {
            SetEditorVisible(false);
        }

        /// <inheritdoc />
        public void SetEditorVisible(bool isVisible)
        {
            if (isVisible == false)
            {
                RestoreEditorCanvases();
            }

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

        /// <inheritdoc/>
        public void SetEditorCanvasesVisible(bool isVisible, Canvas keepEnabled)
        {
            CollectEditorCanvases();
            if (isVisible)
            {
                RestoreEditorCanvases();
                return;
            }

            if (isCanvasesHidden == false)
            {
                CacheEditorCanvasVisible(keepEnabled);
            }

            ApplyEditorCanvasVisible(false, keepEnabled);
            isCanvasesHidden = true;
        }

        private void CollectEditorCanvases()
        {
            if (editorCanvases != null || editorUiRoots == null)
            {
                return;
            }

            HashSet<Canvas> set = new();
            for (int i = 0; i < editorUiRoots.Length; i++)
            {
                GameObject root = editorUiRoots[i];
                if (root == null)
                {
                    continue;
                }

                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                for (int j = 0; j < canvases.Length; j++)
                {
                    if (canvases[j] != null)
                    {
                        set.Add(canvases[j]);
                    }
                }
            }

            editorCanvases = new Canvas[set.Count];
            set.CopyTo(editorCanvases);
        }

        private void CacheEditorCanvasVisible(Canvas keepEnabled)
        {
            cachedCanvasVisible.Clear();
            if (editorCanvases == null)
            {
                return;
            }

            for (int i = 0; i < editorCanvases.Length; i++)
            {
                Canvas canvas = editorCanvases[i];
                if (canvas == null || canvas == keepEnabled)
                {
                    continue;
                }

                cachedCanvasVisible[canvas] = canvas.enabled;
            }
        }

        private void ApplyEditorCanvasVisible(bool isVisible, Canvas keepEnabled)
        {
            if (editorCanvases == null)
            {
                return;
            }

            for (int i = 0; i < editorCanvases.Length; i++)
            {
                Canvas canvas = editorCanvases[i];
                if (canvas == null || canvas == keepEnabled)
                {
                    continue;
                }

                CanvasVisibilityUtility.SetCanvasEnabled(canvas, isVisible);
            }
        }

        private void RestoreEditorCanvases()
        {
            if (isCanvasesHidden == false)
            {
                return;
            }

            foreach (KeyValuePair<Canvas, bool> pair in cachedCanvasVisible)
            {
                if (pair.Key != null)
                {
                    // 非表示時と同じくRaycaster込みで戻す
                    CanvasVisibilityUtility.SetCanvasEnabled(pair.Key, pair.Value);
                }
            }

            cachedCanvasVisible.Clear();
            isCanvasesHidden = false;
        }
    }
}
