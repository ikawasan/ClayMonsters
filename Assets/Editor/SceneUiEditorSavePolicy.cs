#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

/// <summary>
/// ToolsメニューからのUI更新はDirtyのみ付け自動保存しない
/// 保存はユーザーがCtrl+S等で行う
/// </summary>
public static class SceneUiEditorSavePolicy
{
    /// <summary>
    /// シーンを未保存状態にする
    /// </summary>
    public static void MarkDirty(UnityScene scene)
    {
        if (scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    /// <summary>
    /// アクティブシーンを未保存状態にする
    /// </summary>
    public static void MarkActiveDirty()
    {
        MarkDirty(SceneManager.GetActiveScene());
    }

    /// <summary>
    /// バッチモード等で明示的に保存が必要な場合のみ使う
    /// </summary>
    public static void MarkDirtyAndSave(UnityScene scene)
    {
        MarkDirty(scene);
        if (scene.IsValid() && scene.isLoaded)
        {
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
