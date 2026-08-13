using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Scene.TitleScene
{
    /// <summary>
    /// デスクトップペット起動用に選ばれた未育成スロットを永続化する
    /// </summary>
    public static class DesktopPetLaunchRequest
    {
        private const string EnabledKey = "DesktopPet.Enabled";
        private const string SlotIndexKey = "DesktopPet.SlotIndex";
        private const string SlotIndicesKey = "DesktopPet.SlotIndices";

        /// <summary>
        /// 起動要求があるか
        /// </summary>
        public static bool HasPending => PlayerPrefs.GetInt(EnabledKey, 0) == 1;

        /// <summary>
        /// 選択済みスロット番号一覧
        /// </summary>
        public static IReadOnlyList<int> PendingSlotIndices
        {
            get
            {
                if (!HasPending)
                {
                    return System.Array.Empty<int>();
                }

                string packed = PlayerPrefs.GetString(SlotIndicesKey, string.Empty);
                if (!string.IsNullOrEmpty(packed))
                {
                    return ParsePacked(packed);
                }

                int legacy = PlayerPrefs.GetInt(SlotIndexKey, -1);
                return legacy >= 0 ? new[] { legacy } : System.Array.Empty<int>();
            }
        }

        /// <summary>
        /// デスクトップペット起動要求を保存する
        /// </summary>
        /// <param name="slotIndices">未育成スロット番号一覧</param>
        public static void SetPending(IReadOnlyList<int> slotIndices)
        {
            if (slotIndices == null || slotIndices.Count == 0)
            {
                Clear();
                return;
            }

            StringBuilder builder = new StringBuilder(32);
            int written = 0;
            for (int i = 0; i < slotIndices.Count; i++)
            {
                int slotIndex = slotIndices[i];
                if (slotIndex < 0)
                {
                    continue;
                }

                if (written > 0)
                {
                    builder.Append(',');
                }

                builder.Append(slotIndex.ToString(CultureInfo.InvariantCulture));
                written++;
            }

            if (written == 0)
            {
                Clear();
                return;
            }

            PlayerPrefs.SetInt(EnabledKey, 1);
            PlayerPrefs.SetString(SlotIndicesKey, builder.ToString());
            PlayerPrefs.SetInt(SlotIndexKey, slotIndices[0]);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// デスクトップペット起動要求を消す
        /// </summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(EnabledKey);
            PlayerPrefs.DeleteKey(SlotIndexKey);
            PlayerPrefs.DeleteKey(SlotIndicesKey);
            PlayerPrefs.Save();
        }

        private static int[] ParsePacked(string packed)
        {
            string[] parts = packed.Split(',');
            List<int> values = new List<int>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    && value >= 0
                    && !values.Contains(value))
                {
                    values.Add(value);
                }
            }

            return values.ToArray();
        }
    }
}
