using LighthouseExtends.UIComponent.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成UIのHierarchy参照を名前で解決する
    /// RectTransformとHierarchyは変更しない
    /// </summary>
    internal static class TrainingUiReferenceUtility
    {
        public const string InProgressHudContentRootName = "TrainingInProgressHud";

        /// <summary>
        /// 育成HUDのコンテンツルートを返す
        /// </summary>
        public static Transform ResolveInProgressHudContentRoot(Transform hudViewTransform)
        {
            if (hudViewTransform == null)
            {
                return null;
            }

            if (hudViewTransform.name == InProgressHudContentRootName)
            {
                return hudViewTransform;
            }

            Transform nestedRoot = hudViewTransform.Find(InProgressHudContentRootName);
            if (nestedRoot != null)
            {
                return nestedRoot;
            }

            return hudViewTransform;
        }

        /// <summary>
        /// 子孫Transformを名前で検索する
        /// </summary>
        public static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform found = FindDeepChild(child, childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// 子孫TMP_Textを名前で検索する
        /// </summary>
        public static TMP_Text FindText(Transform root, string childName)
        {
            return FindDeepChild(root, childName)?.GetComponent<TMP_Text>();
        }

        /// <summary>
        /// 子孫LHButtonを名前で検索する
        /// </summary>
        public static LHButton FindButton(Transform root, string childName)
        {
            return FindDeepChild(root, childName)?.GetComponent<LHButton>();
        }

        /// <summary>
        /// 子孫Imageを名前で検索する
        /// </summary>
        public static Image FindImage(Transform root, string childName)
        {
            return FindDeepChild(root, childName)?.GetComponent<Image>();
        }

        /// <summary>
        /// WindowPanel配下を優先して子孫Transformを検索する
        /// </summary>
        public static Transform FindWindowPanelChild(Transform windowRoot, string childName)
        {
            if (windowRoot == null)
            {
                return null;
            }

            Transform panel = windowRoot.Find("WindowPanel");
            Transform searchRoot = panel != null ? panel : windowRoot;
            return FindDeepChild(searchRoot, childName);
        }
    }
}
