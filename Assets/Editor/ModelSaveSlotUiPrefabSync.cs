#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// セーブスロットUIプレハブ保存時のフック
/// クリーンなプレハブインスタンスはUnityが自動反映するため追加処理はしない
/// 古いOverrideが残る場合はToolsの再配置メニューを使う
/// </summary>
public sealed class ModelSaveSlotUiPrefabAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        // 意図的に何もしない
        // Override付きインスタンスはUnityの自動反映が効かないため
        // Tools/ClayMonsters/Sync All Scenes Save Slot UI From Prefabs で再配置する
    }
}
#endif
