using UnityEngine;

namespace Battle.Interface
{
    /// <summary>
    /// 攻撃溜め中に周囲から粒子を取り込む演出を再生する
    /// </summary>
    public interface IBattleChargeEffect
    {
        /// <summary>
        /// 指定モデルへ取り込み演出を開始する
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
        /// 取り込み演出を停止する
        /// </summary>
        void Stop();
    }
}
