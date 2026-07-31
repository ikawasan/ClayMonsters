using Extensions;
using LighthouseExtends.UIComponent.Button;
using SaveData;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI.SkillTree.View
{
    /// <summary>
    /// スキルツリー1ノードの表示
    /// </summary>
    public sealed class SkillTreeNodeView : MonoBehaviour
    {
        private static readonly Color SelectedOutlineColor = new(1f, 0.92f, 0.28f, 1f);
        private static readonly Vector2 SelectedOutlineDistance = new(5f, 5f);

        [SerializeField] private SkillTreeNodeId nodeId;
        [SerializeField] private LHButton button;
        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image checkmarkImage;

        private Outline selectionOutline;
        private bool isSelected;

        /// <summary>
        /// ノードID
        /// </summary>
        public SkillTreeNodeId NodeId => nodeId;

        /// <summary>
        /// 配置用RectTransform
        /// </summary>
        public RectTransform RectTransform => (RectTransform)transform;

        private void Awake()
        {
            HideFrameBackground();
        }

        /// <summary>
        /// 表示状態を更新する
        /// </summary>
        /// <param name="level">現在レベル</param>
        /// <param name="prerequisitesMet">前提達成か</param>
        /// <param name="selected">選択中か</param>
        public void Bind(int level, bool prerequisitesMet, bool selected)
        {
            HideFrameBackground();

            bool unlocked = level > 0;
            bool visible = unlocked || prerequisitesMet;
            ApplyVisibility(visible, unlocked);
            SetSelected(visible && selected);

            if (button != null)
            {
                button.interactable = visible;
            }
        }

        /// <summary>
        /// 選択表示を切り替える
        /// </summary>
        /// <param name="selected">選択中か</param>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            ApplySelectionOutline();
        }

        /// <summary>
        /// ノード選択を購読する
        /// </summary>
        /// <param name="action">選択時</param>
        public IDisposable SubscribeClick(UnityAction action)
        {
            if (button == null)
            {
                Debug.LogError("[SkillTreeNodeView] buttonが未配線です", this);
                return new EmptyDisposable();
            }

            return button.SubscribeOnClick(action);
        }

        private void HideFrameBackground()
        {
            if (frameImage == null)
            {
                return;
            }

            // 後ろのボタン枠画像だけ非表示クリック判定は残す
            frameImage.color = new Color(1f, 1f, 1f, 0f);
            frameImage.raycastTarget = true;
        }

        private void ApplyVisibility(bool visible, bool unlocked)
        {
            if (frameImage != null)
            {
                frameImage.raycastTarget = visible;
            }

            if (checkmarkImage != null)
            {
                checkmarkImage.enabled = visible && unlocked;
            }

            if (iconImage == null)
            {
                Debug.LogError("[SkillTreeNodeView] iconImageが未配線です", this);
                return;
            }

            if (!visible)
            {
                iconImage.enabled = false;
                return;
            }

            Sprite sprite = SkillTreeIconCatalog.Resolve(nodeId);
            iconImage.sprite = sprite;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.enabled = sprite != null;
            iconImage.color = Color.white;
        }

        private void ApplySelectionOutline()
        {
            Graphic outlineTarget = iconImage != null ? iconImage : (Graphic)frameImage;
            if (outlineTarget == null)
            {
                return;
            }

            if (selectionOutline == null || selectionOutline.gameObject != outlineTarget.gameObject)
            {
                selectionOutline = outlineTarget.GetComponent<Outline>();
                if (selectionOutline == null)
                {
                    selectionOutline = outlineTarget.gameObject.AddComponent<Outline>();
                }
            }

            selectionOutline.effectColor = SelectedOutlineColor;
            selectionOutline.effectDistance = SelectedOutlineDistance;
            selectionOutline.useGraphicAlpha = true;
            selectionOutline.enabled = isSelected && outlineTarget.enabled;
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
