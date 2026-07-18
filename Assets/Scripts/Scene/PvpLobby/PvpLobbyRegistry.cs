using Scene.BattlePVPScene.Interface;
using Scene.PvpLobby.Interface;
using UnityEngine;

namespace Scene.PvpLobby
{
    /// <summary>
    /// ClayMonstersスコープからDDOLロビーへ橋渡しする
    /// </summary>
    public sealed class PvpLobbyRegistry : IPvpLobby, IPvpLobbyRegistry, IPvpSessionController
    {
        private PvpLobby lobby;
        private IBattlePvpMatchmakingService matchmakingService;

        /// <inheritdoc/>
        public void Bind(PvpLobby lobby, IBattlePvpMatchmakingService matchmakingService)
        {
            this.lobby = lobby;
            this.matchmakingService = matchmakingService;
        }

        /// <inheritdoc/>
        public void Show()
        {
            EnsureLobbyInstance();
            if (lobby == null)
            {
                Debug.LogError(
                    "[PvpLobby] ロビーが未生成です"
                    + " PvpLobbyプレハブ配置を確認してください");
                return;
            }

            lobby.Show();
        }

        /// <inheritdoc/>
        public void Hide()
        {
            lobby?.Hide();
        }

        /// <inheritdoc/>
        public void EndSession()
        {
            matchmakingService?.Cancel();
        }

        private void EnsureLobbyInstance()
        {
            if (lobby != null)
            {
                return;
            }

            PvpLobbyBootstrap.EnsureLobby();
            lobby = Object.FindFirstObjectByType<PvpLobby>(FindObjectsInactive.Include);
        }
    }
}
