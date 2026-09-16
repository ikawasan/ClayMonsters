using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Audio
{
    /// <summary>
    /// スライダー操作のPointerDown/Upのみを受け取る
    /// EventTriggerはIDragHandlerを実装するため同一オブジェクトのSlider操作を壊しうる
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliderPointerCaptureRelay : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        ICancelHandler
    {
        private readonly List<Action> onBegins = new List<Action>(2);
        private readonly List<Action> onEnds = new List<Action>(2);
        private bool isCapturing;

        /// <summary>
        /// 開始終了コールバックを追加する
        /// </summary>
        /// <param name="begin">操作開始</param>
        /// <param name="end">操作終了</param>
        public void AddListener(Action begin, Action end)
        {
            if (begin != null)
            {
                onBegins.Add(begin);
            }

            if (end != null)
            {
                onEnds.Add(end);
            }
        }

        /// <inheritdoc />
        public void OnPointerDown(PointerEventData eventData)
        {
            Begin();
        }

        /// <inheritdoc />
        public void OnPointerUp(PointerEventData eventData)
        {
            End();
        }

        /// <inheritdoc />
        public void OnCancel(BaseEventData eventData)
        {
            End();
        }

        private void OnDisable()
        {
            End();
        }

        private void Begin()
        {
            if (isCapturing)
            {
                return;
            }

            isCapturing = true;
            for (int i = 0; i < onBegins.Count; i++)
            {
                onBegins[i]?.Invoke();
            }
        }

        private void End()
        {
            if (!isCapturing)
            {
                return;
            }

            isCapturing = false;
            for (int i = 0; i < onEnds.Count; i++)
            {
                onEnds[i]?.Invoke();
            }
        }
    }
}
