#if UNITY_EDITOR
using System.IO;
using UnityEditor;

/// <summary>
/// PvpLobbyプレハブが無い場合にEditor起動時へ自動生成する
/// </summary>
[InitializeOnLoad]
public static class PvpLobbyPrefabEnsureOnLoad
{
    private const string LobbyPrefabPath = "Assets/Resources/Pvp/PvpLobbyHost.prefab";
    private const string NetworkHostPrefabPath = "Assets/Resources/Pvp/BattlePvpNetworkHost.prefab";

    static PvpLobbyPrefabEnsureOnLoad()
    {
        EditorApplication.delayCall += EnsurePrefabIfMissing;
    }

    private static void EnsurePrefabIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (File.Exists(LobbyPrefabPath) && File.Exists(NetworkHostPrefabPath))
        {
            return;
        }

        if (!File.Exists("Assets/Scenes/BattlePVP.unity"))
        {
            return;
        }

        UnityEngine.Debug.Log("[PvpLobby] PvpLobbyプレハブ未配置のため自動生成します");
        PvpMvpSetupEditor.CreateLobbyPrefabOnly();
    }
}
#endif
