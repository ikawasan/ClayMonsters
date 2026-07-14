using UnityEngine;

namespace Battle.Interface
{
    /// <summary>
    /// 攻撃命中時のダメージ数値を表示する
    /// </summary>
    public interface IBattleDamagePopup
    {
        /// <summary>
        /// 命中位置にダメージ数値を表示する
        /// </summary>
        /// <param name="worldPosition">表示位置のワールド座標</param>
        /// <param name="damage">与えたダメージ</param>
        /// <param name="isPartBreak">部位破壊が発生した命中か</param>
        /// <param name="isKnockout">とどめの命中か</param>
        void PlayDamage(Vector3 worldPosition, int damage, bool isPartBreak, bool isKnockout);

        /// <summary>
        /// 外れた攻撃の表示位置にミスラベルを表示する
        /// </summary>
        /// <param name="worldPosition">表示位置のワールド座標</param>
        void PlayMiss(Vector3 worldPosition);
    }
}
