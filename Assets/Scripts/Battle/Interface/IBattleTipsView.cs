using System;

namespace Battle.Interface
{
    /// <summary>
    /// 戦闘中チュートリアルTipsの表示契約
    /// </summary>
    public interface IBattleTipsView
    {
        /// <summary>
        /// Tipsが開いているか
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// NPC戦闘向けにTips機能を有効化しヒントを表示する
        /// </summary>
        /// <returns>戦闘終了時に破棄するセッション</returns>
        IDisposable BeginCombatSession();

        /// <summary>
        /// Tipsとヒントを閉じ機能を無効化する
        /// </summary>
        void HideAll();
    }
}
