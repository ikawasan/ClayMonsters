using Battle;
using ClayEditor.Rigging;
using Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// セーブスロット向け攻撃部位情報UIを組み立てる
    /// 使用部位と破壊部位のラベル付きアイコンを横並びで表示する
    /// </summary>
    public static class MoveAttackPartInfoView
    {
        private const string RequiredPartLabel = "使用部位:";
        private const string TargetPartLabel = "破壊部位:";

        private static readonly Color LabelColor = new Color(0.78f, 0.82f, 0.88f, 1f);

        /// <summary>
        /// 使用部位と破壊部位のラベル付きアイコン行を親へ生成する
        /// </summary>
        public static RectTransform Create(
            Transform parent,
            float iconSize,
            float labelFontSize,
            MotionType motion,
            bool largeLayout)
        {
            var rowObject = new GameObject(
                "PartInfoRow",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            rowObject.transform.SetParent(parent, false);

            float rowHeight = Mathf.Max(ResolveTextHeight(labelFontSize), iconSize);
            LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
            rowLayout.minHeight = rowHeight;
            rowLayout.preferredHeight = rowHeight;
            rowLayout.flexibleWidth = 0f;
            rowLayout.flexibleHeight = 0f;

            HorizontalLayoutGroup rowGroup = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowGroup.spacing = largeLayout ? 8f : 6f;
            rowGroup.childAlignment = TextAnchor.MiddleLeft;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandWidth = false;
            rowGroup.childForceExpandHeight = false;

            CreatePartGroup(rowObject.transform, RequiredPartLabel, iconSize, labelFontSize, rowHeight, motion, required: true);
            CreatePartGroup(rowObject.transform, TargetPartLabel, iconSize, labelFontSize, rowHeight, motion, required: false);

            return rowObject.GetComponent<RectTransform>();
        }

        private static void CreatePartGroup(
            Transform parent,
            string label,
            float iconSize,
            float labelFontSize,
            float rowHeight,
            MotionType motion,
            bool required)
        {
            var groupObject = new GameObject(
                required ? "RequiredPartGroup" : "TargetPartGroup",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            groupObject.transform.SetParent(parent, false);

            LayoutElement groupLayout = groupObject.GetComponent<LayoutElement>();
            groupLayout.flexibleWidth = 0f;
            groupLayout.flexibleHeight = 0f;
            groupLayout.minHeight = rowHeight;
            groupLayout.preferredHeight = rowHeight;

            HorizontalLayoutGroup group = groupObject.GetComponent<HorizontalLayoutGroup>();
            group.spacing = 3f;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            TMP_Text labelText = CreateLabel(groupObject.transform, label, labelFontSize, rowHeight);
            LayoutElement labelLayout = labelText.GetComponent<LayoutElement>();
            labelLayout.flexibleWidth = 0f;
            labelLayout.flexibleHeight = 0f;

            if (required)
            {
                MoveTargetPartIconView.CreateRequired(groupObject.transform, iconSize, motion);
            }
            else
            {
                MoveTargetPartIconView.CreateTarget(groupObject.transform, iconSize, motion);
            }
        }

        private static TMP_Text CreateLabel(Transform parent, string text, float fontSize, float preferredHeight)
        {
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            LayoutElement textLayout = textObject.GetComponent<LayoutElement>();
            textLayout.flexibleWidth = 0f;
            textLayout.flexibleHeight = 0f;
            textLayout.minHeight = preferredHeight;
            textLayout.preferredHeight = preferredHeight;

            TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
            AppTmpFontUtility.ApplyDefaultFont(label);
            label.fontSize = fontSize;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = Vector4.zero;
            label.text = text;
            return label;
        }

        private static float ResolveTextHeight(float fontSize)
        {
            return Mathf.Ceil(fontSize * 1.5f);
        }
    }
}
