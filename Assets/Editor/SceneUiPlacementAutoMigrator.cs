#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// 未配置の動的UIを検出してシーンへ配置する
/// 自動実行はシーンの見た目を勝手に書き換えるため無効化しメニューから手動実行する
/// </summary>
public static class SceneUiPlacementAutoMigrator
{
    [MenuItem("Tools/ClayMonsters/Migrate Dynamic UI If Needed")]
    public static void MigrateIfNeeded()
    {
        if (!SceneUiPlacementMigrator.NeedsMigration())
        {
            EditorUtility.DisplayDialog(
                "Scene UI Migration",
                "未配置の動的UIは検出されませんでした",
                "OK");
            return;
        }

        SceneUiPlacementMigrator.MigrateAll();
    }
}
#endif
