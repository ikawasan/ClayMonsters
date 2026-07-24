using Battle;
using Camera.Interface;
using Scene.TrainingScene.Domain;
using Scene.TrainingScene.Interface;
using System;
using UnityEngine;
using VContainer;

namespace Scene.TrainingScene.View
{
    /// <summary>
    /// 育成シーンの行き先ごとにオービットカメラ構図を切り替える
    /// 背景切り替えと同時に位置と角度を即時反映する
    /// </summary>
    public sealed class TrainingLocationCameraView : MonoBehaviour, ITrainingLocationCameraView
    {
        /// <summary>
        /// 行き先とカメラ構図の対応
        /// </summary>
        [Serializable]
        private struct LocationOrbitView
        {
            [SerializeField] private TrainingLocation location;
            [SerializeField] private TrainingLocationOrbitSettings orbitSettings;

            /// <summary>
            /// 対応する行き先
            /// </summary>
            public TrainingLocation Location => location;

            /// <summary>
            /// 適用する構図
            /// </summary>
            public TrainingLocationOrbitSettings OrbitSettings => orbitSettings;
        }

        [SerializeField] private TrainingLocationOrbitSettings defaultView = new TrainingLocationOrbitSettings();
        [SerializeField] private TrainingLocationOrbitSettings restView = new TrainingLocationOrbitSettings();
        [SerializeField] private TrainingLocationOrbitSettings roamView = new TrainingLocationOrbitSettings();
        [SerializeField] private TrainingLocationOrbitSettings inheritanceView = new TrainingLocationOrbitSettings();
        [SerializeField] private LocationOrbitView[] locationViews = Array.Empty<LocationOrbitView>();

        [Inject] private readonly IClayEditCameraView cameraView;
        [Inject] private readonly TrainingDisplay trainingDisplay;

        private bool isTuningEnabled;

        /// <summary>
        /// 実行中のカメラ調整モードを切り替える
        /// </summary>
        /// <param name="enabled">有効なら右ドラッグとホイールで操作できる</param>
        public void SetTuningEnabled(bool enabled)
        {
            isTuningEnabled = enabled;
            if (enabled)
            {
                ApplyTuningState();
                return;
            }

            RestoreTuningCameraState();
        }

        private void LateUpdate()
        {
            if (!isTuningEnabled)
            {
                return;
            }

            ApplyTuningState();
        }

        private void OnDisable()
        {
            if (!isTuningEnabled)
            {
                return;
            }

            isTuningEnabled = false;
            RestoreTuningCameraState();
        }

        /// <inheritdoc/>
        public void ApplyDefaultView()
        {
            ApplyOrbitSettings(defaultView);
        }

        /// <inheritdoc/>
        public void ApplyRestView()
        {
            ApplyOrbitSettings(restView != null ? restView : defaultView);
        }

        /// <inheritdoc/>
        public void ApplyRoamView()
        {
            TrainingLocationOrbitSettings settings = roamView != null ? roamView : null;
            if (settings == null)
            {
                settings = restView != null ? restView : defaultView;
            }

            ApplyOrbitSettings(settings, ResolveRoamFocusCenter());
        }

        /// <inheritdoc/>
        public void ApplyLocationView(TrainingLocation location)
        {
            TrainingLocationOrbitSettings settings = FindLocationOrbitSettings(location);
            ApplyOrbitSettings(settings != null ? settings : defaultView);
        }

        /// <inheritdoc/>
        public void ApplyInheritanceView()
        {
            if (cameraView == null)
            {
                return;
            }

            TrainingLocationOrbitSettings settings =
                inheritanceView != null ? inheritanceView : defaultView;
            // 育成モデルはY180で-Z正面のため水平角0で正面を取る
            const float frontHorizontalAngle = 0f;
            float vertical = settings != null ? settings.VerticalAngle : 2f;
            float distance = settings != null ? settings.Distance : 7.5f;
            float heightOffset = settings != null ? settings.FocusHeightOffset : 0.05f;
            float sideOffset = settings != null ? settings.FocusSideOffset : 0f;

            Vector3 center = ResolveModelCenter();
            Vector3 focus = center + Vector3.up * heightOffset;
            if (!Mathf.Approximately(sideOffset, 0f))
            {
                Vector3 screenRight = BattleFieldScreenAxis.ResolveScreenRight(frontHorizontalAngle);
                focus += screenRight * sideOffset;
            }

            cameraView.SetFocusPosition(focus);
            cameraView.SetOrbitView(frontHorizontalAngle, vertical, distance);
        }

        /// <summary>
        /// 現在のカメラ構図をスナップショットとして取得する
        /// </summary>
        /// <param name="capture">取得した構図</param>
        /// <returns>取得できたらtrue</returns>
        public bool TryCaptureCurrentOrbit(out TrainingLocationOrbitCapture capture)
        {
            return TryCaptureCurrentOrbit(TrainingLocationCameraTarget.Default, out capture);
        }

        /// <summary>
        /// 反映先に応じた基準点で現在のカメラ構図を取得する
        /// </summary>
        /// <param name="target">反映先</param>
        /// <param name="capture">取得した構図</param>
        /// <returns>取得できたらtrue</returns>
        public bool TryCaptureCurrentOrbit(
            TrainingLocationCameraTarget target,
            out TrainingLocationOrbitCapture capture)
        {
            capture = default;
            if (cameraView == null || !cameraView.TryGetOrbitState(
                out float horizontalAngle,
                out float verticalAngle,
                out float distance,
                out Vector3 focus))
            {
                return false;
            }

            capture = BuildOrbitCapture(ResolveCaptureCenter(target), horizontalAngle, verticalAngle, distance, focus);
            return true;
        }

        /// <summary>
        /// スナップショットを指定先の設定へ書き込む
        /// </summary>
        /// <param name="target">反映先</param>
        /// <param name="location">行き先 targetがLocationのとき必須</param>
        /// <param name="capture">反映する構図</param>
        /// <returns>反映できたらtrue</returns>
        public bool TryApplyCapturedOrbit(
            TrainingLocationCameraTarget target,
            TrainingLocation location,
            TrainingLocationOrbitCapture capture)
        {
            switch (target)
            {
                case TrainingLocationCameraTarget.Default:
                    if (defaultView == null)
                    {
                        return false;
                    }

                    defaultView.ApplyCapture(capture);
                    return true;
                case TrainingLocationCameraTarget.Rest:
                    if (restView == null)
                    {
                        return false;
                    }

                    restView.ApplyCapture(capture);
                    return true;
                case TrainingLocationCameraTarget.Roam:
                    if (roamView == null)
                    {
                        return false;
                    }

                    roamView.ApplyCapture(capture);
                    return true;
                case TrainingLocationCameraTarget.Inheritance:
                    if (inheritanceView == null)
                    {
                        return false;
                    }

                    inheritanceView.ApplyCapture(capture);
                    return true;
                case TrainingLocationCameraTarget.Location:
                    return TryApplyCapturedOrbitToLocation(location, capture);
                default:
                    return false;
            }
        }

        private bool TryApplyCapturedOrbitToLocation(
            TrainingLocation location,
            TrainingLocationOrbitCapture capture)
        {
            for (int i = 0; i < locationViews.Length; i++)
            {
                if (locationViews[i].Location != location)
                {
                    continue;
                }

                TrainingLocationOrbitSettings settings = locationViews[i].OrbitSettings;
                if (settings == null)
                {
                    return false;
                }

                settings.ApplyCapture(capture);
                return true;
            }

            return false;
        }

        private TrainingLocationOrbitSettings FindLocationOrbitSettings(TrainingLocation location)
        {
            for (int i = 0; i < locationViews.Length; i++)
            {
                if (locationViews[i].Location == location)
                {
                    return locationViews[i].OrbitSettings;
                }
            }

            return null;
        }

        private void ApplyOrbitSettings(TrainingLocationOrbitSettings settings)
        {
            ApplyOrbitSettings(settings, ResolveModelCenter());
        }

        private void ApplyOrbitSettings(
            TrainingLocationOrbitSettings settings,
            Vector3 focusCenter)
        {
            if (cameraView == null || settings == null)
            {
                return;
            }

            Vector3 focus = ResolveFocusPoint(settings, focusCenter);
            cameraView.SetFocusPosition(focus);
            cameraView.SetOrbitView(
                settings.HorizontalAngle,
                settings.VerticalAngle,
                settings.Distance);
        }

        private Vector3 ResolveFocusPoint(
            TrainingLocationOrbitSettings settings,
            Vector3 focusCenter)
        {
            Vector3 focus = focusCenter + Vector3.up * settings.FocusHeightOffset;
            bool hasSide = !Mathf.Approximately(settings.FocusSideOffset, 0f);
            bool hasForward = !Mathf.Approximately(settings.FocusForwardOffset, 0f);
            if (!hasSide && !hasForward)
            {
                return focus;
            }

            if (hasSide)
            {
                Vector3 screenRight = BattleFieldScreenAxis.ResolveScreenRight(settings.HorizontalAngle);
                focus += screenRight * settings.FocusSideOffset;
            }

            if (hasForward)
            {
                Vector3 screenForward = BattleFieldScreenAxis.ResolveCameraForward(settings.HorizontalAngle);
                focus += screenForward * settings.FocusForwardOffset;
            }

            return focus;
        }

        private static TrainingLocationOrbitCapture BuildOrbitCapture(
            Vector3 center,
            float horizontalAngle,
            float verticalAngle,
            float distance,
            Vector3 focus)
        {
            Vector3 delta = focus - center;
            float focusHeightOffset = delta.y;
            float normalizedHorizontal = Mathf.DeltaAngle(0f, horizontalAngle);
            Vector3 screenRight = BattleFieldScreenAxis.ResolveScreenRight(normalizedHorizontal);
            Vector3 screenForward = BattleFieldScreenAxis.ResolveCameraForward(normalizedHorizontal);
            Vector3 planar = delta - Vector3.up * focusHeightOffset;
            float focusSideOffset = Vector3.Dot(planar, screenRight);
            float focusForwardOffset = Vector3.Dot(planar, screenForward);
            return new TrainingLocationOrbitCapture(
                normalizedHorizontal,
                verticalAngle,
                distance,
                focusHeightOffset,
                focusSideOffset,
                focusForwardOffset);
        }

        private Vector3 ResolveCaptureCenter(TrainingLocationCameraTarget target)
        {
            if (target == TrainingLocationCameraTarget.Roam)
            {
                return ResolveRoamFocusCenter();
            }

            return ResolveModelCenter();
        }

        private Vector3 ResolveRoamFocusCenter()
        {
            if (trainingDisplay != null)
            {
                Transform anchor = trainingDisplay.DisplayAnchor;
                if (anchor != null)
                {
                    return anchor.position;
                }
            }

            return ResolveModelCenter();
        }

        private Vector3 ResolveModelCenter()
        {
            if (trainingDisplay != null && trainingDisplay.TryGetDisplayFocusCenter(out Vector3 center))
            {
                return center;
            }

            return Vector3.zero;
        }

        private void ApplyTuningState()
        {
            if (cameraView == null)
            {
                return;
            }

            cameraView.SetCameraEnable(true);
            cameraView.SetOrbitInputRequiresAlt(false);
            cameraView.SetCameraOperatable(true);
        }

        private void RestoreTuningCameraState()
        {
            if (cameraView == null)
            {
                return;
            }

            cameraView.SetOrbitInputRequiresAlt(true);
            cameraView.SetCameraOperatable(false);
        }
    }
}
