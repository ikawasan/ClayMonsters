using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;

namespace Scene.BattleNpcScene
{
    /// <summary>
    /// BattleNpc教室向けの3点ライティング
    /// Directional・窓からのFill・モデル照らすPointでTitle教室と同系統の見た目を再現する
    /// </summary>
    [MovedFrom("Scene.ModelGalleryScene.ModelGalleryBattleNpcLighting")]
    public sealed class BattleClassroomLighting : MonoBehaviour
    {
        private const string LightingRootName = "BattleNpcLighting";

        [Header("スポーン")]
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform enemySpawnPoint;
        [SerializeField] private Transform fieldCenterOverride;

        [Header("Directional Light")]
        [SerializeField] private Vector3 keyLightEuler = new Vector3(50f, 160f, 0f);
        [SerializeField] private Color keyLightColor = new Color(1f, 0.98f, 0.94f, 1f);
        [SerializeField] private float keyLightIntensity = 1.15f;

        [Header("Window Fill Light")]
        [SerializeField] private Vector3 windowFillEuler = new Vector3(22f, -60f, 0f);
        [SerializeField] private Color windowFillColor = new Color(0.86f, 0.92f, 1f, 1f);
        [SerializeField] private float windowFillIntensity = 0.55f;

        [Header("Model Fill Light")]
        [SerializeField] private float modelFillHeightOffset = 3.09f;
        [SerializeField] private Color modelFillColor = new Color(1f, 0.97f, 0.9f, 1f);
        [SerializeField] private float modelFillIntensity = 2.2f;
        [SerializeField] private float modelFillRange = 22f;

        private Transform lightingRoot;

        private void Awake()
        {
            EnsureSpawnReferences();
        }

        /// <summary>
        /// BattleNpc教室と同じライティング構成を有効化する
        /// </summary>
        public void Apply()
        {
            EnsureSpawnReferences();
            EnsureLightingRoot();
            lightingRoot.gameObject.SetActive(true);
            UpdateModelFillPosition();
        }

        /// <summary>
        /// ライティングを無効化する
        /// </summary>
        public void DisableLighting()
        {
            if (lightingRoot != null)
            {
                lightingRoot.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (lightingRoot == null || !lightingRoot.gameObject.activeSelf)
            {
                return;
            }

            UpdateModelFillPosition();
        }

        private void EnsureSpawnReferences()
        {
            if (playerSpawnPoint == null)
            {
                GameObject found = GameObject.Find("PlayerSpawnPoint");
                if (found != null)
                {
                    playerSpawnPoint = found.transform;
                }
            }

            if (enemySpawnPoint == null)
            {
                GameObject found = GameObject.Find("EnemySpawnPoint");
                if (found != null)
                {
                    enemySpawnPoint = found.transform;
                }
            }
        }

        private void EnsureLightingRoot()
        {
            if (lightingRoot != null)
            {
                return;
            }

            Transform parent = transform;
            Transform existing = parent.Find(LightingRootName);
            if (existing != null)
            {
                lightingRoot = existing;
                return;
            }

            var rootObject = new GameObject(LightingRootName);
            lightingRoot = rootObject.transform;
            lightingRoot.SetParent(parent, false);

            CreateDirectionalLight(
                lightingRoot,
                "Directional Light",
                keyLightEuler,
                keyLightColor,
                keyLightIntensity);
            CreateDirectionalLight(
                lightingRoot,
                "Window Fill Light",
                windowFillEuler,
                windowFillColor,
                windowFillIntensity);
            CreatePointLight(
                lightingRoot,
                "Model Fill Light",
                ResolveFieldCenter() + Vector3.up * modelFillHeightOffset,
                modelFillColor,
                modelFillIntensity,
                modelFillRange);
        }

        private void UpdateModelFillPosition()
        {
            if (lightingRoot == null)
            {
                return;
            }

            Transform modelFill = lightingRoot.Find("Model Fill Light");
            if (modelFill != null)
            {
                modelFill.position = ResolveFieldCenter() + Vector3.up * modelFillHeightOffset;
            }
        }

        private Vector3 ResolveFieldCenter()
        {
            if (fieldCenterOverride != null)
            {
                return fieldCenterOverride.position;
            }

            Vector3 player = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
            Vector3 enemy = enemySpawnPoint != null ? enemySpawnPoint.position : Vector3.zero;
            return (player + enemy) * 0.5f;
        }

        private static void CreateDirectionalLight(
            Transform parent,
            string objectName,
            Vector3 eulerAngles,
            Color color,
            float intensity)
        {
            var lightObject = new GameObject(objectName);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localRotation = Quaternion.Euler(eulerAngles);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;

            lightObject.AddComponent<UniversalAdditionalLightData>();
        }

        private static void CreatePointLight(
            Transform parent,
            string objectName,
            Vector3 worldPosition,
            Color color,
            float intensity,
            float range)
        {
            var lightObject = new GameObject(objectName);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = worldPosition;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;

            lightObject.AddComponent<UniversalAdditionalLightData>();
        }
    }
}
