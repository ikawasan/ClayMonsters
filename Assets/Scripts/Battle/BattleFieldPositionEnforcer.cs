using UnityEngine;

namespace Battle
{
    /// <summary>
    /// モーション更新後にプレイヤーが敵の右側へ入らないようLateUpdateで補正する
    /// </summary>
    public sealed class BattleFieldPositionEnforcer : MonoBehaviour
    {
        private BattleFieldLayout layout;

        /// <summary>
        /// 補正対象のレイアウトを登録する
        /// </summary>
        public void Bind(BattleFieldLayout fieldLayout)
        {
            layout = fieldLayout;
        }

        /// <summary>
        /// 補正を解除する
        /// </summary>
        public void Unbind()
        {
            layout = null;
        }

        private void LateUpdate()
        {
            layout?.ClampPlayerApproachSide();
        }
    }
}
