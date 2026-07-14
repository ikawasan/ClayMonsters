#if UNITY_EDITOR
using ClayEditor.Rigging;
using Scene.BattleNpcScene;
using Scene.BattleNpcScene.View;
using Scene.BattlePvpArena;
using Scene.BattlePVPScene.Network;
using Scene.BattlePVPScene.View;
using Scene.Core;
using Scene.PvpLobby;
using System.IO;
using TMPro;
using UI.Battle.View;
using UI.ClayEditor.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// PvpLobby(DDOL)とBattlePvpArenaシーンのMVPセットアップを行う
/// </summary>
public static class PvpMvpSetupEditor
{
    private const string BattleNpcScenePath = "Assets/Scenes/BattleNpc.unity";
    private const string ArenaScenePath = "Assets/Scenes/BattlePvpArena.unity";
    private const string BattlePvpScenePath = "Assets/Scenes/BattlePVP.unity";
    private const string LobbyPrefabPath = "Assets/Resources/Pvp/PvpLobbyHost.prefab";
    private const string NetworkHostPrefabPath = "Assets/Resources/Pvp/BattlePvpNetworkHost.prefab";
    private const string PlayerPrefabPath = "Assets/Resources/BattlePvp/BattlePvpPlayerRelay.prefab";

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
        BattlePvpSceneCreator.CreateOrUpdateScene();
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
                    ? "PvpLobbyHost.prefab と BattlePvpNetworkHost.prefab を生成しました"
                    : "PvpLobbyプレハブの生成に失敗しました\nConsoleを確認してください",
                "OK");
        }
    }

    private static void CreateNetworkHostPrefab(GameObject networkObject)
    {
        string directory = Path.GetDirectoryName(NetworkHostPrefabPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        GameObject networkCopy = Object.Instantiate(networkObject);
        networkCopy.name = "BattlePvpNetworkManager";
        PrefabUtility.SaveAsPrefabAsset(networkCopy, NetworkHostPrefabPath);
        Object.DestroyImmediate(networkCopy);
        Debug.Log($"[PvpMvpSetup] BattlePvpNetworkHost prefab saved path={NetworkHostPrefabPath}");
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
        GameObject root = GameObject.Find("BattleNpcScene") ?? GameObject.Find("BattlePVPScene");
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
        string directory = Path.GetDirectoryName(LobbyPrefabPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        UnityScene referenceScene = EditorSceneManager.OpenScene(BattlePvpScenePath, OpenSceneMode.Additive);
        Canvas matchmakingCanvas = FindCanvasInScene(referenceScene, "MatchmakingCanvas");
        GameObject networkObject = FindRootInScene(referenceScene, "BattlePvpNetworkManager");
        if (matchmakingCanvas == null || networkObject == null)
        {
            Debug.LogError(
                "[PvpMvpSetup] BattlePVPシーンからロビーUIまたはNetworkManagerが見つかりません"
                + $" canvas={(matchmakingCanvas != null)}"
                + $" network={(networkObject != null)}");
            EditorSceneManager.CloseScene(referenceScene, true);
            return;
        }

        CreateNetworkHostPrefab(networkObject);

        var host = new GameObject("PvpLobbyHost");
        host.AddComponent<PvpLobby>();

        GameObject canvasCopy = Object.Instantiate(matchmakingCanvas.gameObject, host.transform);
        canvasCopy.name = "MatchmakingCanvas";
        RectTransform canvasRect = canvasCopy.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.localScale = Vector3.one;
        }

        GameObject diObject = new GameObject("PvpLobbyDi");
        diObject.transform.SetParent(host.transform, false);
        PvpLobbyLifetimeScope scope = diObject.AddComponent<PvpLobbyLifetimeScope>();

        SerializedObject serializedScope = new SerializedObject(scope);
        serializedScope.FindProperty("pvpLobby").objectReferenceValue = host.GetComponent<PvpLobby>();
        serializedScope.FindProperty("lobbyView").objectReferenceValue =
            canvasCopy.GetComponentInChildren<BattlePVPView>(true);
        serializedScope.FindProperty("networkManager").objectReferenceValue = null;
        serializedScope.FindProperty("sessionSpawner").objectReferenceValue = null;
        serializedScope.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedLobby = new SerializedObject(host.GetComponent<PvpLobby>());
        serializedLobby.FindProperty("uiRoot").objectReferenceValue = canvasCopy;
        serializedLobby.ApplyModifiedPropertiesWithoutUndo();

        FixTmpMaterials(host);
        host.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(host, LobbyPrefabPath);
        Object.DestroyImmediate(host);
        EditorSceneManager.CloseScene(referenceScene, true);
        AssignLobbyPrefabToLifetimeScope();
        Debug.Log($"[PvpMvpSetup] PvpLobbyHost prefab saved path={LobbyPrefabPath}");
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

    private static void FixTmpMaterials(GameObject root)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text.font == null || text.fontSharedMaterial != null)
            {
                continue;
            }

            text.fontSharedMaterial = text.font.material;
        }
    }

    private static Canvas FindCanvasInScene(UnityScene scene, string canvasName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Canvas[] canvases = roots[i].GetComponentsInChildren<Canvas>(true);
            for (int j = 0; j < canvases.Length; j++)
            {
                if (canvases[j].name == canvasName)
                {
                    return canvases[j];
                }
            }
        }

        return null;
    }

    private static GameObject FindRootInScene(UnityScene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
            {
                return roots[i];
            }

            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name == objectName)
                {
                    return transforms[j].gameObject;
                }
            }
        }

        return null;
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
