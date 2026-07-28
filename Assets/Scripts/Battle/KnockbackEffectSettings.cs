using UnityEngine;

namespace Battle
{
    /// <summary>
    /// ふきとばし演出で使うprefab参照
    /// </summary>
    [CreateAssetMenu(fileName = "KnockbackEffectSettings", menuName = "ClayMonsters/Battle/KnockbackEffectSettings")]
    public sealed class KnockbackEffectSettings : ScriptableObject
    {
        private const string ResourcesPath = "Battle/KnockbackEffectSettings";
        private const float DefaultWorldScale = 7f;
        private const float DefaultHeightFallback = 0.85f;
        private const float DefaultSimulationSpeed = 1.65f;
        private const float DefaultDistortion = 0.32f;
        private const float DefaultDistortionDepthFade = 0.08f;
        private const float DefaultStartSize = 2.4f;
        private const float DefaultLifetimeSeconds = 0.55f;
        private const float DefaultPushForwardOffset = 0.35f;
        private const int DefaultRingCount = 2;

        [SerializeField] private GameObject shockwavePrefab;
        [SerializeField] private float worldScale = DefaultWorldScale;
        [SerializeField] private float heightFallback = DefaultHeightFallback;
        [SerializeField] private float simulationSpeed = DefaultSimulationSpeed;
        [SerializeField] private float distortion = DefaultDistortion;
        [SerializeField] private float distortionDepthFade = DefaultDistortionDepthFade;
        [SerializeField] private float startSize = DefaultStartSize;
        [SerializeField] private float lifetimeSeconds = DefaultLifetimeSeconds;
        [SerializeField] private float pushForwardOffset = DefaultPushForwardOffset;
        [SerializeField] private int ringCount = DefaultRingCount;

        /// <summary>
        /// Resourcesから設定を読み込む
        /// </summary>
        public static KnockbackEffectSettings Load()
        {
            return Resources.Load<KnockbackEffectSettings>(ResourcesPath);
        }

        /// <summary>
        /// ふきとばし衝撃波prefab
        /// </summary>
        public GameObject ShockwavePrefab => shockwavePrefab;

        /// <summary>
        /// ワールド空間での衝撃波スケール
        /// </summary>
        public float WorldScale => worldScale > 0f ? worldScale : DefaultWorldScale;

        /// <summary>
        /// モデル中心が取れない場合の高さ
        /// </summary>
        public float HeightFallback => heightFallback;

        /// <summary>
        /// 粒子シミュレーション速度倍率
        /// </summary>
        public float SimulationSpeed => simulationSpeed > 0f ? simulationSpeed : DefaultSimulationSpeed;

        /// <summary>
        /// 歪み強度
        /// </summary>
        public float Distortion => distortion > 0f ? distortion : DefaultDistortion;

        /// <summary>
        /// 深度フェード距離
        /// </summary>
        public float DistortionDepthFade => distortionDepthFade > 0f ? distortionDepthFade : DefaultDistortionDepthFade;

        /// <summary>
        /// 粒子の開始サイズ
        /// </summary>
        public float StartSize => startSize > 0f ? startSize : DefaultStartSize;

        /// <summary>
        /// 粒子寿命秒
        /// </summary>
        public float LifetimeSeconds => lifetimeSeconds > 0f ? lifetimeSeconds : DefaultLifetimeSeconds;

        /// <summary>
        /// 押し出し方向へのオフセット
        /// </summary>
        public float PushForwardOffset => Mathf.Max(0f, pushForwardOffset);

        /// <summary>
        /// 重ねる衝撃波の枚数
        /// </summary>
        public int RingCount => Mathf.Clamp(ringCount, 1, 3);

        /// <summary>
        /// 必須参照を検証する
        /// </summary>
        public bool Validate(out string error)
        {
            if (shockwavePrefab == null)
            {
                error = "shockwavePrefabが未配線です";
                return false;
            }

            error = null;
            return true;
        }
    }
}
