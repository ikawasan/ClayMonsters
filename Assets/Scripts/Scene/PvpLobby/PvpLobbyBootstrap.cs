using UnityEngine;

namespace Scene.PvpLobby
{
    /// <summary>
    /// PvpLobbyHostプレハブをDDOLへ生成する
    /// </summary>
    public static class PvpLobbyBootstrap
    {
        /// <summary>
        /// ロビープレハブを生成する
        /// </summary>
        /// <returns>生成に成功した場合true</returns>
        public static bool EnsureLobby()
        {
            if (Object.FindFirstObjectByType<PvpLobby>(FindObjectsInactive.Include) != null)
            {
                return true;
            }

            if (!PvpLobbyRuntimeFactory.TryCreate(out GameObject instance))
            {
                Debug.LogError(
                    "[PvpLobby] ロビー生成に失敗しました"
                    + " PvpLobbyプレハブ配置を確認してください");
                return false;
            }

            Debug.Log("[PvpLobby] DDOLロビーを生成しました");
            return instance != null;
        }
    }
}
