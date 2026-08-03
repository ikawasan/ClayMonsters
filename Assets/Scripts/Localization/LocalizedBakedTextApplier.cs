using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Localization
{
    /// <summary>
    /// プレハブに焼き込まれたTMP文言をキーで差し替える
    /// 起動時の日本語原文を照合して対象を記憶する
    /// </summary>
    public sealed class LocalizedBakedTextApplier
    {
        private readonly List<Entry> entries = new();
        private bool captured;

        /// <summary>
        /// 原文一致の差し替え対象を登録する
        /// </summary>
        /// <param name="key">テキストキー</param>
        /// <param name="japaneseFallback">日本語原文</param>
        public void Register(string key, string japaneseFallback)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(japaneseFallback))
            {
                return;
            }

            entries.Add(new Entry(key, japaneseFallback, null));
        }

        /// <summary>
        /// ルート配下のTMPから原文一致対象を記憶する
        /// </summary>
        /// <param name="root">検索ルート</param>
        public void Capture(Transform root)
        {
            if (root == null || captured)
            {
                return;
            }

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                string current = Normalize(text.text);
                for (int e = 0; e < entries.Count; e++)
                {
                    Entry entry = entries[e];
                    if (entry.Target != null)
                    {
                        continue;
                    }

                    if (current == Normalize(entry.JapaneseFallback))
                    {
                        entries[e] = new Entry(entry.Key, entry.JapaneseFallback, text);
                        break;
                    }
                }
            }

            captured = true;
        }

        /// <summary>
        /// 記憶済みTMPへ現在言語の文言を適用する
        /// </summary>
        public void Apply()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Target == null)
                {
                    continue;
                }

                LocalizedFont.SetText(
                    entry.Target,
                    LocalizedText.GetOrFallback(entry.Key, entry.JapaneseFallback));
                LocalizedFixedChromeLabel.RefreshHorizontalLayoutWidth(entry.Target);
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\r\n", "\n").Trim();
        }

        private readonly struct Entry
        {
            public Entry(string key, string japaneseFallback, TMP_Text target)
            {
                Key = key;
                JapaneseFallback = japaneseFallback;
                Target = target;
            }

            public string Key { get; }
            public string JapaneseFallback { get; }
            public TMP_Text Target { get; }
        }
    }
}
