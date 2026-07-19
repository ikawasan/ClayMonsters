using UnityEngine;

namespace Extensions
{
    /// <summary>
    /// PNGバイト列から実行時サムネイルを生成する
    /// </summary>
    public static class RuntimeThumbnailUtility
    {
        /// <summary>
        /// PNGからTexture2DとSpriteを生成する
        /// </summary>
        /// <param name="thumbnailPng">PNGバイト列</param>
        /// <param name="texture">生成したTexture2D</param>
        /// <param name="sprite">生成したSprite</param>
        /// <returns>生成に成功したらtrue</returns>
        public static bool TryCreate(
            byte[] thumbnailPng,
            out Texture2D texture,
            out Sprite sprite)
        {
            texture = null;
            sprite = null;
            if (thumbnailPng == null || thumbnailPng.Length == 0)
            {
                return false;
            }

            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(thumbnailPng))
            {
                Object.Destroy(texture);
                texture = null;
                return false;
            }

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            return true;
        }

        /// <summary>
        /// 実行時に生成したSpriteとTexture2Dを破棄する
        /// </summary>
        /// <param name="sprite">破棄するSprite</param>
        /// <param name="texture">破棄するTexture2D</param>
        public static void Destroy(ref Sprite sprite, ref Texture2D texture)
        {
            if (sprite != null)
            {
                Object.Destroy(sprite);
                sprite = null;
            }

            if (texture != null)
            {
                Object.Destroy(texture);
                texture = null;
            }
        }
    }
}
