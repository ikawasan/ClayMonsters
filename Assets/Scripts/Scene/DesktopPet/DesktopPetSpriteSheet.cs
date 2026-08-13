using UnityEngine;

namespace Scene.DesktopPet
{
    /// <summary>
    /// デスクトップペットの向き
    /// </summary>
    public enum DesktopPetFacing
    {
        AnglePos45 = 0,
        AngleNeg45 = 1,
        Front = 2
    }

    /// <summary>
    /// デスクトップペットの動作
    /// </summary>
    public enum DesktopPetAction
    {
        Idle = 0,
        Walk = 1,
        Attack = 2
    }

    /// <summary>
    /// 向きと動作ごとのスプライト連番
    /// </summary>
    public sealed class DesktopPetSpriteSheet
    {
        public const int FacingCount = 3;
        public const int ActionCount = 3;

        private readonly Sprite[][][] frames;

        /// <summary>
        /// 空のシートを作る
        /// </summary>
        public DesktopPetSpriteSheet()
        {
            frames = new Sprite[FacingCount][][];
            for (int facing = 0; facing < FacingCount; facing++)
            {
                frames[facing] = new Sprite[ActionCount][];
                for (int action = 0; action < ActionCount; action++)
                {
                    frames[facing][action] = System.Array.Empty<Sprite>();
                }
            }
        }

        /// <summary>
        /// キャッシュ必須クリップならtrue
        /// </summary>
        public static bool IsRequiredClip(int facing, int action)
        {
            if (facing < 0 || facing >= FacingCount || action < 0 || action >= ActionCount)
            {
                return false;
            }

            if (facing == (int)DesktopPetFacing.Front)
            {
                return action != (int)DesktopPetAction.Attack;
            }

            return true;
        }

        /// <summary>
        /// クリップを登録する
        /// </summary>
        public void SetClip(DesktopPetFacing facing, DesktopPetAction action, Sprite[] clipFrames)
        {
            frames[(int)facing][(int)action] = clipFrames ?? System.Array.Empty<Sprite>();
        }

        /// <summary>
        /// フレーム数を返す
        /// </summary>
        public int GetFrameCount(DesktopPetFacing facing, DesktopPetAction action)
        {
            Sprite[] clip = frames[(int)facing][(int)action];
            return clip != null ? clip.Length : 0;
        }

        /// <summary>
        /// 指定フレームを返す
        /// </summary>
        public Sprite GetFrame(DesktopPetFacing facing, DesktopPetAction action, int frameIndex)
        {
            Sprite[] clip = frames[(int)facing][(int)action];
            if (clip == null || clip.Length == 0)
            {
                return null;
            }

            int safeIndex = frameIndex % clip.Length;
            if (safeIndex < 0)
            {
                safeIndex += clip.Length;
            }

            return clip[safeIndex];
        }

        /// <summary>
        /// 生成したSpriteとTextureを破棄する
        /// </summary>
        public void Dispose()
        {
            var uniqueSprites = new System.Collections.Generic.HashSet<Sprite>();
            var uniqueTextures = new System.Collections.Generic.HashSet<Texture2D>();
            for (int facing = 0; facing < frames.Length; facing++)
            {
                for (int action = 0; action < frames[facing].Length; action++)
                {
                    Sprite[] clip = frames[facing][action];
                    if (clip == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < clip.Length; i++)
                    {
                        Sprite sprite = clip[i];
                        if (sprite == null || !uniqueSprites.Add(sprite))
                        {
                            continue;
                        }

                        if (sprite.texture != null)
                        {
                            uniqueTextures.Add(sprite.texture);
                        }

                        Object.Destroy(sprite);
                    }

                    frames[facing][action] = System.Array.Empty<Sprite>();
                }
            }

            foreach (Texture2D texture in uniqueTextures)
            {
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
            }
        }
    }
}
