using UnityEngine;

namespace Battle.Interface
{
    /// <summary>
    /// 攻撃命中時のワールド演出を再生する
    /// </summary>
    public interface IBattleHitEffect
    {
        /// <summary>
        /// 命中位置にヒット演出を再生する
        /// </summary>
        /// <param name="worldPosition">演出を出すワールド座標</param>
        /// <param name="isPartBreak">部位破壊が発生した命中か</param>
        void PlayHit(Vector3 worldPosition, bool isPartBreak);
    }
}
