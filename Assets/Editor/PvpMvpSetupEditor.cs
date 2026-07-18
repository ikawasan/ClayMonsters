#if UNITY_EDITOR
using ClayEditor.Rigging;
using Scene.BattleNpcScene;
using Scene.BattleNpcScene.View;
using Scene.BattlePvpArena;
using Scene.Core;
using Scene.PvpLobby;
using System.IO;
using UI.Battle.View;
using UI.ClayEditor.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// PvpLobby(DDOL)とBattlePvpArenaシーンのMVPセットアップを行う
/// </summary>
public static class PvpMvpSetupEditor
{
    private const string BattleNpcScenePath = "Assets/Scenes/BattleNpc.unity";
    private const string ArenaScenePath = "Assets/Scenes/BattlePvpArena.unity";
    private const string LobbyPrefabPath = "Assets/Resources/Pvp/PvpLobbyHost.prefab";
    private const string NetworkHostPrefabPath = "Assets/Resources/Pvp/BattlePvpNetworkHost.prefab";

    private const string BattleNpcSceneTypeName = "Scene.BattleNpcScene.BattleNpcScene, Scene";
    private const string BattleNpcLifetimeScopeTypeName = "Scene.BattleNpcScene.BattleNpcLifetimeScope, Scene";
    private const string ArenaSceneTypeName = "Scene.BattlePvpArena.BattlePvpArenaScene, Scene";
    private const string ArenaLifetimeScopeTypeName = "Scene.BattlePvpArena.BattlePvpArenaLifetimeScope, Scene";

    [MenuItem("Tools/ClayMonsters/Setup PVP MVP")]
    public static void SetupPvpMvp()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        RunSetup(showDialog: true);
    }

    [MenuItem("Tools/ClayMonsters/Create PvpLobby Prefab Only")]
    public static void CreateLobbyPrefabOnly()
    {
        RunLobbyPrefabSetup(showDialog: true);
    }

    /// <summary>
    /// バッチモード用セットアップ
    /// </summary>
    public static void SetupPvpMvpBatch()
    {
        RunSetup(showDialog: false);
    }

    private static void RunSetup(bool showDialog)
    {
        CreateArenaScene();
        RunLobbyPrefabSetup(showDialog: false);
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "PVP MVP",
                "PvpLobby + BattlePvpArena のセットアップが完了しました\n"
                + "Titleの通信対戦ボタンからロビーUIが開きます",
                "OK");
        }
    }

    private static void RunLobbyPrefabSetup(bool showDialog)
    {
        CreateLobbyPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (showDialog)
        {
            bool lobbyExists = File.Exists(LobbyPrefabPath);
            bool networkExists = File.Exists(NetworkHostPrefabPath);
            EditorUtility.DisplayDialog(
                "PvpLobby Prefab",
                lobbyExists && networkExists
                    ? "既存のPvpLobbyHost.prefabとBattlePvpNetworkHost.prefabを確認し割り当てました"
                    : "PvpLobbyプレハブが見つかりません\nResources/Pvp配下を確認してください",
                "OK");
        }
    }

    private static void CreateArenaScene()
    {
        if (!File.Exists(BattleNpcScenePath))
        {
            Debug.LogError($"[PvpMvpSetup] source not found: {BattleNpcScenePath}");
            return;
        }

        File.Copy(BattleNpcScenePath, ArenaScenePath, overwrite: true);
        AssetDatabase.ImportAsset(ArenaScenePath);

        UnityScene scene = EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find("BattleNpcScene") ?? GameObject.Find("BattlePvpArenaScene");
        if (root == null)
        {
            Debug.LogError("[PvpMvpSetup] arena scene root not found");
            return;
        }

        root.name = "BattlePvpArenaScene";
        ReplaceComponent(root, BattleNpcSceneTypeName, ArenaSceneTypeName);
        ReplaceComponent(root, BattleNpcLifetimeScopeTypeName, ArenaLifetimeScopeTypeName);

        BattlePvpArenaFlowRunner arenaRunner = EnsureArenaFlowRunnerOnRoot(root);

        WireArenaFlowRunner(arenaRunner, root);
        WireArenaLifetimeScope(root);
        WireArenaScene(root, arenaRunner);

        GameObject field = GameObject.Find("Field");
        if (field != null)
        {
            field.SetActive(true);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void WireArenaFlowRunner(BattlePvpArenaFlowRunner runner, GameObject root)
    {
        SerializedObject serializedRunner = new SerializedObject(runner);
        serializedRunner.FindProperty("loadSlotView").objectReferenceValue =
            root.GetComponentInChildren<LoadSlotView>(true);
        serializedRunner.FindProperty("selectionCanvas").objectReferenceValue = FindCanvas("LoadSlotCanvas");
        serializedRunner.FindProperty("titleReturnButton").objectReferenceValue =
            FindLhButton(root.transform, "TitleReturnButton");
        serializedRunner.FindProperty("battleView").objectReferenceValue =
            Object.FindFirstObjectByType<BattleView>(FindObjectsInactive.Include);
        serializedRunner.FindProperty("battleUiCanvas").objectReferenceValue = FindCanvas("BattleCanvas");
        serializedRunner.FindProperty("playerSpawn").objectReferenceValue = GameObject.Find("PlayerSpawnPoint")?.transform;
        serializedRunner.FindProperty("enemySpawn").objectReferenceValue = GameObject.Find("EnemySpawnPoint")?.transform;
        serializedRunner.FindProperty("configurator").objectReferenceValue =
            Object.FindFirstObjectByType<LoadedModelConfigurator>(FindObjectsInactive.Include);
        serializedRunner.FindProperty("staging").objectReferenceValue =
            Object.FindFirstObjectByType<BattleNpcStaging>(FindObjectsInactive.Include);
        serializedRunner.FindProperty("battleCamera").objectReferenceValue =
            Object.FindFirstObjectByType<Camera.View.ClayEditCameraView>(FindObjectsInactive.Include);
        serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(runner);
    }

    private static void WireArenaLifetimeScope(GameObject root)
    {
        System.Type scopeType = ResolveType(ArenaLifetimeScopeTypeName);
        if (scopeType == null)
        {
            return;
        }

        Component scope = root.GetComponent(scopeType);
        if (scope == null)
        {
            return;
        }

        SerializedObject serializedScope = new SerializedObject(scope);
        serializedScope.FindProperty("arenaScene").objectReferenceValue =
            root.GetComponent(ResolveType(ArenaSceneTypeName));
        serializedScope.FindProperty("battleNpcView").objectReferenceValue =
            Object.FindFirstObjectByType<BattleNpcView>(FindObjectsInactive.Include);
        serializedScope.FindProperty("cameraView").objectReferenceValue =
            Object.FindFirstObjectByType<Camera.View.ClayEditCameraView>(FindObjectsInactive.Include);
        serializedScope.FindProperty("arenaFlowRunner").objectReferenceValue =
            root.GetComponent<BattlePvpArenaFlowRunner>()
            ?? root.GetComponentInChildren<BattlePvpArenaFlowRunner>(true);
        serializedScope.FindProperty("postProcessView").objectReferenceValue =
            Object.FindFirstObjectByType<BattleNpcPostProcessView>(FindObjectsInactive.Include);
        serializedScope.FindProperty("loadSlotView").objectReferenceValue =
            root.GetComponentInChildren<LoadSlotView>(true);
        serializedScope.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(scope);
    }

    private static void WireArenaScene(GameObject root, BattlePvpArenaFlowRunner arenaRunner)
    {
        System.Type arenaSceneType = ResolveType(ArenaSceneTypeName);
        if (arenaSceneType == null || arenaRunner == null)
        {
            return;
        }

        Component arenaScene = root.GetComponent(arenaSceneType);
        if (arenaScene == null)
        {
            return;
        }

        EditorUtility.SetDirty(arenaScene);
    }

    private static void CreateLobbyPrefab()
    {
        if (!File.Exists(LobbyPrefabPath) || !File.Exists(NetworkHostPrefabPath))
        {
            Debug.LogError(
                "[PvpMvpSetup] PvpLobbyHostまたはBattlePvpNetworkHostプレハブが見つかりません"
                + $" lobbyExists={File.Exists(LobbyPrefabPath)}"
                + $" networkExists={File.Exists(NetworkHostPrefabPath)}"
                + " Resources/Pvp配下のプレハブを復元してください");
            return;
        }

        AssignLobbyPrefabToLifetimeScope();
        Debug.Log(
            "[PvpMvpSetup] 既存PvpLobbyプレハブを確認しLifetimeScopeへ割り当てました"
            + $" lobby={LobbyPrefabPath}"
            + $" network={NetworkHostPrefabPath}");
    }

    private static void AssignLobbyPrefabToLifetimeScope()
    {
        const string scopePrefabPath = "Assets/Scripts/StaticResources/ClayMonstersLifetimeScope.prefab";
        GameObject scopePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(scopePrefabPath);
        GameObject lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath);
        if (scopePrefab == null || lobbyPrefab == null)
        {
            return;
        }

        ClayMonstersLifetimeScope scope = scopePrefab.GetComponent<ClayMonstersLifetimeScope>();
        if (scope == null)
        {
            return;
        }

        SerializedObject serializedScope = new SerializedObject(scope);
        serializedScope.FindProperty("pvpLobbyHostPrefab").objectReferenceValue = lobbyPrefab;
        serializedScope.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(scopePrefab);
        PrefabUtility.SavePrefabAsset(scopePrefab);
    }

    private static void UpdateBuildSettings()
    {
        string[] scenes =
        {
            "Assets/Scenes/Bootstrap.unity",
            "Assets/Scenes/Title.unity",
            "Assets/Scenes/ClayEdit.unity",
            "Assets/Scenes/BattleNpc.unity",
            ArenaScenePath,
            "Assets/Scenes/Training.unity"
        };

        var buildScenes = new EditorBuildSettingsScene[scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
        {
            buildScenes[i] = new EditorBuildSettingsScene(scenes[i], true);
        }

        EditorBuildSettings.scenes = buildScenes;
    }

    private static BattlePvpArenaFlowRunner EnsureArenaFlowRunnerOnRoot(GameObject root)
    {
        BattleFlowRunner[] legacyRunners = root.GetComponentsInChildren<BattleFlowRunner>(true);
        for (int i = 0; i < legacyRunners.Length; i++)
        {
            Object.DestroyImmediate(legacyRunners[i]);
        }

        BattlePvpArenaFlowRunner rootRunner = root.GetComponent<BattlePvpArenaFlowRunner>();
        if (rootRunner != null)
        {
            Object.DestroyImmediate(rootRunner);
        }

        Transform runnerTransform = root.transform.Find("BattleFrowRunner");
        GameObject runnerObject;
        if (runnerTransform == null)
        {
            runnerObject = new GameObject("BattleFrowRunner");
            runnerObject.transform.SetParent(root.transform, false);
        }
        else
        {
            runnerObject = runnerTransform.gameObject;
        }

        BattlePvpArenaFlowRunner[] duplicateRunners = root.GetComponentsInChildren<BattlePvpArenaFlowRunner>(true);
        BattlePvpArenaFlowRunner arenaRunner = null;
        for (int i = 0; i < duplicateRunners.Length; i++)
        {
            if (duplicateRunners[i].gameObject == runnerObject)
            {
                arenaRunner = duplicateRunners[i];
                continue;
            }

            Object.DestroyImmediate(duplicateRunners[i]);
        }

        if (arenaRunner == null)
        {
            arenaRunner = runnerObject.AddComponent<BattlePvpArenaFlowRunner>();
        }

        return arenaRunner;
    }

    private static void ReplaceComponent(GameObject target, string oldTypeName, string newTypeName)
    {
        System.Type oldType = ResolveType(oldTypeName);
        System.Type newType = ResolveType(newTypeName);
        if (oldType == null || newType == null)
        {
            return;
        }

        Component oldComponent = target.GetComponent(oldType);
        if (oldComponent != null)
        {
            Object.DestroyImmediate(oldComponent);
        }

        if (target.GetComponent(newType) == null)
        {
            target.AddComponent(newType);
        }
    }

    private static Canvas FindCanvas(string canvasName)
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].name == canvasName)
            {
                return canvases[i];
            }
        }

        return null;
    }

    private static Component FindLhButton(Transform root, string buttonName)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name != buttonName)
            {
                continue;
            }

            Component button = SceneUiLhButtonUtility.FindInChildren(transforms[i]);
            if (button != null)
            {
                return button;
            }
        }

        return null;
    }

    private static System.Type ResolveType(string typeName)
    {
        System.Type type = System.Type.GetType(typeName);
        if (type != null)
        {
            return type;
        }

        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName, false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
#endif
