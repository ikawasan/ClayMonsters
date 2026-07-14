#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Editorツールはシーン配置済みUIのレイアウトを変更しない
/// RectTransformの調整はHierarchy上の手動編集のみを正とする
/// </summary>
public static class SceneUiEditorLayoutPolicy
{
    /// <summary>
    /// 既存UIのRectTransformをツールから変更してよいか
    /// 新規作成時のみtrue
    /// </summary>
    public static bool MayApplyLayout(bool createdNewObject)
    {
        return createdNewObject;
    }

    /// <summary>
    /// 新規作成時だけレイアウトを適用する
    /// </summary>
    public static void ApplyLayoutIfCreated(RectTransform rect, bool createdNewObject, System.Action<RectTransform> applyLayout)
    {
        if (!MayApplyLayout(createdNewObject) || rect == null || applyLayout == null)
        {
            return;
        }

        applyLayout(rect);
    }
}
#endif
