using UnityEngine.SceneManagement;

namespace Scene.Rendering
{
    /// <summary>
    /// 背景輪郭線の有効制御
    /// ClayEditのみ無効その他シーンは有効
    /// </summary>
    public static class BackgroundOutlineActivation
    {
        private static bool isForcedOff;

        /// <summary>
        /// ClayEdit突入時に輪郭線を止める
        /// </summary>
        public static void SuppressForClayEdit()
        {
            isForcedOff = true;
        }

        /// <summary>
        /// ClayEdit離脱時に輪郭線抑制を解除する
        /// </summary>
        public static void ClearClayEditSuppression()
        {
            isForcedOff = false;
        }

        /// <summary>
        /// 現在フレームで輪郭線を実行してよいか
        /// </summary>
        public static bool IsEnabled()
        {
            if (isForcedOff)
            {
                return false;
            }

            UnityEngine.SceneManagement.Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.name == "ClayEdit")
            {
                return false;
            }

            return true;
        }
    }
}
