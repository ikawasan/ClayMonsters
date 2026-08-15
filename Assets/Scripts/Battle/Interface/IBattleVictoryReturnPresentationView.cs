using Battle;

namespace Battle.Interface
{
    /// <summary>
    /// 勝利戻りUIの表示形態を切り替えられる
    /// </summary>
    public interface IBattleVictoryReturnPresentationView
    {
        /// <summary>
        /// 表示形態を設定する
        /// </summary>
        /// <param name="presentation">表示形態</param>
        void SetPresentation(BattleVictoryReturnPresentation presentation);
    }
}
