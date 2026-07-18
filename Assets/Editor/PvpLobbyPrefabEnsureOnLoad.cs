#if UNITY_EDITOR
using System.IO;
using UnityEditor;

/// <summary>
/// PvpLobbyプレハブ欠落時に警告を出す
/// </summary>
[InitializeOnLoad]
public static class PvpLobbyPrefabEnsureOnLoad
{
    private const string LobbyPrefabPath = "Assets/Resources/Pvp/PvpLobbyHost.prefab";
    private const string NetworkHostPrefabPath = "Assets/Resources/Pvp/BattlePvpNetworkHost.prefab";

    static PvpLobbyPrefabEnsureOnLoad()
    {
        EditorApplication.delayCall += WarnIfPrefabMissing;
    }

    private static void WarnIfPrefabMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (File.Exists(LobbyPrefabPath) && File.Exists(NetworkHostPrefabPath))
        {
            return;
        }

        UnityEngine.Debug.LogWarning(
            "[PvpLobby] PvpLobbyHostまたはBattlePvpNetworkHostプレハブが見つかりません"
            + $" lobbyExists={File.Exists(LobbyPrefabPath)}"
            + $" networkExists={File.Exists(NetworkHostPrefabPath)}"
            + " Resources/Pvp配下のプレハブを確認してください");
    }
}
#endif
