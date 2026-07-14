#if UNITY_EDITOR
using ClayEditor.Rigging;
using Scene.BattleNpcScene;
using Scene.BattleNpcScene.View;
using Scene.BattlePVPScene;
using Scene.BattlePVPScene.Network;
using Scene.BattlePVPScene.View;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UI.Battle.View;
using UI.ClayEditor.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// BattlePVPシーンをBattleNpcベースで生成し教室フィールドとマッチングUIを配線する
/// </summary>
public static class BattlePvpSceneCreator
{
    private const string SourceScenePath = "Assets/Scenes/BattleNpc.unity";
    private const string TargetScenePath = "Assets/Scenes/BattlePVP.unity";
    private const string PlayerPrefabPath = "Assets/Resources/BattlePvp/BattlePvpPlayerRelay.prefab";
    private const string BattleNpcSceneTypeName = "Scene.BattleNpcScene.BattleNpcScene, Scene";
    private const string BattlePvpSceneTypeName = "Scene.BattlePVPScene.BattlePVPScene, Scene";
    private const string BattleNpcLifetimeScopeTypeName = "Scene.BattleNpcScene.BattleNpcLifetimeScope, Scene";
    private const string BattlePvpLifetimeScopeTypeName = "Scene.BattlePVPScene.BattlePVPLifetimeScope, Scene";

    [MenuItem("Tools/ClayMonsters/Create BattlePVP Scene")]
    public static void CreateOrUpdateScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsurePlayerPrefab();
        EnsureSceneFileExists();
        UnityScene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        ConfigureScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        WarnIfUnityServicesNotLinked();

        EditorUtility.DisplayDialog(
            "BattlePVP Scene",
            "BattlePVPシーンの生成を完了しました\n教室フィールドとマッチングUIを設定済みです",
            "OK");
    }

    private static void WarnIfUnityServicesNotLinked()
    {
        if (!string.IsNullOrWhiteSpace(UnityEditor.CloudProjectSettings.projectId))
        {
            return;
        }

        Debug.LogWarning(
            "[BattlePvpSceneCreator] Unity Gaming Servicesが未リンクです。Edit > Project Settings > Servicesでプロジェクトをリンクし、DashboardでAuthentication・Lobby・Relayを有効化してください");
    }

    private static void EnsureSceneFileExists()
    {
        if (!File.Exists(SourceScenePath))
        {
            Debug.LogError($"[BattlePvpSceneCreator] source scene not found: {SourceScenePath}");
            return;
        }

        File.Copy(SourceScenePath, TargetScenePath, overwrite: true);
        AssetDatabase.ImportAsset(TargetScenePath);
    }

    private static void ConfigureScene()
    {
        DisableNpcOnlyObjects();
        ActivateClassroomField();
        ConfigureRootSceneComponents();
        ConfigureFlowRunner();
        ConfigureMatchmakingUi();
        ConfigureNetwork();
        ConfigureAuxiliaryUi();
        ConfigureCanvasInitializer(matchmakingCanvas: FindCanvasByName("MatchmakingCanvas"));
        ConfigureLifetimeScope();
    }

    private static void ConfigureAuxiliaryUi()
    {
        GameObject sceneRoot = GameObject.Find("BattlePVPScene");
        if (sceneRoot == null)
        {
            return;
        }

        System.Type lifetimeScopeType = ResolveType(BattlePvpLifetimeScopeTypeName);
        Component lifetimeScope = lifetimeScopeType != null
            ? Object.FindFirstObjectByType(lifetimeScopeType, FindObjectsInactive.Include) as Component
            : null;
        BattlePvpAuxiliaryUiSceneBuilder.EnsureOnScene(sceneRoot.transform, lifetimeScope);
    }

    private static void ConfigureCanvasInitializer(Canvas matchmakingCanvas)
    {
        System.Type initializerType = ResolveType("Lighthouse.Scene.SceneCamera.SceneCanvasInitializer, Lighthouse.Runtime");
        if (initializerType == null)
        {
            return;
        }

        Component initializer = Object.FindFirstObjectByType(initializerType, FindObjectsInactive.Include) as Component;
        if (initializer == null)
        {
            return;
        }

        SerializedObject serializedInitializer = new SerializedObject(initializer);
        SerializedProperty listProperty = serializedInitializer.FindProperty("sceneCanvasList");
        if (listProperty == null || matchmakingCanvas == null)
        {
            return;
        }

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            if (listProperty.GetArrayElementAtIndex(i).objectReferenceValue == matchmakingCanvas)
            {
                return;
            }
        }

        listProperty.InsertArrayElementAtIndex(listProperty.arraySize);
        listProperty.GetArrayElementAtIndex(listProperty.arraySize - 1).objectReferenceValue = matchmakingCanvas;
        serializedInitializer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(initializer);
    }

    private static void DisableNpcOnlyObjects()
    {
        DisableComponentsInScene<BattleNpcView>();
        DisableComponentsInScene<BattleFlowRunner>();
        RemoveComponentsInScene(BattleNpcSceneTypeName);
        RemoveComponentsInScene(BattleNpcLifetimeScopeTypeName);
    }

    private static void ActivateClassroomField()
    {
        GameObject field = GameObject.Find("Field");
        if (field != null)
        {
            field.SetActive(true);
        }
    }

    private static void ConfigureRootSceneComponents()
    {
        GameObject root = GameObject.Find("BattleNpcScene") ?? new GameObject("BattlePVPScene");
        root.name = "BattlePVPScene";

        GetOrAddComponent(root, BattlePvpSceneTypeName);
        EnsureDedicatedLifetimeScope(root);
        EditorUtility.SetDirty(root);
    }

    private static void EnsureDedicatedLifetimeScope(GameObject sceneRoot)
    {
        System.Type lifetimeScopeType = ResolveType(BattlePvpLifetimeScopeTypeName);
        if (lifetimeScopeType == null)
        {
            return;
        }

        Component scopeOnRoot = sceneRoot.GetComponent(lifetimeScopeType);
        Transform dedicatedTransform = sceneRoot.transform.Find("BattlePvpDi");
        GameObject dedicatedObject = dedicatedTransform != null
            ? dedicatedTransform.gameObject
            : new GameObject("BattlePvpDi");

        if (dedicatedTransform == null)
        {
            dedicatedObject.transform.SetParent(sceneRoot.transform, false);
        }

        Component dedicatedScope = dedicatedObject.GetComponent(lifetimeScopeType);
        if (dedicatedScope == null)
        {
            dedicatedScope = dedicatedObject.AddComponent(lifetimeScopeType);
        }

        if (scopeOnRoot != null && scopeOnRoot != dedicatedScope)
        {
            Object.DestroyImmediate(scopeOnRoot);
        }
    }

    private static void ConfigureFlowRunner()
    {
        GameObject root = GameObject.Find("BattlePVPScene");
        if (root == null)
        {
            return;
        }

        BattlePvpFlowRunner flowRunner = root.GetComponent<BattlePvpFlowRunner>();
        if (flowRunner == null)
        {
            flowRunner = root.AddComponent<BattlePvpFlowRunner>();
        }

        SerializedObject serializedRunner = new SerializedObject(flowRunner);
        GameObject sceneRoot = GameObject.Find("BattlePVPScene");
        LoadSlotView loadSlotView = sceneRoot != null
            ? sceneRoot.GetComponentInChildren<LoadSlotView>(true)
            : Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
        serializedRunner.FindProperty("loadSlotView").objectReferenceValue = loadSlotView;
        serializedRunner.FindProperty("selectionCanvas").objectReferenceValue = FindCanvasByName("LoadSlotCanvas");
        serializedRunner.FindProperty("battleView").objectReferenceValue =
            Object.FindFirstObjectByType<BattleView>(FindObjectsInactive.Include);
        serializedRunner.FindProperty("battleUiCanvas").objectReferenceValue = FindCanvasByName("BattleCanvas");
        serializedRunner.FindProperty("matchmakingCanvas").objectReferenceValue = FindCanvasByName("MatchmakingCanvas");
        serializedRunner.FindProperty("playerSpawn").objectReferenceValue = GameObject.Find("PlayerSpawnPoint")?.transform;
        serializedRunner.FindProperty("enemySpawn").objectReferenceValue = GameObject.Find("EnemySpawnPoint")?.transform;
        serializedRunner.FindProperty("configurator").objectReferenceValue =
            Object.FindFirstObjectByType<LoadedModelConfigurator>(FindObjectsInactive.Include);
        serializedRunner.FindProperty("staging").objectReferenceValue =
            Object.FindFirstObjectByType<BattleNpcStaging>(FindObjectsInactive.Include);
        serializedRunner.FindProperty("battleCamera").objectReferenceValue =
            Object.FindFirstObjectByType<Camera.View.ClayEditCameraView>(FindObjectsInactive.Include);
        serializedRunner.FindProperty("sessionSpawner").objectReferenceValue =
            Object.FindFirstObjectByType<BattlePvpSessionSpawner>(FindObjectsInactive.Include);
        serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flowRunner);
    }

    private static void ConfigureMatchmakingUi()
    {
        Canvas matchmakingCanvas = FindCanvasByName("MatchmakingCanvas");
        if (matchmakingCanvas == null)
        {
            GameObject root = GameObject.Find("BattlePVPScene") ?? GameObject.Find("BattleNpcScene");
            Transform parent = root != null ? root.transform : null;
            matchmakingCanvas = CreateOverlayCanvas(parent, "MatchmakingCanvas", 200);
        }

        BattlePVPView view = matchmakingCanvas.GetComponentInChildren<BattlePVPView>(true);
        if (view == null)
        {
            var viewObject = new GameObject("BattlePVPView", typeof(RectTransform));
            viewObject.transform.SetParent(matchmakingCanvas.transform, false);
            RectTransform viewRect = viewObject.GetComponent<RectTransform>();
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = Vector2.zero;
            viewRect.offsetMax = Vector2.zero;
            view = viewObject.AddComponent<BattlePVPView>();
        }

        GameObject modeSelectPanel = EnsurePanel(view.transform, "ModeSelectPanel", "通信対戦");
        GameObject directPanel = EnsurePanel(view.transform, "DirectMatchPanel", "特定の相手と対戦");
        GameObject randomPanel = EnsurePanel(view.transform, "RandomMatchPanel", "不特定の相手と対戦");
        GameObject matchingPanel = EnsurePanel(view.transform, "MatchingPanel", "マッチング中");
        matchingPanel.SetActive(false);
        directPanel.SetActive(false);
        randomPanel.SetActive(false);

        Component directMatchButton = EnsureCenteredButton(modeSelectPanel.transform, "DirectMatchButton", "特定の相手と対戦", new Vector2(0f, 20f), ButtonStyle.Primary);
        Component randomMatchButton = EnsureCenteredButton(modeSelectPanel.transform, "RandomMatchButton", "不特定の相手と対戦", new Vector2(0f, -80f), ButtonStyle.Primary);
        Component returnButton = EnsureCenteredButton(modeSelectPanel.transform, "ReturnButton", "タイトルへ戻る", new Vector2(0f, -180f), ButtonStyle.Secondary);

        Component createRoomButton = EnsureCenteredButton(directPanel.transform, "CreateRoomButton", "ルームを作成", new Vector2(0f, 100f), ButtonStyle.Primary);
        TMP_InputField joinCodeInput = EnsureJoinCodeInput(directPanel.transform, new Vector2(0f, 0f));
        Component joinRoomButton = EnsureCenteredButton(directPanel.transform, "JoinRoomButton", "ルームに参加", new Vector2(0f, -100f), ButtonStyle.Accent);
        Component directBackButton = EnsureCenteredButton(directPanel.transform, "DirectBackButton", "戻る", new Vector2(0f, -200f), ButtonStyle.Secondary);
        TMP_Text directJoinCodeText = EnsureStatusText(directPanel.transform, "DirectJoinCodeText", new Vector2(0f, 210f));

        Component startRandomButton = EnsureCenteredButton(randomPanel.transform, "StartRandomMatchButton", "マッチング開始", new Vector2(0f, 0f), ButtonStyle.Primary);
        Component randomBackButton = EnsureCenteredButton(randomPanel.transform, "RandomBackButton", "戻る", new Vector2(0f, -120f), ButtonStyle.Secondary);
        Component copyJoinCodeButton = EnsureCenteredButton(matchingPanel.transform, "CopyJoinCodeButton", "コードをコピー", new Vector2(0f, -60f), ButtonStyle.Accent);
        copyJoinCodeButton.gameObject.SetActive(false);
        Component cancelMatchButton = EnsureCenteredButton(matchingPanel.transform, "CancelMatchButton", "キャンセル", new Vector2(0f, -160f), ButtonStyle.Secondary);
        TMP_Text statusText = EnsureStatusText(matchingPanel.transform, "StatusText", new Vector2(0f, 40f));

        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("returnButton").objectReferenceValue = returnButton;
        serializedView.FindProperty("modeSelectPanel").objectReferenceValue = modeSelectPanel;
        serializedView.FindProperty("directMatchPanel").objectReferenceValue = directPanel;
        serializedView.FindProperty("randomMatchPanel").objectReferenceValue = randomPanel;
        serializedView.FindProperty("matchingPanel").objectReferenceValue = matchingPanel;
        serializedView.FindProperty("directMatchButton").objectReferenceValue = directMatchButton;
        serializedView.FindProperty("randomMatchButton").objectReferenceValue = randomMatchButton;
        serializedView.FindProperty("createRoomButton").objectReferenceValue = createRoomButton;
        serializedView.FindProperty("joinRoomButton").objectReferenceValue = joinRoomButton;
        serializedView.FindProperty("directBackButton").objectReferenceValue = directBackButton;
        serializedView.FindProperty("joinCodeInput").objectReferenceValue = joinCodeInput;
        serializedView.FindProperty("directJoinCodeText").objectReferenceValue = directJoinCodeText;
        serializedView.FindProperty("startRandomMatchButton").objectReferenceValue = startRandomButton;
        serializedView.FindProperty("randomBackButton").objectReferenceValue = randomBackButton;
        serializedView.FindProperty("cancelMatchButton").objectReferenceValue = cancelMatchButton;
        serializedView.FindProperty("copyJoinCodeButton").objectReferenceValue = copyJoinCodeButton;
        serializedView.FindProperty("statusText").objectReferenceValue = statusText;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    private enum ButtonStyle
    {
        Primary,
        Secondary,
        Accent
    }

    private static void ConfigureNetwork()
    {
        EnsurePlayerPrefab();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        BattlePvpSessionSpawner spawner = Object.FindFirstObjectByType<BattlePvpSessionSpawner>(FindObjectsInactive.Include);
        Component networkManager = FindNetworkManager();
        if (networkManager == null)
        {
            GameObject networkObject = new GameObject("BattlePvpNetworkManager");
            networkManager = AddNetworkManager(networkObject);
            spawner = networkObject.AddComponent<BattlePvpSessionSpawner>();
        }
        else if (spawner == null)
        {
            spawner = networkManager.gameObject.GetComponent<BattlePvpSessionSpawner>();
            if (spawner == null)
            {
                spawner = networkManager.gameObject.AddComponent<BattlePvpSessionSpawner>();
            }
        }

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("playerRelayPrefab").objectReferenceValue =
            prefab != null ? prefab.GetComponent<BattlePvpInputRelay>() : null;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spawner);

        SerializedObject serializedNetwork = new SerializedObject(networkManager);
        SerializedProperty playerPrefabProperty = serializedNetwork.FindProperty("NetworkConfig.PlayerPrefab");
        if (playerPrefabProperty != null && prefab != null)
        {
            playerPrefabProperty.objectReferenceValue = prefab;
        }

        SerializedProperty autoSpawnProperty = serializedNetwork.FindProperty("NetworkConfig.AutoSpawnPlayerPrefabClientSide");
        if (autoSpawnProperty != null)
        {
            autoSpawnProperty.boolValue = false;
        }

        SerializedProperty maxClientsProperty = serializedNetwork.FindProperty("NetworkConfig.MaxConnectedClients");
        if (maxClientsProperty != null)
        {
            maxClientsProperty.intValue = 2;
        }

        // 独自のシーン管理を使うためNGOのネットワークシーン同期を無効化し接続時のシーンリロードを防ぐ
        SerializedProperty sceneManagementProperty = serializedNetwork.FindProperty("NetworkConfig.EnableSceneManagement");
        if (sceneManagementProperty != null)
        {
            sceneManagementProperty.boolValue = false;
        }

        WireNetworkTransport(serializedNetwork, networkManager.gameObject);
        serializedNetwork.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(networkManager);
    }

    private static void WireNetworkTransport(SerializedObject serializedNetwork, GameObject networkHost)
    {
        System.Type transportType = ResolveType("Unity.Netcode.Transports.UTP.UnityTransport, Unity.Netcode.Runtime");
        if (transportType == null)
        {
            return;
        }

        Component transport = networkHost.GetComponent(transportType);
        if (transport == null)
        {
            transport = networkHost.AddComponent(transportType);
        }

        SerializedProperty transportProperty = serializedNetwork.FindProperty("NetworkConfig.NetworkTransport");
        if (transportProperty != null)
        {
            transportProperty.objectReferenceValue = transport;
        }
    }

    private static void ConfigureLifetimeScope()
    {
        System.Type lifetimeScopeType = ResolveType(BattlePvpLifetimeScopeTypeName);
        if (lifetimeScopeType == null)
        {
            return;
        }

        Component lifetimeScope = Object.FindFirstObjectByType(lifetimeScopeType, FindObjectsInactive.Include) as Component;
        if (lifetimeScope == null)
        {
            return;
        }

        SerializedObject serializedScope = new SerializedObject(lifetimeScope);
        serializedScope.FindProperty("battlePvpScene").objectReferenceValue =
            Object.FindFirstObjectByType(ResolveType(BattlePvpSceneTypeName));
        serializedScope.FindProperty("battlePvpView").objectReferenceValue =
            Object.FindFirstObjectByType<BattlePVPView>(FindObjectsInactive.Include);
        serializedScope.FindProperty("cameraView").objectReferenceValue =
            Object.FindFirstObjectByType<Camera.View.ClayEditCameraView>(FindObjectsInactive.Include);
        serializedScope.FindProperty("battlePvpFlowRunner").objectReferenceValue =
            Object.FindFirstObjectByType<BattlePvpFlowRunner>(FindObjectsInactive.Include);
        serializedScope.FindProperty("postProcessView").objectReferenceValue =
            Object.FindFirstObjectByType<BattleNpcPostProcessView>(FindObjectsInactive.Include);
        serializedScope.FindProperty("networkManager").objectReferenceValue = FindNetworkManager();
        serializedScope.FindProperty("sessionSpawner").objectReferenceValue =
            Object.FindFirstObjectByType<BattlePvpSessionSpawner>(FindObjectsInactive.Include);
        serializedScope.FindProperty("loadSlotView").objectReferenceValue =
            Object.FindFirstObjectByType<LoadSlotView>(FindObjectsInactive.Include);
        BattlePvpAuxiliaryUiSceneBuilder.EnsureOnScene(
            GameObject.Find("BattlePVPScene")?.transform,
            lifetimeScope);
        serializedScope.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lifetimeScope);
    }

    private static void EnsurePlayerPrefab()
    {
        if (!Directory.Exists("Assets/Resources/BattlePvp"))
        {
            Directory.CreateDirectory("Assets/Resources/BattlePvp");
        }

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (existing != null)
        {
            return;
        }

        var prefabObject = new GameObject("BattlePvpPlayerRelay");
        prefabObject.AddComponent<BattlePvpInputRelay>();
        AddNetworkObject(prefabObject);
        PrefabUtility.SaveAsPrefabAsset(prefabObject, PlayerPrefabPath);
        Object.DestroyImmediate(prefabObject);
        AssetDatabase.ImportAsset(PlayerPrefabPath);
    }

    private static Component FindNetworkManager()
    {
        System.Type networkManagerType = ResolveType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime");
        if (networkManagerType == null)
        {
            return null;
        }

        return Object.FindFirstObjectByType(networkManagerType, FindObjectsInactive.Include) as Component;
    }

    private static Component AddNetworkManager(GameObject host)
    {
        System.Type networkManagerType = ResolveType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime");
        System.Type transportType = ResolveType("Unity.Netcode.Transports.UTP.UnityTransport, Unity.Netcode.Runtime");
        if (networkManagerType == null || transportType == null)
        {
            Debug.LogWarning("[BattlePvpSceneCreator] Netcode package not found");
            return null;
        }

        Component networkManager = host.GetComponent(networkManagerType);
        if (networkManager == null)
        {
            networkManager = host.AddComponent(networkManagerType);
        }

        if (host.GetComponent(transportType) == null)
        {
            host.AddComponent(transportType);
        }

        return networkManager;
    }

    private static void AddNetworkObject(GameObject target)
    {
        System.Type networkObjectType = ResolveType("Unity.Netcode.NetworkObject, Unity.Netcode.Runtime");
        if (networkObjectType == null)
        {
            return;
        }

        if (target.GetComponent(networkObjectType) == null)
        {
            target.AddComponent(networkObjectType);
        }
    }

    private static Canvas FindCanvasByName(string canvasName)
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == canvasName)
            {
                return canvases[i];
            }
        }

        return null;
    }

    private static Canvas CreateOverlayCanvas(Transform parent, string name, int sortingOrder)
    {
        var canvasObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        if (parent != null)
        {
            canvasObject.transform.SetParent(parent, false);
        }

        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        return canvas;
    }

    private static GameObject EnsurePanel(Transform parent, string name, string headerText)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            EnsurePanelHeader(existing, headerText);
            Image existingImage = existing.GetComponent<Image>();
            if (existingImage != null)
            {
                TitleClayUiVisualUtility.ApplyPanel(existingImage);
            }

            return existing.gameObject;
        }

        var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panelObject.GetComponent<Image>();
        TitleClayUiVisualUtility.ApplyPanel(image);
        EnsurePanelHeader(panelObject.transform, headerText);
        return panelObject;
    }

    private static void EnsurePanelHeader(Transform panel, string headerText)
    {
        if (panel == null || string.IsNullOrEmpty(headerText))
        {
            return;
        }

        Transform existing = panel.Find("PanelHeader");
        TextMeshProUGUI header;
        if (existing != null)
        {
            header = existing.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            var headerObject = new GameObject("PanelHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerObject.transform.SetParent(panel, false);
            RectTransform headerRect = headerObject.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(900f, 72f);
            headerRect.anchoredPosition = new Vector2(0f, -36f);
            header = headerObject.GetComponent<TextMeshProUGUI>();
        }

        header.text = headerText;
        header.fontSize = 42f;
        TitleClayUiVisualUtility.ApplyTitleText(header);
    }

    private static GameObject EnsurePanel(Transform parent, string name)
    {
        return EnsurePanel(parent, name, string.Empty);
    }

    private static Component EnsureCenteredButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPosition,
        ButtonStyle style = ButtonStyle.Primary)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            if (existing is RectTransform existingRect)
            {
                existingRect.anchoredPosition = anchoredPosition;
            }

            Component existingButton = SceneUiLhButtonUtility.FindInChildren(existing);
            ApplyButtonStyle(existingButton, style);
            return existingButton;
        }

        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520f, 72f);
        rect.anchoredPosition = anchoredPosition;

        Component button = SceneUiLhButtonUtility.AddComponent(buttonObject);
        ApplyButtonStyle(button, style);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 30f;
        text.raycastTarget = false;
        return button;
    }

    private static void ApplyButtonStyle(Component button, ButtonStyle style)
    {
        switch (style)
        {
            case ButtonStyle.Secondary:
                SceneUiLhButtonUtility.ApplySecondaryButton(button);
                break;
            case ButtonStyle.Accent:
                SceneUiLhButtonUtility.ApplyAccentButton(button);
                break;
            default:
                SceneUiLhButtonUtility.ApplyPrimaryButton(button);
                break;
        }
    }

    private static TMP_InputField EnsureJoinCodeInput(Transform parent, Vector2 anchoredPosition)
    {
        Transform existing = parent.Find("JoinCodeInput") ?? parent.Find("RoomCodeInput");
        if (existing != null)
        {
            existing.name = "JoinCodeInput";
            if (existing is RectTransform existingRect)
            {
                existingRect.anchoredPosition = anchoredPosition;
            }

            return existing.GetComponent<TMP_InputField>();
        }

        var inputObject = new GameObject("JoinCodeInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(parent, false);
        RectTransform rect = inputObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 64f);
        rect.anchoredPosition = anchoredPosition;

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(inputObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 28f;

        var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderObject.transform.SetParent(inputObject.transform, false);
        RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(12f, 8f);
        placeholderRect.offsetMax = new Vector2(-12f, -8f);
        TextMeshProUGUI placeholder = placeholderObject.GetComponent<TextMeshProUGUI>();
        placeholder.text = "参加コードを入力";
        placeholder.fontSize = 24f;
        placeholder.color = new Color(1f, 1f, 1f, 0.45f);

        TMP_InputField inputField = inputObject.GetComponent<TMP_InputField>();
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.characterLimit = 12;
        inputField.contentType = TMP_InputField.ContentType.Alphanumeric;
        TitleClayUiVisualUtility.ApplyInputField(inputField);
        return inputField;
    }

    private static TMP_Text EnsureStatusText(Transform parent, string name, Vector2 anchoredPosition)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            if (existing is RectTransform existingRect)
            {
                existingRect.anchoredPosition = anchoredPosition;
            }

            return existing.GetComponent<TMP_Text>();
        }

        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 160f);
        rect.anchoredPosition = anchoredPosition;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 28f;
        text.textWrappingMode = TextWrappingModes.Normal;
        TitleClayUiVisualUtility.ApplyStatusText(text);
        return text;
    }

    private static void DisableComponentsInScene<T>() where T : Behaviour
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            components[i].enabled = false;
        }
    }

    private static void RemoveComponentsInScene(string typeName)
    {
        System.Type componentType = ResolveType(typeName);
        if (componentType == null)
        {
            return;
        }

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Component[] components = roots[rootIndex].GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && componentType.IsInstanceOfType(component))
                {
                    Object.DestroyImmediate(component);
                }
            }
        }
    }

    private static Component GetOrAddComponent(GameObject host, string typeName)
    {
        System.Type type = ResolveType(typeName);
        if (type == null)
        {
            return null;
        }

        Component component = host.GetComponent(type);
        if (component == null)
        {
            component = host.AddComponent(type);
        }

        return component;
    }

    private static void EnsureBuildSettings()
    {
        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(TargetScenePath)))
        {
            return;
        }

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == TargetScenePath)
            {
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(TargetScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
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
