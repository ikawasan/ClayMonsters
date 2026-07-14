#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// ClayEditシーンへ入場フロー用UIを配置する
/// </summary>
public static class ClayEditEntrySceneSetup
{
    private const string ScenePath = "Assets/Scenes/ClayEdit.unity";

    [MenuItem("Tools/ClayEdit/Setup Entry Flow UI")]
    public static void Setup()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SceneUiPlacementMigrator.MigrateClayEditEntryFlowUi();
        SceneUiEditorSavePolicy.MarkDirty(scene);
        EditorUtility.DisplayDialog(
            "ClayEdit Entry Flow",
            "入場フローUIの配置が完了しました\nシーンは未保存です。必要に応じて手動で保存してください",
            "OK");
    }
}
#endif
