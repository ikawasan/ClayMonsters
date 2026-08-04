namespace Localization
{
    /// <summary>
    /// 敵カタログ個体名の言語別表示名を解決する
    /// </summary>
    public static class EnemyDisplayName
    {
        /// <summary>
        /// 敵スロット用TextTableキーを返す
        /// </summary>
        /// <param name="slotIndex">敵スロット番号</param>
        /// <returns>キー</returns>
        public static string KeyForSlot(int slotIndex)
        {
            return GameTextKeys.EnemyNamePrefix + slotIndex.ToString();
        }

        /// <summary>
        /// 敵スロットの表示名を返す
        /// modelNameを日本語フォールバックとする
        /// </summary>
        /// <param name="slotIndex">敵スロット番号</param>
        /// <param name="fallbackModelName">セーブのmodelName</param>
        /// <returns>表示名</returns>
        public static string Resolve(int slotIndex, string fallbackModelName)
        {
            if (slotIndex < 0)
            {
                return fallbackModelName ?? string.Empty;
            }

            string fallback = fallbackModelName ?? string.Empty;
            return LocalizedText.GetOrFallback(KeyForSlot(slotIndex), fallback);
        }
    }
}
