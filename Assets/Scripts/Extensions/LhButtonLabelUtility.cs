using TMPro;

namespace Extensions
{
    /// <summary>
    /// TMP_Textへのラベル設定ヘルパー
    /// </summary>
    public static class LhButtonLabelUtility
    {
        /// <summary>
        /// ラベルテキストを設定する
        /// </summary>
        /// <param name="text">対象TMP_Text</param>
        /// <param name="label">表示テキスト</param>
        public static void SetLabel(TMP_Text text, string label)
        {
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
