using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace Scene.BattlePVPScene.Interface
{
    /// <summary>
    /// PvPマッチングとネットワーク接続を管理する
    /// </summary>
    public interface IBattlePvpMatchmakingService
    {
        /// <summary>
        /// 接続状態の変化を通知する
        /// </summary>
        event Action<string> StatusChanged;

        /// <summary>
        /// 2人揃って接続済みか
        /// </summary>
        bool IsSessionReady { get; }

        /// <summary>
        /// ローカルがホストか
        /// </summary>
        bool IsHost { get; }

        /// <summary>
        /// 進行中のルームコード
        /// </summary>
        string CurrentRoomCode { get; }

        /// <summary>
        /// 特定相手向けにホストとしてルームを作成する
        /// </summary>
        UniTask<BattlePvpMatchmakingResult> CreateDirectRoomAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 参加コードで特定の相手のルームへ参加する
        /// </summary>
        UniTask<BattlePvpMatchmakingResult> JoinDirectRoomAsync(string joinCode, CancellationToken cancellationToken);

        /// <summary>
        /// 不特定の相手を自動マッチングする
        /// </summary>
        UniTask<BattlePvpMatchmakingResult> StartRandomMatchAsync(CancellationToken cancellationToken);

        /// <summary>
        /// マッチングと接続を中断する
        /// </summary>
        void Cancel();

        /// <summary>
        /// ロビーのみ解放しNetworkManager接続はArenaへ引き継ぐ
        /// </summary>
        void FinishMatchmakingKeepNetwork();
    }
}
