using Battle.Interface;
using Extensions;
using LighthouseExtends.TextTable;
using Localization;
using R3;
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
    public sealed class BattleTipsView : MonoBehaviour, IBattleTipsView, ILanguageAwareUi
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
        private IDisposable languageSubscription;

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
            SubscribeLanguageChange();
            HideAll();
        }


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyGuideTexts();
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

            LhButtonLabelUtility.SetLabel(
                closeButton,
                LocalizedText.GetOrFallback(GameTextKeys.CommonClose, "閉じる"));

            if (tipsCanvas != null)
            {
                TMP_Text title = FindChildTmp(tipsCanvas.transform, "Title");
                if (title == null)
                {
                    title = FindChildTmp(tipsCanvas.transform, "TitleText");
                }

                if (title != null)
                {
                    LocalizedFont.SetText(
                        title,
                        LocalizedText.GetOrFallback(GameTextKeys.BattleTipsTitle, "戦闘チュートリアル"));
                }
            }
        }

        private void SubscribeLanguageChange()
        {
            ITextTableService textTableService = TextTableService.Instance;
            if (textTableService == null)
            {
                return;
            }

            languageSubscription?.Dispose();
            languageSubscription = textTableService.CurrentLanguage.Subscribe(_ => ApplyGuideTexts());
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
            languageSubscription?.Dispose();
            languageSubscription = null;

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
            ApplyGuideTexts();
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

        /// <summary>
        /// Tips本文を検査用に開く
        /// 戦闘セッションなしでも表示する
        /// </summary>
        public void ShowOpenTipsForInspection()
        {
            isFeatureAvailable = true;
            isCombatSessionActive = true;
            ApplyGuideTexts();
            isOpen = true;
            SetHintVisible(true);
            SetTipsVisible(true);
            if (this is Localization.ILanguageAwareUi)
            {
                RefreshLocalizedUi();
            }
        }

        private void OpenTips()
        {
            if (!isFeatureAvailable || !isCombatSessionActive)
            {
                return;
            }

            ApplyGuideTexts();
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
