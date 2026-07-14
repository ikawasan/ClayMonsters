using UnityEngine;

namespace ClayEditor.Paint
{
    /// <summary>
    /// ペイント色の表示色と保存色を変換する
    /// </summary>
    public static class PaintColorUtility
    {
        /// <summary>
        /// UIカラーピッカー等の表示色をボクセル保存用のリニア色へ変換する
        /// </summary>
        /// <param name="displayColor">表示色</param>
        /// <returns>保存用リニア色</returns>
        public static Color ToStorageColor(Color displayColor)
        {
            Color linear = displayColor.linear;
            linear.r = Mathf.Clamp01(linear.r);
            linear.g = Mathf.Clamp01(linear.g);
            linear.b = Mathf.Clamp01(linear.b);
            linear.a = 1f;
            return linear;
        }

        /// <summary>
        /// マテリアルへ設定するリニア色を返す
        /// </summary>
        /// <param name="displayColor">表示色</param>
        /// <returns>マテリアル用リニア色</returns>
        public static Color ToMaterialColor(Color displayColor)
        {
            return ToStorageColor(displayColor);
        }
    }
}
