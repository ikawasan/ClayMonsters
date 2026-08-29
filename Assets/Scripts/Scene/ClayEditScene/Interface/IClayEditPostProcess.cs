using UnityEngine;

namespace Scene.ClayEditScene.Interface
{
    /// <summary>
    /// ClayEditシーンの背景色を制御する
    /// メッシュ色味維持のためポストプロセスは掛けない
    /// </summary>
    public interface IClayEditPostProcess
    {
        /// <summary>
        /// 現在の背景色
        /// </summary>
        Color BackgroundColor { get; }

        /// <summary>
        /// 黒背景を有効化しカメラのポストプロセスを切る
        /// </summary>
        void Enable();

        /// <summary>
        /// 設定を無効化して他シーンへの影響を戻す
        /// </summary>
        void Disable();

        /// <summary>
        /// 背景色を更新する
        /// 有効中ならカメラへ即時反映する
        /// </summary>
        /// <param name="color">適用する背景色</param>
        void SetBackgroundColor(Color color);

        /// <summary>
        /// 背景色を既定値へ戻す
        /// 有効中ならカメラへ即時反映する
        /// </summary>
        void ResetBackgroundColorToDefault();
    }
}
