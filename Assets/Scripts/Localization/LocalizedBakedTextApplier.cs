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
        private Transform captureRoot;

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

            entries.Add(new Entry(key, japaneseFallback));
        }

        /// <summary>
        /// ルート配下のTMPから原文一致対象を記憶する
        /// </summary>
        /// <param name="root">検索ルート</param>
        public void Capture(Transform root)
        {
            if (root == null)
            {
                return;
            }

            captureRoot = root;
            BindMissingTargets();
        }

        /// <summary>
        /// 記憶済みTMPへ現在言語の文言を適用する
        /// </summary>
        public void Apply()
        {
            if (captureRoot != null)
            {
                BindMissingTargets();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                string text = LocalizedText.GetOrFallback(entry.Key, entry.JapaneseFallback);
                for (int t = 0; t < entry.Targets.Count; t++)
                {
                    TMP_Text target = entry.Targets[t];
                    if (target == null)
                    {
                        continue;
                    }

                    LocalizedFont.SetText(target, text);
                    LocalizedFixedChromeLabel.RefreshHorizontalLayoutWidth(target);
                }
            }
        }

        private void BindMissingTargets()
        {
            if (captureRoot == null || entries.Count == 0)
            {
                return;
            }

            TMP_Text[] texts = captureRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                string current = Normalize(text.text);
                if (string.IsNullOrEmpty(current))
                {
                    continue;
                }

                for (int e = 0; e < entries.Count; e++)
                {
                    Entry entry = entries[e];
                    if (current != Normalize(entry.JapaneseFallback))
                    {
                        continue;
                    }

                    if (entry.Targets.Contains(text))
                    {
                        continue;
                    }

                    entry.Targets.Add(text);
                    break;
                }
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

        private sealed class Entry
        {
            public Entry(string key, string japaneseFallback)
            {
                Key = key;
                JapaneseFallback = japaneseFallback;
                Targets = new List<TMP_Text>(4);
            }

            public string Key { get; }

            public string JapaneseFallback { get; }

            public List<TMP_Text> Targets { get; }
        }
    }
}
