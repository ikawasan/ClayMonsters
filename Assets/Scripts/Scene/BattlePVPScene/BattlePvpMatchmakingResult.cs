namespace Scene.BattlePVPScene
{
    /// <summary>
    /// マッチング完了または失敗の結果
    /// </summary>
    public readonly struct BattlePvpMatchmakingResult
    {
        /// <summary>
        /// マッチング結果を生成する
        /// </summary>
        public BattlePvpMatchmakingResult(bool isSuccess, bool isHost, string roomCode, string errorMessage)
        {
            IsSuccess = isSuccess;
            IsHost = isHost;
            RoomCode = roomCode ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        /// <summary>
        /// マッチングに成功したか
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// ホスト側か
        /// </summary>
        public bool IsHost { get; }

        /// <summary>
        /// ルームコード
        /// </summary>
        public string RoomCode { get; }

        /// <summary>
        /// 失敗時のメッセージ
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 成功結果を生成する
        /// </summary>
        public static BattlePvpMatchmakingResult Succeeded(bool isHost, string roomCode) =>
            new BattlePvpMatchmakingResult(true, isHost, roomCode, string.Empty);

        /// <summary>
        /// 失敗結果を生成する
        /// </summary>
        public static BattlePvpMatchmakingResult Failed(string errorMessage) =>
            new BattlePvpMatchmakingResult(false, false, string.Empty, errorMessage);
    }
}
