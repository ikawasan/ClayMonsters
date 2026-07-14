using Camera.View;
using Unity.Cinemachine;
using UnityEngine;

namespace Camera.Utility
{
    /// <summary>
    /// シーン遷移時のカメラ入退場をブレンドなしで制御する
    /// FadeInSceneBaseとClayMonstersSceneManagerから全シーン共通で呼び出す
    /// </summary>
    public static class SceneCameraEntryCoordinator
    {
        /// <summary>
        /// 全シーンのVCamを無効化してBrainのブレンド対象をクリアする
        /// 遷移先シーン入場前に呼び出す
        /// </summary>
        public static void DeactivateAllCameras()
        {
            ClayEditCameraView[] views = UnityEngine.Object.FindObjectsByType<ClayEditCameraView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < views.Length; i++)
            {
                views[i].SetCameraEnable(false);
            }
        }

        /// <summary>
        /// シーン内のClayEditCameraViewを即座にスナップする
        /// </summary>
        public static void PrepareForSceneFadeIn()
        {
            ClayEditCameraView[] views = UnityEngine.Object.FindObjectsByType<ClayEditCameraView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < views.Length; i++)
            {
                views[i].PrepareSceneEntry();
            }
        }

        /// <summary>
        /// Brainの出力カメラをアクティブVCamの最終姿勢へ即時同期する
        /// </summary>
        public static void SnapBrainToActiveCamera()
        {
            CinemachineBrain brain = UnityEngine.Object.FindFirstObjectByType<CinemachineBrain>();
            if (brain == null || brain.ActiveVirtualCamera == null)
            {
                return;
            }

            CameraState state = brain.ActiveVirtualCamera.State;
            GameObject controlledObject = brain.ControlledObject;
            if (controlledObject == null)
            {
                return;
            }

            Transform target = controlledObject.transform;
            target.SetPositionAndRotation(state.GetFinalPosition(), state.GetFinalOrientation());
        }

        /// <summary>
        /// シーン入場時に抑えていたオービットダンピングを復元する
        /// </summary>
        public static void ReleaseOrbitDamping()
        {
            ClayEditCameraView[] views = UnityEngine.Object.FindObjectsByType<ClayEditCameraView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < views.Length; i++)
            {
                views[i].ReleaseSceneEntryDamping();
            }
        }
    }
}
