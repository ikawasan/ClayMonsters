using UnityEngine;
using UnityEditor;
using TMPro;

public class TMPFontChanger : EditorWindow
{
    private TMP_FontAsset newFontAsset;
    private Material newMaterialPreset;

    [MenuItem("Tools/TMP Font Changer")]
    public static void ShowWindow()
    {
        GetWindow<TMPFontChanger>("TMP Font Changer");
    }

    private void OnGUI()
    {
        GUILayout.Label("シーン内の全TMPフォントを一括変更", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        newFontAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("新しいフォントアセット", newFontAsset, typeof(TMP_FontAsset), false);
        newMaterialPreset = (Material)EditorGUILayout.ObjectField("マテリアルプリセット (任意)", newMaterialPreset, typeof(Material), false);

        EditorGUILayout.Space();

        if (GUILayout.Button("シーン内の全フォントを変更する"))
        {
            ChangeAllFonts();
        }
    }

    private void ChangeAllFonts()
    {
        if (newFontAsset == null)
        {
            EditorUtility.DisplayDialog("エラー", "変更後のフォントアセットを指定してください。", "OK");
            return;
        }

        // シーン内のすべてのTMP_Text（非アクティブも含む）を検索
        TMP_Text[] allTextComponents = Resources.FindObjectsOfTypeAll<TMP_Text>();
        int count = 0;

        foreach (TMP_Text tmp in allTextComponents)
        {
            // アセットやプレハブそのものは除外（シーン内のオブジェクトのみ対象）
            if (!EditorUtility.IsPersistent(tmp.transform.root.gameObject) && tmp.gameObject.scene.isLoaded)
            {
                // Undo機能に対応させる（Ctrl+Zで戻せるようにする）
                Undo.RecordObject(tmp, "Change TMP Font");

                tmp.font = newFontAsset;

                if (newMaterialPreset != null)
                {
                    tmp.fontSharedMaterial = newMaterialPreset;
                }

                // 変更を確定させ、画面を更新
                EditorUtility.SetDirty(tmp);
                count++;
            }
        }

        // シーンを「未保存（*マーク）」状態にして、変更を確実に保存できるようにする
        if (count > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        EditorUtility.DisplayDialog("完了", $"{count} 個の TMP_Text のフォントを変更しました！", "OK");
    }
}