using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Battle.Interface
{
    /// <summary>
    /// 戦闘シーン内のキャンバス切替時に画面フェードを挟む
    /// </summary>
    public interface IBattleCanvasTransition
    {
        /// <summary>
        /// 暗転オーバーレイの入力ブロックを強制解除する
        /// </summary>
        void ReleasePresentationInput();

        /// <summary>
        /// 画面を暗転する
        /// </summary>
        UniTask FadeOutAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 画面を明転する
        /// </summary>
        UniTask FadeInAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 必要なら暗転後にキャンバスの有効状態を変えて明転する
        /// </summary>
        /// <param name="fadeOutBeforeChange">変更前に暗転するか</param>
        /// <param name="fadeInAfterChange">変更後に明転するか</param>
        UniTask SetCanvasEnabledAsync(
            Canvas canvas,
            bool enabled,
            CancellationToken cancellationToken = default,
            bool fadeOutBeforeChange = true,
            bool fadeInAfterChange = true);

        /// <summary>
        /// 暗転後に表示キャンバスを切り替えて明転する
        /// </summary>
        UniTask SwitchCanvasAsync(Canvas from, Canvas to, CancellationToken cancellationToken = default);

        /// <summary>
        /// 暗転後に処理を実行する(明転は呼び出し側が行う)
        /// </summary>
        UniTask FadeOutAndRunAsync(Action apply, CancellationToken cancellationToken = default);
    }
}
