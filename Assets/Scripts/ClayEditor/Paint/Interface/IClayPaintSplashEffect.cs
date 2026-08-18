using UnityEngine;

namespace ClayEditor.Paint.Interface
{
    /// <summary>
    /// ペイント時のインク飛沫演出を再生する
    /// </summary>
    public interface IClayPaintSplashEffect
    {
        /// <summary>
        /// 塗った位置の周囲へインク飛沫を出す
        /// </summary>
        /// <param name="worldPosition">塗りの中心ワールド座標</param>
        /// <param name="worldNormal">ヒット面の法線</param>
        /// <param name="color">現在のペイント色</param>
        /// <param name="brushRadius">ブラシ半径</param>
        void Play(Vector3 worldPosition, Vector3 worldNormal, Color color, float brushRadius);
    }
}
