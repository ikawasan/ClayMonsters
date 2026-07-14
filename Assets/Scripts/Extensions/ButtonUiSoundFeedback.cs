using Audio.Interface;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Extensions
{
    /// <summary>
    /// Buttonのホバーと押下でUI効果音を鳴らす
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ButtonUiSoundFeedback : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
    {
        private const float HoverCooldownSeconds = 0.05f;

        private static IUiSoundService uiSoundService;
        private static float lastHoverPlayTime;

        private Button button;

        /// <summary>
        /// UI効果音サービスを設定する
        /// </summary>
        /// <param name="service">再生サービス</param>
        public static void Configure(IUiSoundService service)
        {
            uiSoundService = service;
        }

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        /// <inheritdoc />
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanPlaySound())
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - lastHoverPlayTime < HoverCooldownSeconds)
            {
                return;
            }

            lastHoverPlayTime = now;
            uiSoundService.PlayHover();
        }

        /// <inheritdoc />
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanPlaySound())
            {
                return;
            }

            uiSoundService.PlayClick();
        }

        private bool CanPlaySound()
        {
            return uiSoundService != null
                && button != null
                && button.isActiveAndEnabled
                && button.interactable;
        }
    }
}
