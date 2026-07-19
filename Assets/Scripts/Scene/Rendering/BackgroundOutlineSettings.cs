using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Scene.Rendering
{
    /// <summary>
    /// 背景輪郭線の調整用設定
    /// VolumeProfileとは分離し実行時削除を避ける
    /// </summary>
    [CreateAssetMenu(
        fileName = "BackgroundOutlineSettings",
        menuName = "ClayMonsters/Background Outline Settings")]
    public sealed class BackgroundOutlineSettings : ScriptableObject
    {
        private const string ResourcesAssetPath = "Assets/Resources/BackgroundOutlineSettings.asset";
        private const string SettingsAssetPath = "Assets/Settings/BackgroundOutlineSettings.asset";

        [SerializeField, Range(0f, 1f)] private float intensity = 0.85f;
        [SerializeField, Range(0.25f, 8f)] private float thickness = 1.1f;
        [FormerlySerializedAs("depthSensitivity")]
        [SerializeField, Range(0.1f, 40f)] private float normalSensitivity = 4f;
        [SerializeField] private Color color = new Color(0.12f, 0.08f, 0.06f, 1f);

        /// <summary>
        /// 輪郭の強さ
        /// </summary>
        public float Intensity => intensity;

        /// <summary>
        /// 輪郭の検出幅
        /// </summary>
        public float Thickness => thickness;

        /// <summary>
        /// 法線差の感度
        /// </summary>
        public float NormalSensitivity => normalSensitivity;

        /// <summary>
        /// 輪郭色
        /// </summary>
        public Color Color => color;

#if UNITY_EDITOR
        private static bool isSyncingMirror;

        private void OnValidate()
        {
            SyncMirrorAsset();
        }

        private void SyncMirrorAsset()
        {
            if (isSyncingMirror)
            {
                return;
            }

            string selfPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(selfPath))
            {
                return;
            }

            string mirrorPath = null;
            if (selfPath == SettingsAssetPath)
            {
                mirrorPath = ResourcesAssetPath;
            }
            else if (selfPath == ResourcesAssetPath)
            {
                mirrorPath = SettingsAssetPath;
            }

            if (string.IsNullOrEmpty(mirrorPath))
            {
                return;
            }

            BackgroundOutlineSettings mirror =
                AssetDatabase.LoadAssetAtPath<BackgroundOutlineSettings>(mirrorPath);
            if (mirror == null || mirror == this)
            {
                return;
            }

            isSyncingMirror = true;
            try
            {
                SerializedObject serializedMirror = new SerializedObject(mirror);
                serializedMirror.FindProperty("intensity").floatValue = intensity;
                serializedMirror.FindProperty("thickness").floatValue = thickness;
                serializedMirror.FindProperty("normalSensitivity").floatValue = normalSensitivity;
                serializedMirror.FindProperty("color").colorValue = color;
                serializedMirror.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mirror);
            }
            finally
            {
                isSyncingMirror = false;
            }
        }
#endif
    }
}
