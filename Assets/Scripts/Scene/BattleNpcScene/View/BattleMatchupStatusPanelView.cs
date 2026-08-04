using System;
using Battle;
using Extensions;
using Localization;
using TMPro;
using UI.ClayEditor.View;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// 対戦紹介中に両者のステータス数値と五角形レーダーを表示する
    /// </summary>
    public sealed class BattleMatchupStatusPanelView : MonoBehaviour, ILanguageAwareUi
    {
        // HP攻撃防御速さ命中の表示上限(BattleStatusBalanceと揃える)
        private static readonly float[] DisplayMaxValues = { 999f, 999f, 999f, 999f, 999f };

        [Header("表示制御")]
        [SerializeField] private Canvas buttonCanvas;
        [SerializeField] private Canvas detailCanvas;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;

        [Header("プレイヤー")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private BattleStatusRadarChart playerRadar;

        [Header("敵")]
        [SerializeField] private TMP_Text enemyNameText;
        [SerializeField] private BattleStatusRadarChart enemyRadar;

        private bool isBound;
        private bool didDetachCanvases;
        private bool isButtonExpectedVisible;
        private bool isDetailVisible;
        private Action<bool> detailVisibilityListener;
        private BattleUnit boundPlayer;
        private BattleUnit boundEnemy;

        private void Awake()
        {
            ValidateRefs();
            EnsureCanvasLayout();
            openButton?.EnsureUiSoundFeedback();
            closeButton?.EnsureUiSoundFeedback();
            ApplyLocalizedChrome();
            if (openButton != null)
            {
                openButton.onClick.AddListener(ShowDetail);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HideDetail);
            }

            SetButtonVisible(false);
            HideDetail();
        }

        private void OnDestroy()
        {
            if (openButton != null)
            {
                openButton.onClick.RemoveListener(ShowDetail);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HideDetail);
            }
        }

        /// <summary>
        /// 対戦両者のステータスを取り込む
        /// </summary>
        /// <param name="player">プレイヤー</param>
        /// <param name="enemy">敵</param>
        public void Bind(BattleUnit player, BattleUnit enemy)
        {
            ValidateRefs();
            EnsureCanvasLayout();
            boundPlayer = player;
            boundEnemy = enemy;
            ApplyLocalizedChrome();
            ApplySide(
                player,
                playerNameText,
                playerRadar,
                new Color(0.35f, 0.78f, 1f, 0.5f));
            ApplySide(
                enemy,
                enemyNameText,
                enemyRadar,
                new Color(1f, 0.45f, 0.35f, 0.5f));
            isBound = true;
        }


        /// <inheritdoc/>
        public void RefreshLocalizedUi()
        {
            ApplyLocalizedChrome();
            if (!isBound)
            {
                return;
            }

            ApplySide(
                boundPlayer,
                playerNameText,
                playerRadar,
                new Color(0.35f, 0.78f, 1f, 0.5f));
            ApplySide(
                boundEnemy,
                enemyNameText,
                enemyRadar,
                new Color(1f, 0.45f, 0.35f, 0.5f));
        }

        private bool chromeOriginalsCaptured;
        private string openOriginal = "ステータス";
        private string closeOriginal = "閉じる";

        private void ApplyLocalizedChrome()
        {
            CaptureChromeOriginalsIfNeeded();
            LhButtonLabelUtility.SetLabel(
                openButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.BattleMatchupStatus, openOriginal));
            LhButtonLabelUtility.SetLabel(
                closeButton,
                SceneLocalizedLabel.Resolve(GameTextKeys.CommonClose, closeOriginal));
        }

        private void CaptureChromeOriginalsIfNeeded()
        {
            if (chromeOriginalsCaptured)
            {
                return;
            }

            openOriginal = SceneLocalizedLabel.Capture(openButton, openOriginal);
            closeOriginal = SceneLocalizedLabel.Capture(closeButton, closeOriginal);
            chromeOriginalsCaptured = true;
        }

        /// <summary>
        /// 詳細パネル開閉時に背面UIの表示制御へ通知する
        /// </summary>
        /// <param name="listener">trueで詳細表示中</param>
        public void SetDetailVisibilityListener(Action<bool> listener)
        {
            detailVisibilityListener = listener;
        }

        /// <summary>
        /// 対戦紹介中のステータスボタン表示を切り替える
        /// </summary>
        /// <param name="visible">表示するか</param>
        public void SetButtonVisible(bool visible)
        {
            isButtonExpectedVisible = visible;
            EnsureCanvasLayout();
            if (!visible)
            {
                HideDetail();
            }

            ApplyButtonCanvasVisible(visible && !isDetailVisible);

            if (openButton != null)
            {
                openButton.interactable = visible && !isDetailVisible;
            }
        }

        /// <summary>
        /// 詳細パネルを閉じる
        /// </summary>
        public void HideDetail()
        {
            if (!isDetailVisible && (detailCanvas == null || !detailCanvas.enabled))
            {
                isDetailVisible = false;
                return;
            }

            if (detailCanvas != null)
            {
                detailCanvas.enabled = false;
            }

            isDetailVisible = false;
            ApplyButtonCanvasVisible(isButtonExpectedVisible);
            if (openButton != null)
            {
                openButton.interactable = isButtonExpectedVisible;
            }

            detailVisibilityListener?.Invoke(false);
        }

        /// <summary>
        /// 詳細パネルを開く
        /// </summary>
        public void ShowDetail()
        {
            if (!isBound)
            {
                Debug.LogWarning("[BattleMatchupStatusPanelView] Bind前に詳細表示が要求されました");
                return;
            }

            EnsureCanvasLayout();
            if (detailCanvas == null)
            {
                return;
            }

            detailCanvas.overrideSorting = true;
            if (detailCanvas.sortingOrder < 1100)
            {
                detailCanvas.sortingOrder = 1100;
            }

            // 背面の開くボタンを隠し詳細のみ見せる
            ApplyButtonCanvasVisible(false);
            if (openButton != null)
            {
                openButton.interactable = false;
            }

            detailCanvas.enabled = true;
            isDetailVisible = true;
            detailVisibilityListener?.Invoke(true);
        }

        private void ApplyButtonCanvasVisible(bool visible)
        {
            if (buttonCanvas != null)
            {
                buttonCanvas.enabled = visible;
            }
            else if (openButton != null)
            {
                CanvasVisibilityUtility.SetUiVisible(openButton.gameObject, visible);
            }
        }

        private void EnsureCanvasLayout()
        {
            // 親Overlayのscale0の影響を受けないようシーンルートへ移す
            Transform sceneRoot = transform.root;
            DetachCanvas(buttonCanvas, sceneRoot, 1050);
            DetachCanvas(detailCanvas, sceneRoot, 1100);
            didDetachCanvases = true;
        }

        private void DetachCanvas(Canvas canvas, Transform sceneRoot, int sortingOrder)
        {
            if (canvas == null)
            {
                return;
            }

            Transform canvasTransform = canvas.transform;
            if (!didDetachCanvases && sceneRoot != null && canvasTransform.parent != sceneRoot)
            {
                canvasTransform.SetParent(sceneRoot, false);
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            if (canvasTransform is RectTransform rect)
            {
                // ScreenSpaceOverlayのルート相当として左下原点にする
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
            }

            if (canvas.TryGetComponent(out CanvasScaler scaler))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600f, 900f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            ModelSaveSlotScrollListView.FixCanvasScaleHierarchy(canvas);
            canvasTransform.SetAsLastSibling();
        }

        private void ApplySide(
            BattleUnit unit,
            TMP_Text nameText,
            BattleStatusRadarChart radar,
            Color fillColor)
        {
            if (nameText != null)
            {
                nameText.text = unit != null ? unit.Name : string.Empty;
            }

            if (radar == null)
            {
                return;
            }

            int[] values =
            {
                unit != null ? unit.MaxHp : 0,
                unit != null ? unit.Attack : 0,
                unit != null ? unit.Defense : 0,
                unit != null ? unit.Speed : 0,
                unit != null ? unit.Hit : 0
            };

            radar.color = Color.white;
            radar.SetColors(fillColor, new Color(fillColor.r, fillColor.g, fillColor.b, 0.95f));
            radar.SetStatusValues(values, DisplayMaxValues);
        }

        private void ValidateRefs()
        {
            if (buttonCanvas == null)
            {
                Debug.LogError("[BattleMatchupStatusPanelView] buttonCanvasが未配線です", this);
            }

            if (detailCanvas == null)
            {
                Debug.LogError("[BattleMatchupStatusPanelView] detailCanvasが未配線です", this);
            }

            if (openButton == null)
            {
                Debug.LogError("[BattleMatchupStatusPanelView] openButtonが未配線です", this);
            }

            if (closeButton == null)
            {
                Debug.LogError("[BattleMatchupStatusPanelView] closeButtonが未配線です", this);
            }

            if (playerNameText == null || playerRadar == null)
            {
                Debug.LogError("[BattleMatchupStatusPanelView] プレイヤー側参照が未配線です", this);
            }

            if (enemyNameText == null || enemyRadar == null)
            {
                Debug.LogError("[BattleMatchupStatusPanelView] 敵側参照が未配線です", this);
            }
        }
    }
}
