using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.SkillTree.View
{
    /// <summary>
    /// スキルツリーのパンとズーム操作
    /// 左ドラッグで移動ホイールで拡大縮小
    /// </summary>
    public sealed class SkillTreePanZoom : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        private const float PanClickCancelThreshold = 8f;

        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private float minScale = 0.45f;
        [SerializeField] private float maxScale = 2.2f;
        [SerializeField] private float zoomStep = 0.08f;

        private Vector2 dragPointerLocal;
        private Vector2 dragStartScreen;
        private bool isDragging;
        private bool hasPanned;
        private Vector2 initialAnchoredPosition;
        private Vector3 initialScale;

        private void Awake()
        {
            if (content == null)
            {
                content = transform as RectTransform;
            }

            if (viewport == null)
            {
                viewport = content != null && content.parent is RectTransform parent
                    ? parent
                    : content;
            }

            if (content != null)
            {
                initialAnchoredPosition = content.anchoredPosition;
                initialScale = content.localScale;
            }

            EnsureRaycastTarget();
        }

        /// <summary>
        /// 表示位置と倍率を初期化する
        /// </summary>
        public void ResetView()
        {
            if (content == null)
            {
                return;
            }

            content.anchoredPosition = initialAnchoredPosition;
            content.localScale = initialScale;
        }

        /// <inheritdoc />
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (content == null || viewport == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewport,
                    eventData.position,
                    eventData.pressEventCamera,
                    out dragPointerLocal))
            {
                return;
            }

            dragStartScreen = eventData.position;
            hasPanned = false;
            isDragging = true;
        }

        /// <inheritdoc />
        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || content == null || viewport == null)
            {
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewport,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            if (!hasPanned
                && (eventData.position - dragStartScreen).sqrMagnitude
                >= PanClickCancelThreshold * PanClickCancelThreshold)
            {
                hasPanned = true;
            }

            Vector2 delta = localPoint - dragPointerLocal;
            dragPointerLocal = localPoint;
            content.anchoredPosition += delta;
        }

        /// <inheritdoc />
        public void OnEndDrag(PointerEventData eventData)
        {
            if (hasPanned)
            {
                // パン操作後はノードクリックを発火させない
                eventData.eligibleForClick = false;
            }

            isDragging = false;
            hasPanned = false;
        }

        /// <inheritdoc />
        public void OnScroll(PointerEventData eventData)
        {
            if (content == null || viewport == null)
            {
                return;
            }

            float scroll = eventData.scrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f)
            {
                return;
            }

            float current = content.localScale.x;
            float next = Mathf.Clamp(current + (scroll * zoomStep), minScale, maxScale);
            if (Mathf.Approximately(current, next))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewport,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                content.localScale = Vector3.one * next;
                return;
            }

            Vector2 contentPos = content.anchoredPosition;
            Vector2 before = (localPoint - contentPos) / current;
            content.localScale = Vector3.one * next;
            content.anchoredPosition = localPoint - (before * next);
        }

        private void EnsureRaycastTarget()
        {
            Image image = GetComponent<Image>();
            if (image == null)
            {
                Debug.LogError(
                    "[SkillTreePanZoom] Imageが必要ですTreeAreaにImageを付けてください",
                    this);
                return;
            }

            image.raycastTarget = true;
        }
    }
}
