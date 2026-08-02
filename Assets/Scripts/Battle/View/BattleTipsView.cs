using Battle.Interface;
using Extensions;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Battle.View
{
    /// <summary>
    /// 戦闘チュートリアルTipsと右端ヒントの表示
    /// 入力説明はアイコン付き文言を適用する
    /// </summary>
    public sealed class BattleTipsView : MonoBehaviour, IBattleTipsView
    {
        [Header("参照")]
        [SerializeField] private Canvas tipsCanvas;
        [SerializeField] private Canvas hintCanvas;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text tipsBodyText;
        [SerializeField] private TMP_Text hintText;

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

            ApplyGuideTexts();
            HideAll();
        }

        private void ApplyGuideTexts()
        {
            ResolveTextsIfNeeded();
            if (tipsBodyText != null)
            {
                InputIconTmpUtility.ApplySpriteAsset(tipsBodyText);
                tipsBodyText.text = InputGuideTexts.BattleTipsBody;
            }
            else
            {
                Debug.LogError("[BattleTipsView] tipsBodyTextが未配線です", this);
            }

            if (hintText != null)
            {
                InputIconTmpUtility.ApplySpriteAsset(hintText);
                hintText.text = InputGuideTexts.BattleTipsEscHint;
            }
            else
            {
                Debug.LogError("[BattleTipsView] hintTextが未配線です", this);
            }
        }

        private void ResolveTextsIfNeeded()
        {
            if (tipsBodyText == null && tipsCanvas != null)
            {
                tipsBodyText = FindChildTmp(tipsCanvas.transform, "Body");
            }

            if (hintText == null && hintCanvas != null)
            {
                hintText = FindChildTmp(hintCanvas.transform, "HintText");
            }
        }

        private static TMP_Text FindChildTmp(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].gameObject.name == objectName)
                {
                    return texts[i];
                }
            }

            return null;
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
