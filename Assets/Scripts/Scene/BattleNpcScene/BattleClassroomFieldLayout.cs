using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scene.BattleNpcScene
{
    /// <summary>
    /// BattleClassroom教室の戦闘フィールド配置定数と参照解決
    /// ClayEdit・Trainingのフィールド表示で共用する
    /// </summary>
    public static class BattleClassroomFieldLayout
    {
        /// <summary>
        /// 戦闘用教室ルートのオブジェクト名
        /// </summary>
        public const string FieldObjectName = "Field";

        private static readonly string[] PreferredRootNames =
        {
            "TrainingScene",
            "BattleNpcScene",
            "BattleNpc",
            "BattlePvpArenaScene"
        };

        public const float FieldFloorLocalY = 0.605f;
        public const float HorizontalAngle = 0f;
        public const float VerticalAngle = 10f;
        public const float CameraDistance = 7.5f;
        public const float FocusHeightOffset = 0.75f;

        /// <summary>
        /// 非アクティブ含むFieldオブジェクトを探す
        /// </summary>
        public static GameObject FindFieldObject()
        {
            UnityEngine.SceneManagement.Scene activeScene = SceneManager.GetActiveScene();

            GameObject rootedInActiveScene = FindFieldUnderPreferredRoots(activeScene, preferActiveScene: true);
            if (rootedInActiveScene != null)
            {
                return rootedInActiveScene;
            }

            // 他シーンのpreferredより先にアクティブシーンのFieldを優先する
            GameObject scannedInActiveScene = FindFieldByTransformScan(activeScene, requireActiveScene: true);
            if (scannedInActiveScene != null)
            {
                return scannedInActiveScene;
            }

            GameObject rootedAny = FindFieldUnderPreferredRoots(activeScene, preferActiveScene: false);
            if (rootedAny != null)
            {
                return rootedAny;
            }

            return FindFieldByTransformScan(activeScene, requireActiveScene: false);
        }

        /// <summary>
        /// 戦闘用Fieldの表示を切り替える
        /// </summary>
        /// <param name="isVisible">表示するか</param>
        public static void SetFieldVisible(bool isVisible)
        {
            GameObject field = FindFieldObject();
            if (field == null)
            {
                return;
            }

            if (field.activeSelf != isVisible)
            {
                field.SetActive(isVisible);
            }
        }

        private static GameObject FindFieldUnderPreferredRoots(
            UnityEngine.SceneManagement.Scene activeScene,
            bool preferActiveScene)
        {
            for (int i = 0; i < PreferredRootNames.Length; i++)
            {
                GameObject root = FindSceneRootByName(PreferredRootNames[i], activeScene, preferActiveScene);
                if (root == null)
                {
                    continue;
                }

                Transform fieldTransform = root.transform.Find(FieldObjectName);
                if (fieldTransform == null)
                {
                    continue;
                }

                GameObject field = fieldTransform.gameObject;
                if (field.scene.IsValid())
                {
                    return field;
                }
            }

            return null;
        }

        private static GameObject FindSceneRootByName(
            string objectName,
            UnityEngine.SceneManagement.Scene activeScene,
            bool preferActiveScene)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current == null || current.name != objectName || current.parent != null)
                {
                    continue;
                }

                GameObject candidate = current.gameObject;
                if (!candidate.scene.IsValid())
                {
                    continue;
                }

                if (preferActiveScene
                    && activeScene.IsValid()
                    && candidate.scene != activeScene)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static GameObject FindFieldByTransformScan(
            UnityEngine.SceneManagement.Scene activeScene,
            bool requireActiveScene)
        {
            GameObject fallback = null;
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform current = transforms[i];
                if (current == null || current.name != FieldObjectName)
                {
                    continue;
                }

                GameObject candidate = current.gameObject;
                if (!candidate.scene.IsValid())
                {
                    continue;
                }

                if (requireActiveScene)
                {
                    if (activeScene.IsValid() && candidate.scene == activeScene)
                    {
                        return candidate;
                    }

                    continue;
                }

                fallback ??= candidate;
            }

            return fallback;
        }
    }
}