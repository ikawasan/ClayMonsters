using Scene.BattlePVPScene.Interface;

namespace Scene.PvpLobby.Interface
{
    /// <summary>
    /// DDOLロビー実体をClayMonstersスコープへ登録する
    /// </summary>
    public interface IPvpLobbyRegistry
    {
        /// <summary>
        /// 生成済みロビー実体を登録する
        /// </summary>
        void Bind(Scene.PvpLobby.PvpLobby lobby, IBattlePvpMatchmakingService matchmakingService);
    }
}
