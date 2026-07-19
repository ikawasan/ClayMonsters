using UnityEngine;

namespace Battle.Interface
{
    /// <summary>
    /// 攻撃溜め中の力溜め演出を再生する
    /// </summary>
    public interface IBattleChargeEffect
    {
        /// <summary>
        /// 指定モデルへ溜め演出を開始する
        /// </summary>
        /// <param name="modelRoot">攻撃側モデルルート</param>
        /// <param name="duration">溜め時間(秒)</param>
        void Play(Transform modelRoot, float duration);

        /// <summary>
        /// 溜め強度(0-1)を反映する
        /// </summary>
        /// <param name="intensity">溜め強度</param>
        void SetIntensity(float intensity);

        /// <summary>
        /// 溜め演出を停止する
        /// </summary>
        void Stop();
    }
}
