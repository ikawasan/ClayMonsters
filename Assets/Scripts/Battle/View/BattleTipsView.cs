using Battle.Interface;
using Extensions;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Battle.View
{
    /// <summary>
    /// 戦闘チュートリアルTipsと右端ヒントの表示
    /// 文言はPrefab側のTMPに保持する
    /// </summary>
    public sealed class BattleTipsView : MonoBehaviour, IBattleTipsView
    {
        [Header("参照")]
        [SerializeField] private Canvas tipsCanvas;
        [SerializeField] private Canvas hintCanvas;
        [SerializeField] private Button closeButton;

        private bool isFeatureAvailable;
        private bool isCombatSessionActive;
        private bool isOpen;

        /// <inheritdoc />
        public bool IsOpen => isOpen;

        private void Awake()
        {
            closeButton?.EnsureUiSoundFeedback();
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseTips);
            }

            HideAll();
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseTips);
            }

            if (GameplayTime.IsPaused)
            {
                GameplayTime.IsPaused = false;
            }
        }

        private void Update()
        {
            if (!isCombatSessionActive || !isFeatureAvailable)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (isOpen)
            {
                CloseTips();
            }
            else
            {
                OpenTips();
            }
        }

        /// <inheritdoc />
        public IDisposable BeginCombatSession()
        {
            isFeatureAvailable = true;
            isCombatSessionActive = true;
            SetHintVisible(true);
            SetTipsVisible(false);
            return new Session(this);
        }

        /// <inheritdoc />
        public void HideAll()
        {
            EndCombatSessionInternal();
            SetHintVisible(false);
            SetTipsVisible(false);
        }

        private void OpenTips()
        {
            if (!isFeatureAvailable || !isCombatSessionActive)
            {
                return;
            }

            isOpen = true;
            GameplayTime.IsPaused = true;
            SetTipsVisible(true);
        }

        private void CloseTips()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            GameplayTime.IsPaused = false;
            SetTipsVisible(false);
        }

        private void EndCombatSessionInternal()
        {
            isCombatSessionActive = false;
            isFeatureAvailable = false;
            isOpen = false;
            GameplayTime.IsPaused = false;
        }

        private void SetTipsVisible(bool visible)
        {
            CanvasVisibilityUtility.SetCanvasEnabled(tipsCanvas, visible);
            if (closeButton != null)
            {
                closeButton.interactable = visible;
            }
        }

        private void SetHintVisible(bool visible)
        {
            CanvasVisibilityUtility.SetCanvasEnabled(hintCanvas, visible);
        }

        private sealed class Session : IDisposable
        {
            private BattleTipsView owner;

            public Session(BattleTipsView owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (owner == null)
                {
                    return;
                }

                owner.HideAll();
                owner = null;
            }
        }
    }
}
