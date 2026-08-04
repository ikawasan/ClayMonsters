using System;
using System.Collections.Generic;
using UnityEngine;

namespace Localization
{
    /// <summary>
    /// 敵1体分の言語別表示名
    /// </summary>
    [Serializable]
    public sealed class EnemyNameLocalizationEntry
    {
        /// <summary>
        /// 敵スロット番号
        /// </summary>
        public int slotIndex;

        /// <summary>
        /// Inspector用メモ
        /// </summary>
        public string editorNote;

        /// <summary>
        /// 日本語
        /// </summary>
        public string japanese;

        /// <summary>
        /// 英語
        /// </summary>
        public string english;

        /// <summary>
        /// 簡体字中国語
        /// </summary>
        public string chineseSimplified;

        /// <summary>
        /// 繁体字中国語
        /// </summary>
        public string chineseTraditional;

        /// <summary>
        /// 韓国語
        /// </summary>
        public string korean;

        /// <summary>
        /// ドイツ語
        /// </summary>
        public string german;

        /// <summary>
        /// フランス語
        /// </summary>
        public string french;

        /// <summary>
        /// スペイン語
        /// </summary>
        public string spanish;

        /// <summary>
        /// ブラジルポルトガル語
        /// </summary>
        public string portugueseBrazil;

        /// <summary>
        /// 言語コードに対応する名前を返す
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        /// <returns>表示名</returns>
        public string GetName(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                return japanese ?? string.Empty;
            }

            if (string.Equals(languageCode, GameLanguageCodes.Japanese, StringComparison.Ordinal))
            {
                return japanese ?? string.Empty;
            }

            if (string.Equals(languageCode, GameLanguageCodes.English, StringComparison.Ordinal))
            {
                return english ?? string.Empty;
            }

            if (string.Equals(languageCode, GameLanguageCodes.ChineseSimplified, StringComparison.Ordinal))
            {
                return chineseSimplified ?? string.Empty;
            }

            if (string.Equals(languageCode, GameLanguageCodes.ChineseTraditional, StringComparison.Ordinal))
            {
                return chineseTraditional ?? string.Empty;
            }

            if (string.Equals(languageCode, GameLanguageCodes.Korean, StringComparison.Ordinal))
            {
                return korean ?? string.Empty;
            }

            if (string.Equals(languageCode, GameLanguageCodes.German, StringComparison.Ordinal))
            {
                return german ?? string.Empty;
            }

            if (string.Equals(languageCode, GameLanguageCodes.French, StringComparison.Ordinal))
            {
                return french ?? string.Empty;
            }

            if (string.Equals(languageCode, GameLanguageCodes.Spanish, StringComparison.Ordinal))
            {
                return spanish ?? string.Empty;
            }

            if (string.Equals(languageCode, GameLanguageCodes.PortugueseBrazil, StringComparison.Ordinal))
            {
                return portugueseBrazil ?? string.Empty;
            }

            return japanese ?? string.Empty;
        }

        /// <summary>
        /// 言語コードに対応する名前を設定する
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        /// <param name="value">表示名</param>
        public void SetName(string languageCode, string value)
        {
            string safe = value ?? string.Empty;
            if (string.Equals(languageCode, GameLanguageCodes.Japanese, StringComparison.Ordinal))
            {
                japanese = safe;
                return;
            }

            if (string.Equals(languageCode, GameLanguageCodes.English, StringComparison.Ordinal))
            {
                english = safe;
                return;
            }

            if (string.Equals(languageCode, GameLanguageCodes.ChineseSimplified, StringComparison.Ordinal))
            {
                chineseSimplified = safe;
                return;
            }

            if (string.Equals(languageCode, GameLanguageCodes.ChineseTraditional, StringComparison.Ordinal))
            {
                chineseTraditional = safe;
                return;
            }

            if (string.Equals(languageCode, GameLanguageCodes.Korean, StringComparison.Ordinal))
            {
                korean = safe;
                return;
            }

            if (string.Equals(languageCode, GameLanguageCodes.German, StringComparison.Ordinal))
            {
                german = safe;
                return;
            }

            if (string.Equals(languageCode, GameLanguageCodes.French, StringComparison.Ordinal))
            {
                french = safe;
                return;
            }

            if (string.Equals(languageCode, GameLanguageCodes.Spanish, StringComparison.Ordinal))
            {
                spanish = safe;
                return;
            }

            if (string.Equals(languageCode, GameLanguageCodes.PortugueseBrazil, StringComparison.Ordinal))
            {
                portugueseBrazil = safe;
            }
        }
    }

    /// <summary>
    /// 敵モンスター名の言語別定義
    /// Inspectorで編集しEditorからUiTSVへ書き出す
    /// </summary>
    [CreateAssetMenu(
        fileName = "EnemyNameLocalizationCatalog",
        menuName = "ClayMonsters/Localization/Enemy Name Catalog")]
    public sealed class EnemyNameLocalizationCatalog : ScriptableObject
    {
        /// <summary>
        /// 敵スロット数
        /// </summary>
        public const int SlotCount = 25;

        [SerializeField]
        private List<EnemyNameLocalizationEntry> entries = new List<EnemyNameLocalizationEntry>(SlotCount);

        /// <summary>
        /// エントリ一覧
        /// </summary>
        public IReadOnlyList<EnemyNameLocalizationEntry> Entries => entries;

        /// <summary>
        /// スロット数を揃えて欠番を埋める
        /// </summary>
        public void EnsureSlotEntries()
        {
            if (entries == null)
            {
                entries = new List<EnemyNameLocalizationEntry>(SlotCount);
            }

            var bySlot = new Dictionary<int, EnemyNameLocalizationEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                EnemyNameLocalizationEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                bySlot[entry.slotIndex] = entry;
            }

            entries.Clear();
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (!bySlot.TryGetValue(slot, out EnemyNameLocalizationEntry entry) || entry == null)
                {
                    entry = new EnemyNameLocalizationEntry
                    {
                        slotIndex = slot,
                        editorNote = string.Empty,
                        japanese = string.Empty,
                        english = string.Empty,
                        chineseSimplified = string.Empty,
                        chineseTraditional = string.Empty,
                        korean = string.Empty,
                        german = string.Empty,
                        french = string.Empty,
                        spanish = string.Empty,
                        portugueseBrazil = string.Empty,
                    };
                }
                else
                {
                    entry.slotIndex = slot;
                }

                entries.Add(entry);
            }
        }

        /// <summary>
        /// スロット番号のエントリを返す
        /// </summary>
        /// <param name="slotIndex">スロット番号</param>
        /// <returns>エントリ(無いときnull)</returns>
        public EnemyNameLocalizationEntry FindEntry(int slotIndex)
        {
            if (entries == null)
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                EnemyNameLocalizationEntry entry = entries[i];
                if (entry != null && entry.slotIndex == slotIndex)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
