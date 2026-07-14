#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// Editor破棄前にInspector選択を外す
/// 再コンパイル直前のみ自動で選択解除する
/// </summary>
[InitializeOnLoad]
public static class SceneUiEditorSelectionUtility
{
    private static bool isSanitizing;

    static SceneUiEditorSelectionUtility()
    {
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += RemoveDestroyedSelectionOnUpdate;
    }

    /// <summary>
    /// 無効なInspector選択を手動でリセットする
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Clear Invalid Inspector Selection")]
    public static void ClearInvalidInspectorSelectionMenu()
    {
        ResetInspectorSelection();
        Debug.Log("[SceneUiEditorSelectionUtility] Inspector選択をリセットしました");
    }

    /// <summary>
    /// Inspector選択を安全にリセットする
    /// </summary>
    public static void ResetInspectorSelectionSafe()
    {
        ResetInspectorSelection();
        ScheduleResetInspectorSelection();
    }

    /// <summary>
    /// アクティブシーンからMissing Scriptを除去する
    /// </summary>
    [MenuItem("Tools/ClayMonsters/Remove Missing Scripts In Active Scene")]
    public static void RemoveMissingScriptsInActiveSceneMenu()
    {
        UnityScene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("[SceneUiEditorSelectionUtility] アクティブシーンがありません");
            return;
        }

        int removedCount = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            removedCount += RemoveMissingScriptsRecursive(roots[i].transform);
        }

        ResetInspectorSelection();
        Debug.Log(
            $"[SceneUiEditorSelectionUtility] Missing Scriptを{removedCount}件除去しました"
            + " シーンは未保存です");
    }

    /// <summary>
    /// 対象が選択中なら選択を解除する
    /// </summary>
    public static void ClearIfSelected(Object target)
    {
        if (target == null)
        {
            return;
        }

        GameObject gameObject = target as GameObject;
        if (gameObject == null && target is Component component)
        {
            gameObject = component.gameObject;
        }

        if (gameObject != null)
        {
            if (Selection.activeGameObject == gameObject)
            {
                Selection.activeGameObject = null;
                return;
            }

            Object activeObject = Selection.activeObject;
            if (activeObject is Component activeComponent
                && activeComponent.gameObject == gameObject)
            {
                Selection.activeObject = null;
                return;
            }
        }

        if (Selection.activeObject == target)
        {
            Selection.activeObject = null;
        }
    }

    /// <summary>
    /// 選択解除後に即時破棄する
    /// </summary>
    public static void DestroyImmediateSafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        ClearIfSelected(target);
        ResetInspectorSelection();
        Object.DestroyImmediate(target);
        ScheduleResetInspectorSelection();
    }

    private static void OnBeforeAssemblyReload()
    {
        ResetInspectorSelection();
    }

    private static void OnAfterAssemblyReload()
    {
        ScheduleResetInspectorSelection();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode
            || state == PlayModeStateChange.EnteredEditMode)
        {
            ResetInspectorSelection();
            ScheduleResetInspectorSelection();
        }
    }

    private static void RemoveDestroyedSelectionOnUpdate()
    {
        if (!HasDestroyedSelection())
        {
            return;
        }

        ResetInspectorSelection();
    }

    private static bool HasDestroyedSelection()
    {
        Object[] selectedObjects = Selection.objects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            if (selectedObjects[i] == null)
            {
                return true;
            }
        }

        if (Selection.activeInstanceID != 0 && Selection.activeObject == null)
        {
            return true;
        }

        return false;
    }

    private static void ScheduleResetInspectorSelection()
    {
        EditorApplication.delayCall -= ResetInspectorSelection;
        EditorApplication.delayCall += ResetInspectorSelection;
    }

    private static void ResetInspectorSelection()
    {
        if (isSanitizing)
        {
            return;
        }

        isSanitizing = true;
        try
        {
            Selection.objects = System.Array.Empty<Object>();
            Selection.instanceIDs = System.Array.Empty<int>();
            Selection.activeInstanceID = 0;
            Selection.activeObject = null;

            ActiveEditorTracker.sharedTracker.isLocked = false;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
        finally
        {
            isSanitizing = false;
        }
    }

    private static int RemoveMissingScriptsRecursive(Transform root)
    {
        if (root == null)
        {
            return 0;
        }

        int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root.gameObject);
        for (int i = 0; i < root.childCount; i++)
        {
            removedCount += RemoveMissingScriptsRecursive(root.GetChild(i));
        }

        return removedCount;
    }
}
#endif
