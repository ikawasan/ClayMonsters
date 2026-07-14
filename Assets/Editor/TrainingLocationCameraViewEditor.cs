#if UNITY_EDITOR
using Camera.Interface;
using Camera.View;
using Scene.TrainingScene;
using Scene.TrainingScene.Domain;
using Scene.TrainingScene.View;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 育成カメラ構図の実行中キャプチャとInspector反映を担当する
/// </summary>
[CustomEditor(typeof(TrainingLocationCameraView))]
public sealed class TrainingLocationCameraViewEditor : UnityEditor.Editor
{
    private const string CaptureTargetKey = "TrainingLocationCameraViewEditor.CaptureTarget";
    private const string CaptureLocationKey = "TrainingLocationCameraViewEditor.CaptureLocation";
    private const string EnableCameraTuningKey = "TrainingLocationCameraViewEditor.EnableCameraTuning";

    private TrainingLocationCameraTarget captureTarget = TrainingLocationCameraTarget.Default;
    private TrainingLocation captureLocation = TrainingLocation.ScienceLab;
    private bool enableCameraTuning;

    [InitializeOnLoadMethod]
    private static void RegisterPlayModeBridge()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnEnable()
    {
        captureTarget = (TrainingLocationCameraTarget)EditorPrefs.GetInt(
            CaptureTargetKey,
            (int)TrainingLocationCameraTarget.Default);
        captureLocation = (TrainingLocation)EditorPrefs.GetInt(
            CaptureLocationKey,
            (int)TrainingLocation.ScienceLab);
        enableCameraTuning = EditorPrefs.GetBool(EnableCameraTuningKey, false);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);
        DrawCaptureSection();
    }

    private void DrawCaptureSection()
    {
        EditorGUILayout.LabelField("実行中キャプチャ", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Play中にカメラを調整してから反映先を選び「現在のカメラ構図を反映」を押してください。"
            + " Play終了後もシーンへ書き戻します。",
            MessageType.Info);

        captureTarget = (TrainingLocationCameraTarget)EditorGUILayout.EnumPopup("反映先", captureTarget);
        EditorPrefs.SetInt(CaptureTargetKey, (int)captureTarget);
        if (captureTarget == TrainingLocationCameraTarget.Location)
        {
            captureLocation = (TrainingLocation)EditorGUILayout.EnumPopup("行き先", captureLocation);
            EditorPrefs.SetInt(CaptureLocationKey, (int)captureLocation);
        }

        if (Application.isPlaying)
        {
            EditorGUI.BeginChangeCheck();
            enableCameraTuning = EditorGUILayout.ToggleLeft(
                "調整用にカメラ操作を有効化",
                enableCameraTuning);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(EnableCameraTuningKey, enableCameraTuning);
                ApplyCameraTuningState(enableCameraTuning);
            }

            if (enableCameraTuning)
            {
                EditorGUILayout.HelpBox(
                    "Gameビューをクリックしてから操作してください。\n"
                    + "右ドラッグ: 回転\n"
                    + "Shift+右ドラッグ / 中ボタンドラッグ: 注視点移動\n"
                    + "ホイール: ズーム",
                    MessageType.Warning);
            }
        }

        using (new EditorGUI.DisabledScope(!CanCapture()))
        {
            if (GUILayout.Button("現在のカメラ構図を反映", GUILayout.Height(28f)))
            {
                CaptureCurrentOrbit();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "編集モードではClayEditCameraの現在値を読み取って反映します。",
                MessageType.None);
        }
    }

    private bool CanCapture()
    {
        return target is TrainingLocationCameraView view && TryReadCapture(view, out _);
    }

    private void CaptureCurrentOrbit()
    {
        if (target is not TrainingLocationCameraView view)
        {
            return;
        }

        if (!TryReadCapture(view, out TrainingLocationOrbitCapture capture))
        {
            EditorUtility.DisplayDialog(
                "育成カメラ構図",
                "現在のカメラ構図を取得できませんでした。",
                "OK");
            return;
        }

        Undo.RecordObject(view, "Capture Training Location Camera");
        if (!view.TryApplyCapturedOrbit(captureTarget, captureLocation, capture))
        {
            EditorUtility.DisplayDialog(
                "育成カメラ構図",
                "選択した反映先へ書き込めませんでした。",
                "OK");
            return;
        }

        EditorUtility.SetDirty(view);
        serializedObject.Update();

        if (Application.isPlaying)
        {
            TrainingLocationCameraPlayModeBridge.QueueCapture(
                view,
                captureTarget,
                captureLocation,
                capture);
        }
        else
        {
            MarkSceneDirty(view);
        }

        Debug.Log(BuildCaptureLog(captureTarget, captureLocation, capture));
    }

    private static bool TryReadCapture(
        TrainingLocationCameraView view,
        out TrainingLocationOrbitCapture capture)
    {
        if (view.TryCaptureCurrentOrbit(out capture))
        {
            return true;
        }

        ClayEditCameraView clayCamera = FindClayEditCameraView();
        if (clayCamera == null || !clayCamera.TryGetOrbitState(
            out float horizontalAngle,
            out float verticalAngle,
            out float distance,
            out Vector3 focus))
        {
            capture = default;
            return false;
        }

        Vector3 center = Vector3.zero;
        TrainingDisplay display = view.GetComponentInParent<TrainingDisplay>()
            ?? UnityEngine.Object.FindFirstObjectByType<TrainingDisplay>(FindObjectsInactive.Include);
        if (display != null)
        {
            display.TryGetDisplayFocusCenter(out center);
        }

        Vector3 delta = focus - center;
        float focusHeightOffset = delta.y;
        Vector3 screenRight = Battle.BattleFieldScreenAxis.ResolveScreenRight(horizontalAngle);
        Vector3 planar = delta - Vector3.up * focusHeightOffset;
        float focusSideOffset = Vector3.Dot(planar, screenRight);
        capture = new TrainingLocationOrbitCapture(
            horizontalAngle,
            verticalAngle,
            distance,
            focusHeightOffset,
            focusSideOffset);
        return true;
    }

    private void ApplyCameraTuningState(bool enabled)
    {
        if (target is not TrainingLocationCameraView view)
        {
            return;
        }

        view.SetTuningEnabled(enabled);
    }

    private static ClayEditCameraView FindClayEditCameraView()
    {
        return UnityEngine.Object.FindFirstObjectByType<ClayEditCameraView>(FindObjectsInactive.Include);
    }

    private static string BuildCaptureLog(
        TrainingLocationCameraTarget target,
        TrainingLocation location,
        TrainingLocationOrbitCapture capture)
    {
        string targetName = target switch
        {
            TrainingLocationCameraTarget.Default => "Default",
            TrainingLocationCameraTarget.Rest => "Rest",
            TrainingLocationCameraTarget.Location =>
                $"{TrainingLocationCatalog.GetDisplayName(location)}({location})",
            _ => target.ToString()
        };

        return "[TrainingLocationCamera] 反映完了 "
            + $"{targetName} "
            + $"H={capture.HorizontalAngle:F1} V={capture.VerticalAngle:F1} D={capture.Distance:F2} "
            + $"FocusH={capture.FocusHeightOffset:F2} FocusS={capture.FocusSideOffset:F2}";
    }

    private static void MarkSceneDirty(Component component)
    {
        if (component == null)
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        TrainingLocationCameraPlayModeBridge.FlushPendingCaptures();
        EditorPrefs.SetBool(EnableCameraTuningKey, false);
    }
}

/// <summary>
/// Play終了後に育成カメラ構図をシーンへ書き戻す
/// </summary>
internal static class TrainingLocationCameraPlayModeBridge
{
    private sealed class PendingCapture
    {
        public string ScenePath;
        public string HierarchyPath;
        public TrainingLocationCameraTarget Target;
        public TrainingLocation Location;
        public TrainingLocationOrbitCapture Capture;
    }

    private static readonly List<PendingCapture> PendingCaptures = new List<PendingCapture>();

    /// <summary>
    /// Play中キャプチャを退避する
    /// </summary>
    public static void QueueCapture(
        TrainingLocationCameraView view,
        TrainingLocationCameraTarget target,
        TrainingLocation location,
        TrainingLocationOrbitCapture capture)
    {
        if (view == null)
        {
            return;
        }

        string scenePath = view.gameObject.scene.path;
        string hierarchyPath = BuildHierarchyPath(view.transform);
        for (int i = PendingCaptures.Count - 1; i >= 0; i--)
        {
            PendingCapture pending = PendingCaptures[i];
            if (pending.ScenePath == scenePath && pending.HierarchyPath == hierarchyPath
                && pending.Target == target
                && (target != TrainingLocationCameraTarget.Location || pending.Location == location))
            {
                PendingCaptures.RemoveAt(i);
            }
        }

        PendingCaptures.Add(new PendingCapture
        {
            ScenePath = scenePath,
            HierarchyPath = hierarchyPath,
            Target = target,
            Location = location,
            Capture = capture
        });
    }

    /// <summary>
    /// 退避したキャプチャをシーンへ反映する
    /// </summary>
    public static void FlushPendingCaptures()
    {
        if (PendingCaptures.Count == 0)
        {
            return;
        }

        PendingCapture[] captures = PendingCaptures.ToArray();
        PendingCaptures.Clear();

        for (int i = 0; i < captures.Length; i++)
        {
            ApplyPendingCapture(captures[i]);
        }
    }

    private static void ApplyPendingCapture(PendingCapture pending)
    {
        if (pending == null || string.IsNullOrEmpty(pending.ScenePath))
        {
            return;
        }

        TrainingLocationCameraView view = FindViewInOpenScene(pending.ScenePath, pending.HierarchyPath);
        if (view == null)
        {
            Debug.LogWarning(
                $"[TrainingLocationCamera] Play終了後の書き戻し先が見つかりません: {pending.HierarchyPath}");
            return;
        }

        Undo.RecordObject(view, "Capture Training Location Camera");
        if (!view.TryApplyCapturedOrbit(pending.Target, pending.Location, pending.Capture))
        {
            Debug.LogWarning(
                $"[TrainingLocationCamera] Play終了後の書き戻しに失敗しました: {pending.HierarchyPath}");
            return;
        }

        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        Debug.Log(
            $"[TrainingLocationCamera] Play終了後にシーンへ書き戻しました: {pending.HierarchyPath}");
    }

    private static TrainingLocationCameraView FindViewInOpenScene(string scenePath, string hierarchyPath)
    {
        TrainingLocationCameraView[] views = UnityEngine.Object.FindObjectsByType<TrainingLocationCameraView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < views.Length; i++)
        {
            TrainingLocationCameraView view = views[i];
            if (view == null || view.gameObject.scene.path != scenePath)
            {
                continue;
            }

            if (BuildHierarchyPath(view.transform) == hierarchyPath)
            {
                return view;
            }
        }

        return null;
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
#endif
