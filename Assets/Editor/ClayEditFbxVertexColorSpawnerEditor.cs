#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using Scene.ClayEditScene;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ClayEditのFBX頂点カラースポーン操作をInspectorから実行する
/// </summary>
[CustomEditor(typeof(ClayEditFbxVertexColorSpawner))]
public sealed class ClayEditFbxVertexColorSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Play Mode中にボタンを押すとFBXのメッシュとテクスチャを頂点カラーへ変換しClayEdit造形へ取り込みます。"
            + " Spawn Euler Angles で向きを指定できます(Apply Auto Orientation がOFFのとき指定向きがそのまま使われます)。"
            + " Import Scale でグリッドフィット後のサイズ倍率を指定できます(1で従来どおり0.5で半分2で約2倍)。"
            + " Color Subdivision Depth / Max Uv Edge Length で色の細かさを調整できます。"
            + " この機能はEditor専用でROMビルドには含まれません。",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("FBXを頂点カラー変換してスポーン", GUILayout.Height(28f)))
            {
                var spawner = (ClayEditFbxVertexColorSpawner)target;
                SpawnAsync(spawner).Forget();
            }
        }
    }

    private static async UniTaskVoid SpawnAsync(ClayEditFbxVertexColorSpawner spawner)
    {
        (bool success, string errorMessage) = await spawner.SpawnAsync(default);
        if (success)
        {
            Debug.Log("[ClayEditFbxVertexColorSpawner] FBXの頂点カラー変換スポーンに成功しました", spawner);
            return;
        }

        Debug.LogError(
            $"[ClayEditFbxVertexColorSpawner] スポーンに失敗しました: {errorMessage}",
            spawner);
    }
}
#endif
