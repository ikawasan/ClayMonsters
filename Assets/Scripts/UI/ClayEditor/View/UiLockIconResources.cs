using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// ロック表示用アイコンのResources読み込み
    /// </summary>
    public static class UiLockIconResources
    {
        private const string ResourcePath = "UI/Icons/UiLockIcon";
        private static Sprite cachedSprite;
        private static bool loadAttempted;

        /// <summary>
        /// ロックアイコンSpriteを返す
        /// </summary>
        public static Sprite GetSprite()
        {
            if (cachedSprite != null)
            {
                return cachedSprite;
            }

            if (loadAttempted)
            {
                return null;
            }

            loadAttempted = true;
            cachedSprite = LoadSprite();
            if (cachedSprite == null)
            {
                Debug.LogError(
                    $"[UiLockIconResources] {ResourcePath} のSprite読み込みに失敗しました"
                    + " Texture TypeがSpriteか確認してください");
            }

            return cachedSprite;
        }

        private static Sprite LoadSprite()
        {
            Sprite sprite = Resources.Load<Sprite>(ResourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            Sprite[] sprites = Resources.LoadAll<Sprite>(ResourcePath);
            if (sprites != null && sprites.Length > 0)
            {
                return sprites[0];
            }

            Texture2D texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
