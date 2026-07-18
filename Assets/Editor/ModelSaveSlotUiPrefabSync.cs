#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// セーブスロットUIプレハブ保存時のフック
/// クリーンなプレハブインスタンスはUnityが自動反映するため追加処理はしない
/// 再配置メニューは破壊的のため提供しない
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
    }
}
#endif
