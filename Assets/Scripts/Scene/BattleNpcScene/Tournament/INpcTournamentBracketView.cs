using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Scene.BattleNpcScene.Tournament
{
    /// <summary>
    /// トーナメントブラケットUI
    /// </summary>
    public interface INpcTournamentBracketView
    {
        /// <summary>
        /// 必須UIが配線済みか
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// ブラケットを表示する
        /// </summary>
        /// <param name="bracket">状態</param>
        void Show(NpcTournamentBracket bracket);

        /// <summary>
        /// プレイヤーの現在サムネを中央にして入場用ズームを適用する
        /// </summary>
        /// <param name="bracket">状態</param>
        void FocusPlayerEntry(NpcTournamentBracket bracket);

        /// <summary>
        /// ブラケットを隠す
        /// </summary>
        void Hide();

        /// <summary>
        /// ブラケットを即座に隠す(クリーンアップ用)
        /// </summary>
        void HideImmediate();

        /// <summary>
        /// 左クリック進行またはタイトル中断まで待つ
        /// パン操作直後のクリックは無視する
        /// </summary>
        /// <param name="cancellationToken">キャンセル</param>
        /// <returns>進行または中断</returns>
        UniTask<NpcTournamentBracketWaitResult> WaitForAdvanceOrAbortAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// 葉スロットのサムネを設定する
        /// </summary>
        /// <param name="leafIndex">葉</param>
        /// <param name="sprite">サムネ</param>
        /// <param name="displayName">名前</param>
        void SetLeafThumbnail(int leafIndex, Sprite sprite, string displayName);

        /// <summary>
        /// 敗北表示を更新する
        /// </summary>
        /// <param name="bracket">状態</param>
        void RefreshDefeated(NpcTournamentBracket bracket);

        /// <summary>
        /// 勝者を上段ノードへ反映する
        /// </summary>
        /// <param name="bracket">状態</param>
        void RefreshAdvanceSlots(NpcTournamentBracket bracket);

        /// <summary>
        /// 対戦前に両サムネを線に沿って真上へ上昇させ大きく揺らす
        /// </summary>
        /// <param name="round">ラウンド</param>
        /// <param name="matchIndex">試合番号</param>
        /// <param name="leftLeaf">左葉</param>
        /// <param name="rightLeaf">右葉</param>
        /// <param name="restoreSlotsAfter">完了後にトラベラー表示だけ消すか(葉へは戻さない)</param>
        /// <param name="cancellationToken">キャンセル</param>
        UniTask PlayTravelRiseAndShakeAsync(
            int round,
            int matchIndex,
            int leftLeaf,
            int rightLeaf,
            bool restoreSlotsAfter,
            CancellationToken cancellationToken);

        /// <summary>
        /// 戦闘後に敗北側をゆっくり暗転し勝利側を横移動する
        /// </summary>
        /// <param name="round">ラウンド</param>
        /// <param name="matchIndex">試合番号</param>
        /// <param name="winnerLeaf">勝利葉</param>
        /// <param name="loserLeaf">敗北葉</param>
        /// <param name="cancellationToken">キャンセル</param>
        UniTask PlayPostBattleResolveAsync(
            int round,
            int matchIndex,
            int winnerLeaf,
            int loserLeaf,
            CancellationToken cancellationToken);

        /// <summary>
        /// 決勝勝利後に敗者を暗転し勝者を線交差位置へ置く
        /// </summary>
        /// <param name="round">ラウンド</param>
        /// <param name="matchIndex">試合番号</param>
        /// <param name="winnerLeaf">勝利葉</param>
        /// <param name="loserLeaf">敗北葉</param>
        /// <param name="cancellationToken">キャンセル</param>
        UniTask PrepareChampionAtIntersectionAsync(
            int round,
            int matchIndex,
            int winnerLeaf,
            int loserLeaf,
            CancellationToken cancellationToken);

        /// <summary>
        /// 交差位置の勝者サムネを優勝枠へ上昇させる
        /// </summary>
        /// <param name="winnerLeaf">勝利葉</param>
        /// <param name="cancellationToken">キャンセル</param>
        UniTask PlayChampionRiseAsync(
            int winnerLeaf,
            CancellationToken cancellationToken);

        /// <summary>
        /// 優勝報酬のテキストウィンドウを表示し閉じるまで待つ
        /// </summary>
        /// <param name="points">獲得ポイント</param>
        /// <param name="cancellationToken">キャンセル</param>
        UniTask ShowChampionRewardAsync(int points, CancellationToken cancellationToken);
    }
}
