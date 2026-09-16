using Audio.Interface;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Audio
{
    /// <summary>
    /// スライダー移動速度に応じてループSEの再生速度を変える
    /// </summary>
    public sealed class SliderMoveSeBinder : IDisposable
    {
        private const float MinPitch = 0.5f;
        private const float MaxPitch = 50f;
        private const float SpeedForMaxPitch = 3f;
        private const float MinMoveDelta = 0.0005f;

        private readonly ISeService seService;
        private readonly SeTrackId trackId;
        private bool isDragging;
        private bool isPlaying;
        private float lastValue;
        private int lastMoveFrame = -1;
        private bool disposed;
        private TickHost tickHost;

        public SliderMoveSeBinder(ISeService seService, SeTrackId trackId)
        {
            this.seService = seService;
            this.trackId = trackId;
        }

        /// <summary>
        /// 対象スライダーへ移動SEを接続する
        /// </summary>
        /// <param name="slider">監視するスライダー</param>
        public void Attach(Slider slider)
        {
            if (disposed || slider == null || seService == null)
            {
                return;
            }

            // EventTriggerはIDragHandlerを持ちSliderのドラッグを壊しうるため専用Relayを使う
            SliderPointerCaptureRelay relay = slider.GetComponent<SliderPointerCaptureRelay>();
            if (relay == null)
            {
                relay = slider.gameObject.AddComponent<SliderPointerCaptureRelay>();
            }

            relay.AddListener(() => BeginDrag(slider), EndDrag);

            slider.onValueChanged.AddListener(_ =>
            {
                if (isDragging)
                {
                    OnMoved(slider.normalizedValue);
                }
            });

            EnsureTickHost(slider);
        }

        /// <summary>
        /// 再生中SEを止める
        /// </summary>
        public void Stop()
        {
            EndDrag();
        }

        /// <summary>
        /// 再生中SEを止めて接続を終了する
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            EndDrag();
            if (tickHost != null)
            {
                tickHost.Bind(null);
                tickHost = null;
            }
        }

        internal void TickIdle()
        {
            if (disposed)
            {
                return;
            }

            if (Time.frameCount > lastMoveFrame)
            {
                StopPlayback();
            }
        }

        private void EnsureTickHost(Slider slider)
        {
            if (tickHost != null)
            {
                return;
            }

            tickHost = slider.gameObject.GetComponent<TickHost>();
            if (tickHost == null)
            {
                tickHost = slider.gameObject.AddComponent<TickHost>();
            }

            tickHost.Bind(this);
        }

        private void BeginDrag(Slider slider)
        {
            if (disposed || slider == null)
            {
                return;
            }

            isDragging = true;
            lastValue = slider.normalizedValue;
            lastMoveFrame = -1;
            StopPlayback();
        }

        private void OnMoved(float normalizedValue)
        {
            if (disposed || !isDragging || seService == null)
            {
                return;
            }

            float delta = Mathf.Abs(normalizedValue - lastValue);
            if (delta < MinMoveDelta)
            {
                StopPlayback();
                return;
            }

            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.008f);
            float speed = delta / dt;
            lastValue = normalizedValue;
            lastMoveFrame = Time.frameCount;

            float t = Mathf.Clamp01(speed / SpeedForMaxPitch);
            float pitch = Mathf.Lerp(MinPitch, MaxPitch, t);
            seService.PlayLoop(trackId, pitch);
            isPlaying = true;
        }

        private void EndDrag()
        {
            isDragging = false;
            lastMoveFrame = -1;
            StopPlayback();
        }

        private void StopPlayback()
        {
            if (!isPlaying || seService == null)
            {
                return;
            }

            seService.Stop(trackId);
            isPlaying = false;
        }

        private sealed class TickHost : MonoBehaviour
        {
            private SliderMoveSeBinder owner;

            public void Bind(SliderMoveSeBinder binder)
            {
                owner = binder;
            }

            private void LateUpdate()
            {
                owner?.TickIdle();
            }

            private void OnDisable()
            {
                owner?.Stop();
            }
        }
    }
}
