using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// 対戦紹介中は教室を隠して暗い虚空とチーム色の環境光を表示し
    /// キャラを主役に見せたうえで本番開始時に教室へ戻す
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleMatchupBackgroundView : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private GameObject classroomRoot;
        [SerializeField] private UnityEngine.Camera targetCamera;

        [Header("演出")]
        [SerializeField] private BattleMatchupFlameEffectSettings flameEffectSettings;
        [SerializeField] private bool spawnFlameEffects = true;
        [SerializeField] private bool spawnSideLights = true;
        [SerializeField] private bool spawnCharacterLights = true;
        [SerializeField] private float flameHorizontalOffset = 7f;
        [SerializeField] private float flameDepth = 8f;
        [SerializeField] private float flameVerticalOffset = -2.5f;
        [SerializeField] private float voidBackdropDepth = 16f;

        private GameObject flameRoot;
        private BattleMatchupFlameEffectBuilder.SideFlameEffect playerFlameEffect;
        private BattleMatchupFlameEffectBuilder.SideFlameEffect enemyFlameEffect;
        private BattleMatchupFlameEffectBuilder.ClashFlameEffect clashFlameEffect;
        private Light leftAccentLight;
        private Light rightAccentLight;
        private Light keyDirectionalLight;
        private Light fillDirectionalLight;
        private Light playerSpotLight;
        private Light enemySpotLight;
        private Light playerRimLight;
        private Light enemyRimLight;
        private Vector3 backdropCenter;
        private Vector3 playerWorldPosition;
        private Vector3 enemyWorldPosition;
        private bool hasCharacterPositions;
        private bool isFlameActive;
        private bool hasStoredCameraState;
        private CameraClearFlags storedClearFlags;
        private Color storedBackgroundColor;

        /// <summary>
        /// 背景追従に使うカメラを設定する
        /// </summary>
        /// <param name="camera">追従対象カメラ</param>
        public void SetTargetCamera(UnityEngine.Camera camera)
        {
            targetCamera = camera;
        }

        /// <summary>
        /// 対戦キャラのワールド座標を設定しスポットライトを追従させる
        /// </summary>
        /// <param name="playerPosition">プレイヤー位置</param>
        /// <param name="enemyPosition">敵位置</param>
        public void SetCharacterWorldPositions(Vector3 playerPosition, Vector3 enemyPosition)
        {
            playerWorldPosition = playerPosition;
            enemyWorldPosition = enemyPosition;
            hasCharacterPositions = true;
            UpdateCharacterLights();
            UpdateFlamePositions();
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = UnityEngine.Camera.main;
            }

            ValidateClassroomRoot();
        }

        private void LateUpdate()
        {
            if (!isFlameActive || flameRoot == null || !flameRoot.activeSelf)
            {
                return;
            }

            UpdateBackdropFacing();
            UpdateFlamePositions();
            PulseAccentLights();
            UpdateCharacterLights();
        }

        /// <summary>
        /// 教室背景を非表示にする
        /// </summary>
        public void HideClassroom()
        {
            BattleClassroomFieldLayout.SetFieldVisible(classroomRoot, false);
        }

        /// <summary>
        /// 対戦紹介用の炎背景を表示する
        /// </summary>
        /// <param name="center">背景の中心座標</param>
        public void ShowFlame(Vector3 center)
        {
            backdropCenter = center;
            EnsureFlameRoot(center);

            HideClassroom();
            ApplyMatchupCameraBackground();
            isFlameActive = true;

            if (flameRoot != null)
            {
                flameRoot.SetActive(true);
                flameRoot.transform.position = center;
            }

            playerFlameEffect?.Play();
            enemyFlameEffect?.Play();
            clashFlameEffect?.Play();

            UpdateFlamePositions();
            SetLightsEnabled(true);
            UpdateCharacterLights();
        }

        /// <summary>
        /// 炎演出のみ終了するFieldは表示しない
        /// </summary>
        public void EndFlamePresentation()
        {
            isFlameActive = false;
            RestoreCameraBackground();

            if (flameRoot != null)
            {
                flameRoot.SetActive(false);
            }

            playerFlameEffect?.Stop();
            enemyFlameEffect?.Stop();
            clashFlameEffect?.Stop();
            SetLightsEnabled(false);
        }

        /// <summary>
        /// 戦闘用Field教室を表示する
        /// </summary>
        public void ShowClassroom()
        {
            EndFlamePresentation();
            if (!ValidateClassroomRoot())
            {
                return;
            }

            FieldBackgroundLayerUtility.Apply(classroomRoot);
            BattleClassroomFieldLayout.SetFieldVisible(classroomRoot, true);
        }

        private bool ValidateClassroomRoot()
        {
            if (classroomRoot == null)
            {
                Debug.LogError(
                    "[BattleMatchupBackgroundView] classroomRootが未配線です"
                    + " このシーンのFieldを割り当ててください",
                    this);
                return false;
            }

            if (classroomRoot.name != BattleClassroomFieldLayout.FieldObjectName)
            {
                Debug.LogError(
                    "[BattleMatchupBackgroundView] classroomRootにはFieldを割り当ててください"
                    + $" 現在:{classroomRoot.name}",
                    this);
                return false;
            }

            if (classroomRoot.scene != gameObject.scene)
            {
                Debug.LogError(
                    "[BattleMatchupBackgroundView] classroomRootは同一シーンのFieldである必要があります"
                    + $" fieldScene={classroomRoot.scene.name} viewScene={gameObject.scene.name}",
                    this);
                return false;
            }

            return true;
        }

        private void OnDestroy()
        {
            if (flameRoot != null)
            {
                Destroy(flameRoot);
            }
        }

        private void SetLightsEnabled(bool isEnabled)
        {
            if (leftAccentLight != null)
            {
                leftAccentLight.enabled = isEnabled;
            }

            if (rightAccentLight != null)
            {
                rightAccentLight.enabled = isEnabled;
            }

            if (keyDirectionalLight != null)
            {
                keyDirectionalLight.enabled = isEnabled;
            }

            if (fillDirectionalLight != null)
            {
                fillDirectionalLight.enabled = isEnabled;
            }

            if (playerSpotLight != null)
            {
                playerSpotLight.enabled = isEnabled && hasCharacterPositions;
            }

            if (enemySpotLight != null)
            {
                enemySpotLight.enabled = isEnabled && hasCharacterPositions;
            }

            if (playerRimLight != null)
            {
                playerRimLight.enabled = isEnabled && hasCharacterPositions;
            }

            if (enemyRimLight != null)
            {
                enemyRimLight.enabled = isEnabled && hasCharacterPositions;
            }
        }

        private void ApplyMatchupCameraBackground()
        {
            UnityEngine.Camera camera = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
            if (camera == null)
            {
                return;
            }

            if (!hasStoredCameraState)
            {
                storedClearFlags = camera.clearFlags;
                storedBackgroundColor = camera.backgroundColor;
                hasStoredCameraState = true;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.018f, 0.038f, 1f);
        }

        private void RestoreCameraBackground()
        {
            if (!hasStoredCameraState)
            {
                return;
            }

            UnityEngine.Camera camera = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
            if (camera != null)
            {
                camera.clearFlags = storedClearFlags;
                camera.backgroundColor = storedBackgroundColor;
            }

            hasStoredCameraState = false;
        }

        private void UpdateBackdropFacing()
        {
            UnityEngine.Camera camera = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
            if (camera == null || flameRoot == null)
            {
                return;
            }

            flameRoot.transform.position = backdropCenter;
            Vector3 toCamera = camera.transform.position - backdropCenter;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.01f)
            {
                flameRoot.transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
            }
        }

        private void UpdateCharacterLights()
        {
            if (!hasCharacterPositions || !isFlameActive)
            {
                return;
            }

            UnityEngine.Camera camera = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
            Vector3 cameraForward = camera != null
                ? camera.transform.forward
                : Vector3.forward;
            cameraForward.y = 0f;
            if (cameraForward.sqrMagnitude < 0.01f)
            {
                cameraForward = Vector3.forward;
            }

            cameraForward.Normalize();
            Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);

            PlaceCharacterSpotLight(playerSpotLight, playerWorldPosition, cameraForward, new Color(0.72f, 0.88f, 1f), 3.6f);
            PlaceCharacterSpotLight(enemySpotLight, enemyWorldPosition, cameraForward, new Color(1f, 0.72f, 0.38f), 3.4f);
            PlaceCharacterRimLight(playerRimLight, playerWorldPosition, -cameraRight, new Color(0.35f, 0.62f, 1f), 2.2f);
            PlaceCharacterRimLight(enemyRimLight, enemyWorldPosition, cameraRight, new Color(1f, 0.42f, 0.12f), 2f);
        }

        private static void PlaceCharacterSpotLight(
            Light light,
            Vector3 characterPosition,
            Vector3 cameraForward,
            Color color,
            float intensity)
        {
            if (light == null)
            {
                return;
            }

            Vector3 offset = (-cameraForward * 1.8f) + (Vector3.up * 3.4f);
            light.transform.position = characterPosition + offset;
            light.transform.LookAt(characterPosition + Vector3.up * 0.85f);
            light.color = color;
            light.intensity = intensity;
        }

        private static void PlaceCharacterRimLight(
            Light light,
            Vector3 characterPosition,
            Vector3 sideOffset,
            Color color,
            float intensity)
        {
            if (light == null)
            {
                return;
            }

            light.transform.position = characterPosition + sideOffset.normalized * 2.4f + Vector3.up * 1.6f;
            light.transform.LookAt(characterPosition + Vector3.up * 0.9f);
            light.color = color;
            light.intensity = intensity;
        }

        private void PulseAccentLights()
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.1f);

            if (leftAccentLight != null)
            {
                leftAccentLight.intensity = Mathf.Lerp(3.2f, 4.8f, wave);
            }

            if (rightAccentLight != null)
            {
                rightAccentLight.intensity = Mathf.Lerp(3f, 4.6f, 1f - wave);
            }
        }

        private void UpdateFlamePositions()
        {
            if (!spawnFlameEffects || flameRoot == null)
            {
                return;
            }

            BattleMatchupFlameEffectSettings settings = ResolveFlameEffectSettings();
            float inwardTilt = settings != null ? settings.SideInwardTilt : 16f;
            float outwardOffset = settings != null ? settings.FlameOutwardOffset : 1.1f;

            if (hasCharacterPositions)
            {
                Vector3 playerLocal = flameRoot.transform.InverseTransformPoint(playerWorldPosition);
                Vector3 enemyLocal = flameRoot.transform.InverseTransformPoint(enemyWorldPosition);
                float playerTilt = Mathf.Sign(enemyLocal.x - playerLocal.x) * inwardTilt;
                float enemyTilt = Mathf.Sign(playerLocal.x - enemyLocal.x) * inwardTilt;
                float playerX = playerLocal.x + outwardOffset * Mathf.Sign(playerLocal.x - enemyLocal.x);
                float enemyX = enemyLocal.x + outwardOffset * Mathf.Sign(enemyLocal.x - playerLocal.x);

                PlaceSideFlame(playerFlameEffect, playerX, playerTilt);
                PlaceSideFlame(enemyFlameEffect, enemyX, enemyTilt);
            }
            else
            {
                PlaceSideFlame(playerFlameEffect, -flameHorizontalOffset, inwardTilt);
                PlaceSideFlame(enemyFlameEffect, flameHorizontalOffset, -inwardTilt);
            }

            if (clashFlameEffect?.Root != null && settings != null && settings.SpawnClashFlame)
            {
                clashFlameEffect.Root.localPosition = new Vector3(0f, flameVerticalOffset + 0.15f, flameDepth + 0.6f);
            }
        }

        private void PlaceSideFlame(
            BattleMatchupFlameEffectBuilder.SideFlameEffect effect,
            float localX,
            float inwardTilt)
        {
            if (effect?.Root == null)
            {
                return;
            }

            effect.Root.localPosition = new Vector3(localX, flameVerticalOffset, flameDepth);
            effect.Root.localRotation = Quaternion.Euler(0f, inwardTilt, 0f);
        }

        private BattleMatchupFlameEffectSettings ResolveFlameEffectSettings()
        {
            if (flameEffectSettings != null)
            {
                return flameEffectSettings;
            }

            return BattleMatchupFlameEffectSettings.LoadDefault();
        }

        private void EnsureFlameRoot(Vector3 center)
        {
            if (flameRoot != null)
            {
                return;
            }

            flameRoot = new GameObject("MatchupFlameBackground");
            flameRoot.transform.SetParent(transform, false);
            flameRoot.transform.position = center;

            CreateVoidBackdrop(flameRoot.transform);
            CreateMatchupLights(flameRoot.transform);

            if (spawnCharacterLights)
            {
                CreateCharacterLights(flameRoot.transform);
            }

            if (spawnFlameEffects)
            {
                BattleMatchupFlameEffectSettings settings = ResolveFlameEffectSettings();
                if (settings == null || !settings.IsValid)
                {
                    Debug.LogWarning("[BattleMatchupBackgroundView] Matchup flame settings or prefabs are missing.");
                }
                else
                {
                    playerFlameEffect = BattleMatchupFlameEffectBuilder.BuildPlayerFlame(
                        settings,
                        flameRoot.transform,
                        new Vector3(-flameHorizontalOffset, flameVerticalOffset, flameDepth),
                        settings.SideInwardTilt);
                    enemyFlameEffect = BattleMatchupFlameEffectBuilder.BuildEnemyFlame(
                        settings,
                        flameRoot.transform,
                        new Vector3(flameHorizontalOffset, flameVerticalOffset, flameDepth),
                        -settings.SideInwardTilt);
                    if (settings.SpawnClashFlame)
                    {
                        clashFlameEffect = BattleMatchupFlameEffectBuilder.BuildClashFlame(
                            settings,
                            flameRoot.transform,
                            new Vector3(0f, flameVerticalOffset + 0.15f, flameDepth + 0.6f));
                    }
                }
            }

            flameRoot.SetActive(false);
        }

        private void CreateVoidBackdrop(Transform parent)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "MatchupVoidBackdrop";
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 1.8f, voidBackdropDepth);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(42f, 28f, 1f);

            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                var material = new Material(shader);
                material.SetColor("_BaseColor", new Color(0.008f, 0.008f, 0.02f, 1f));
                renderer.sharedMaterial = material;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void CreateMatchupLights(Transform parent)
        {
            if (!spawnSideLights)
            {
                return;
            }

            leftAccentLight = CreateAccentLight(
                parent,
                "MatchupLightLeft",
                new Vector3(-4.5f, 3.2f, 4.5f),
                new Color(0.25f, 0.55f, 1f),
                3.6f,
                28f);
            rightAccentLight = CreateAccentLight(
                parent,
                "MatchupLightRight",
                new Vector3(4.5f, 3.2f, 4.5f),
                new Color(1f, 0.35f, 0.12f),
                3.4f,
                28f);

            keyDirectionalLight = CreateDirectionalLight(
                parent,
                "MatchupKeyLight",
                new Vector3(42f, 155f, 0f),
                new Color(1f, 0.97f, 0.92f),
                1.35f);
            fillDirectionalLight = CreateDirectionalLight(
                parent,
                "MatchupFillLight",
                new Vector3(18f, -35f, 0f),
                new Color(0.82f, 0.88f, 1f),
                0.55f);
        }

        private void CreateCharacterLights(Transform parent)
        {
            playerSpotLight = CreateSpotLight(parent, "MatchupPlayerSpot", new Color(0.72f, 0.88f, 1f), 3.6f, 38f, 42f);
            enemySpotLight = CreateSpotLight(parent, "MatchupEnemySpot", new Color(1f, 0.72f, 0.38f), 3.4f, 38f, 42f);
            playerRimLight = CreateSpotLight(parent, "MatchupPlayerRim", new Color(0.35f, 0.62f, 1f), 2.2f, 18f, 55f);
            enemyRimLight = CreateSpotLight(parent, "MatchupEnemyRim", new Color(1f, 0.42f, 0.12f), 2f, 18f, 55f);
        }

        private static Light CreateAccentLight(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Color color,
            float intensity,
            float range)
        {
            GameObject host = new GameObject(objectName);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = localPosition;

            Light light = host.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.enabled = false;
            host.AddComponent<UniversalAdditionalLightData>();
            return light;
        }

        private static Light CreateDirectionalLight(
            Transform parent,
            string objectName,
            Vector3 eulerAngles,
            Color color,
            float intensity)
        {
            GameObject host = new GameObject(objectName);
            host.transform.SetParent(parent, false);
            host.transform.localRotation = Quaternion.Euler(eulerAngles);

            Light light = host.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.enabled = false;
            host.AddComponent<UniversalAdditionalLightData>();
            return light;
        }

        private static Light CreateSpotLight(
            Transform parent,
            string objectName,
            Color color,
            float intensity,
            float range,
            float spotAngle)
        {
            GameObject host = new GameObject(objectName);
            host.transform.SetParent(parent, false);

            Light light = host.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * 0.72f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            host.AddComponent<UniversalAdditionalLightData>();
            return light;
        }
    }
}
