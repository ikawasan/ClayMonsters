using UnityEngine;

namespace Scene.DesktopPet
{
    /// <summary>
    /// スプライトシートを低FPSで再生する
    /// </summary>
    public sealed class DesktopPetSpriteAnimator
    {
        private readonly SpriteRenderer renderer;
        private readonly DesktopPetSpriteSheet sheet;
        private readonly float secondsPerFrame;

        private DesktopPetFacing facing = DesktopPetFacing.AnglePos45;
        private DesktopPetAction action = DesktopPetAction.Idle;
        private int frameIndex;
        private float elapsed;

        /// <summary>
        /// 再生器を生成する
        /// </summary>
        public DesktopPetSpriteAnimator(
            SpriteRenderer renderer,
            DesktopPetSpriteSheet sheet,
            float fps = 8f)
        {
            this.renderer = renderer;
            this.sheet = sheet;
            secondsPerFrame = 1f / Mathf.Max(1f, fps);
            ApplyFrame();
        }

        /// <summary>
        /// 向きを切り替える
        /// </summary>
        public void SetFacing(DesktopPetFacing nextFacing)
        {
            if (facing == nextFacing)
            {
                return;
            }

            facing = nextFacing;
            // 向き変更で歩行サイクルをリセットするとループが途切れて見える
            ApplyFrame();
        }

        /// <summary>
        /// 動作を切り替える
        /// </summary>
        public void SetAction(DesktopPetAction nextAction)
        {
            if (action == nextAction)
            {
                return;
            }

            action = nextAction;
            frameIndex = 0;
            elapsed = 0f;
            ApplyFrame();
        }

        /// <summary>
        /// 現在の動作
        /// </summary>
        public DesktopPetAction CurrentAction => action;

        /// <summary>
        /// 現在の向き
        /// </summary>
        public DesktopPetFacing CurrentFacing => facing;

        /// <summary>
        /// 毎フレーム進める
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (sheet == null || renderer == null)
            {
                return;
            }

            int count = sheet.GetFrameCount(facing, action);
            if (count <= 1)
            {
                return;
            }

            elapsed += deltaTime;
            while (elapsed >= secondsPerFrame)
            {
                elapsed -= secondsPerFrame;
                frameIndex++;
                if (action == DesktopPetAction.Attack && frameIndex >= count)
                {
                    SetAction(DesktopPetAction.Idle);
                    return;
                }

                frameIndex %= count;
                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            if (renderer == null || sheet == null)
            {
                return;
            }

            Sprite sprite = sheet.GetFrame(facing, action, frameIndex);
            if (sprite != null)
            {
                renderer.sprite = sprite;
            }
        }
    }
}
