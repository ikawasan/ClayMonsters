using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// トーナメント図のパンとズーム
    /// 左ドラッグで移動ホイールで拡大縮小
    /// </summary>
    public sealed class NpcTournamentPanZoom : MonoBehaviour,
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
        [SerializeField] private Vector2 contentResetPosition = new Vector2(0f, 180f);

        private Vector2 dragPointerLocal;
        private Vector2 dragStartScreen;
        private bool isDragging;
        private bool hasPanned;
        private bool panConsumedForClick;
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
                content.anchoredPosition = contentResetPosition;
                initialAnchoredPosition = contentResetPosition;
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
            panConsumedForClick = false;
            hasPanned = false;
        }

        /// <summary>
        /// 指定Rectの中心をViewport中央に合わせて最大ズームする
        /// </summary>
        /// <param name="target">対象</param>
        public void FocusOn(RectTransform target)
        {
            if (content == null || viewport == null || target == null)
            {
                return;
            }

            float next = Mathf.Clamp(maxScale, minScale, maxScale);
            content.localScale = Vector3.one * next;

            Vector2 localInContent;
            if (!TryGetContentLocalPoint(target, out localInContent))
            {
                return;
            }

            FocusOnContentPoint(localInContent);
        }

        /// <summary>
        /// Contentローカル座標をViewport中央に合わせる(倍率は維持)
        /// </summary>
        /// <param name="contentLocalPoint">Content内座標</param>
        public void FocusOnContentPoint(Vector2 contentLocalPoint)
        {
            if (content == null)
            {
                return;
            }

            float scale = content.localScale.x;
            if (scale < 0.01f)
            {
                scale = 1f;
            }

            content.anchoredPosition = new Vector2(
                -contentLocalPoint.x * scale,
                -contentLocalPoint.y * scale);
            panConsumedForClick = false;
            hasPanned = false;
        }

        private bool TryGetContentLocalPoint(RectTransform target, out Vector2 localInContent)
        {
            localInContent = default;
            if (content == null || target == null)
            {
                return false;
            }

            // Content直下ならanchoredPositionが最も安定する
            if (target.parent == content)
            {
                Vector2 centerOffset = new Vector2(
                    (0.5f - target.pivot.x) * target.rect.width,
                    (0.5f - target.pivot.y) * target.rect.height);
                localInContent = target.anchoredPosition + centerOffset;
                return true;
            }

            Vector3 worldCenter = target.TransformPoint(target.rect.center);
            Vector3 local = content.InverseTransformPoint(worldCenter);
            localInContent = new Vector2(local.x, local.y);
            return true;
        }

        /// <summary>
        /// 直近のパン操作を消費してクリック無効判定に使う
        /// </summary>
        /// <returns>パン済みならtrue</returns>
        public bool ConsumePanForClickBlock()
        {
            bool blocked = panConsumedForClick || hasPanned;
            panConsumedForClick = false;
            return blocked;
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
                eventData.eligibleForClick = false;
                panConsumedForClick = true;
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
                    "[NpcTournamentPanZoom] Imageが必要ですViewportにImageを付けてください",
                    this);
                return;
            }

            image.raycastTarget = true;
        }
    }
}
