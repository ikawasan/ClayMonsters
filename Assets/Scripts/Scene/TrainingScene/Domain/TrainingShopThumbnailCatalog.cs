using System.Collections.Generic;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 売店アイテムのサムネイル解決
    /// </summary>
    public static class TrainingShopThumbnailCatalog
    {
        private const string ResourcesRoot = "Image/Training/Items/";
        private static readonly Dictionary<string, Sprite> cache =
            new Dictionary<string, Sprite>();

        /// <summary>
        /// 商品IDからサムネイルを取得する
        /// </summary>
        /// <param name="itemId">商品ID</param>
        public static Sprite Resolve(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            if (cache.TryGetValue(itemId, out Sprite cached) && cached != null)
            {
                return cached;
            }

            string path = ResourcesRoot + itemId;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            if (sprite == null)
            {
                Debug.LogError(
                    $"[TrainingShopThumbnailCatalog] サムネイルが見つかりません path={path}");
                return null;
            }

            cache[itemId] = sprite;
            return sprite;
        }
    }
}
