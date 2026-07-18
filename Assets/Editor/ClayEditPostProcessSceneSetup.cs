using Scene.BattleNpcScene;
using Scene.BattleNpcScene.View;
using Scene.ClayEditScene;
using Scene.ClayEditScene.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ClayEditシーンを黒背景へ戻ぁE/// 教室Volumeは外し教室3点照明�E残してメチE��ュ色味を他シーンと揁E��めE/// </summary>
public static class ClayEditPostProcessSceneSetup
{
    private const string ScenePath = "Assets/Scenes/ClayEdit.unity";

    [MenuItem("Tools/ClayEdit/Restore ClayEdit Post Process")]
    public static void RestoreClayEditPostProcess()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ClayEditLifetimeScope lifetimeScope = Object.FindFirstObjectByType<ClayEditLifetimeScope>(
            FindObjectsInactive.Include);
        if (lifetimeScope == null)
        {
            EditorUtility.DisplayDialog(
                "ClayEdit Post Process",
                "ClayEditLifetimeScopeが見つかりません",
                "OK");
            return;
        }

        Transform hostTransform = lifetimeScope.transform.Find("PostProcess");
        GameObject host = hostTransform != null
            ? hostTransform.gameObject
            : new GameObject("PostProcess");
        if (hostTransform == null)
        {
            host.transform.SetParent(lifetimeScope.transform, false);
            Undo.RegisterCreatedObjectUndo(host, "Create ClayEdit PostProcess");
        }

        BattleNpcPostProcessView classroomPostProcess =
            host.GetComponent<BattleNpcPostProcessView>();
        if (classroomPostProcess != null)
        {
            Undo.DestroyObjectImmediate(classroomPostProcess);
        }

        ClayEditPostProcessView clayEditPostProcess = host.GetComponent<ClayEditPostProcessView>();
        if (clayEditPostProcess == null)
        {
            clayEditPostProcess = Undo.AddComponent<ClayEditPostProcessView>(host);
        }

        SerializedObject postProcessSerialized = new SerializedObject(clayEditPostProcess);
        postProcessSerialized.FindProperty("backgroundColor").colorValue = Color.black;
        postProcessSerialized.ApplyModifiedPropertiesWithoutUndo();

        Volume volume = host.GetComponent<Volume>();
        if (volume != null)
        {
            Undo.DestroyObjectImmediate(volume);
        }

        BattleClassroomLighting lighting = lifetimeScope.GetComponent<BattleClassroomLighting>();
        if (lighting == null)
        {
            lighting = Undo.AddComponent<BattleClassroomLighting>(lifetimeScope.gameObject);
        }

        lighting.enabled = true;

        SerializedObject scopeSerialized = new SerializedObject(lifetimeScope);
        SerializedProperty postProcessProperty = scopeSerialized.FindProperty("clayEditPostProcessView");
        if (postProcessProperty != null)
        {
            postProcessProperty.objectReferenceValue = clayEditPostProcess;
        }

        scopeSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lifetimeScope);
        EditorUtility.SetDirty(clayEditPostProcess);
        EditorUtility.SetDirty(lighting);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog(
            "ClayEdit Post Process",
            "黒背景のみに戻し教室照明を残してシーンを保存しました",
            "OK");
    }
}
