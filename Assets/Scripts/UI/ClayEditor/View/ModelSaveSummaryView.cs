using Extensions;
using TMPro;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 属性・パラメータ・攻撃のサマリーをTMPで表示する
    /// 未設定なら親RectTransform配下に表示用テキストを自動生成する
    /// </summary>
    public sealed class ModelSaveSummaryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private float fontSize = 20f;
        [SerializeField] private Color textColor = new Color(0.92f, 0.94f, 0.98f, 1f);

        private void Awake()
        {
            if (summaryText == null)
            {
                summaryText = GetComponentInChildren<TMP_Text>(true);
            }
        }

        /// <summary>
        /// 詳細サマリーテキストを表示する
        /// </summary>
        /// <param name="text">表示文</param>
        public void SetText(string text)
        {
            if (summaryText == null)
            {
                return;
            }

            summaryText.text = string.IsNullOrEmpty(text) ? string.Empty : text;
            summaryText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>
        /// 表示をクリアする
        /// </summary>
        public void Clear()
        {
            SetText(string.Empty);
        }
    }
}
