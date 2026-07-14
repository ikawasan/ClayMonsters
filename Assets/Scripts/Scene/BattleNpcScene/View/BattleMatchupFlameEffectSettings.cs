using UnityEngine;
using UnityEngine.Serialization;

namespace Scene.BattleNpcScene.View
{
    /// <summary>
    /// 対戦紹介炎背景で使うVefectsプレハブと配置色の設定
    /// </summary>
    [CreateAssetMenu(fileName = "MatchupFlameEffectSettings", menuName = "ClayMonsters/Battle/MatchupFlameEffectSettings")]
    public sealed class BattleMatchupFlameEffectSettings : ScriptableObject
    {
        private const string DefaultResourcePath = "Battle/MatchupFlameEffectSettings";

        [Header("Prefabs")]
        [FormerlySerializedAs("sideFlamePrefab")]
        [SerializeField] private GameObject playerFlamePrefab;
        [SerializeField] private GameObject enemyFlamePrefab;
        [SerializeField] private GameObject clashFlamePrefab;

        [Header("Layout")]
        [SerializeField] private float sideFlameScale = 1.4f;
        [SerializeField] private float clashFlameScale = 2f;
        [SerializeField] private Vector3 sideFlameLocalScale = new Vector3(2.2f, 2.2f, 2.2f);
        [SerializeField] private float sideInwardTilt = 8f;
        [SerializeField] private Vector3 sideFlameLocalOffset = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 clashFlameLocalOffset = new Vector3(0f, 0f, 0f);
        [SerializeField] private bool spawnClashFlame;
        [SerializeField] private float flameOutwardOffset = 2f;

        [Header("Intensity")]
        [SerializeField] private float flameIntensityMultiplier = 2f;
        [SerializeField] private float particleEmissionMultiplier = 1.85f;
        [SerializeField] private float particleSizeMultiplier = 1.35f;
        [SerializeField] private float simulationSpeedMultiplier = 1.25f;
        [SerializeField] private float materialOpacityBoost = 1.6f;

        [Header("Tint")]
        [FormerlySerializedAs("leftFlameTint")]
        [SerializeField] private Color playerFlameTint = new Color(1f, 0.18f, 0.02f, 1f);
        [FormerlySerializedAs("rightFlameTint")]
        [SerializeField] private Color enemyFlameTint = new Color(0.4f, 0.85f, 1f, 1f);
        [SerializeField] private Color clashFlameTint = new Color(1f, 0.82f, 0.55f, 1f);
        [FormerlySerializedAs("leftLightIntensity")]
        [SerializeField] private float playerLightIntensity = 2.2f;
        [FormerlySerializedAs("rightLightIntensity")]
        [SerializeField] private float enemyLightIntensity = 2.2f;
        [SerializeField] private float clashLightIntensity = 2.4f;

        /// <summary>
        /// 自分側炎プレハブ
        /// </summary>
        public GameObject PlayerFlamePrefab => playerFlamePrefab;

        /// <summary>
        /// 敵側炎プレハブ
        /// </summary>
        public GameObject EnemyFlamePrefab => enemyFlamePrefab != null ? enemyFlamePrefab : playerFlamePrefab;

        /// <summary>
        /// 中央衝突炎プレハブ
        /// </summary>
        public GameObject ClashFlamePrefab => clashFlamePrefab;

        /// <summary>
        /// 片側炎スケール倍率
        /// </summary>
        public float SideFlameScale => sideFlameScale;

        /// <summary>
        /// 中央炎スケール
        /// </summary>
        public float ClashFlameScale => clashFlameScale;

        /// <summary>
        /// 片側炎ローカルスケール
        /// </summary>
        public Vector3 SideFlameLocalScale => sideFlameLocalScale;

        /// <summary>
        /// 炎全体の強度倍率
        /// </summary>
        public float FlameIntensityMultiplier => flameIntensityMultiplier;

        /// <summary>
        /// パーティクル放出量倍率
        /// </summary>
        public float ParticleEmissionMultiplier => particleEmissionMultiplier;

        /// <summary>
        /// パーティクルサイズ倍率
        /// </summary>
        public float ParticleSizeMultiplier => particleSizeMultiplier;

        /// <summary>
        /// シミュレーション速度倍率
        /// </summary>
        public float SimulationSpeedMultiplier => simulationSpeedMultiplier;

        /// <summary>
        /// マテリアル不透明度ブースト
        /// </summary>
        public float MaterialOpacityBoost => materialOpacityBoost;

        /// <summary>
        /// 中央へ向ける傾き
        /// </summary>
        public float SideInwardTilt => sideInwardTilt;

        /// <summary>
        /// 片側炎ローカルオフセット
        /// </summary>
        public Vector3 SideFlameLocalOffset => sideFlameLocalOffset;

        /// <summary>
        /// 中央炎ローカルオフセット
        /// </summary>
        public Vector3 ClashFlameLocalOffset => clashFlameLocalOffset;

        /// <summary>
        /// 中央衝突炎を生成するか
        /// </summary>
        public bool SpawnClashFlame => spawnClashFlame;

        /// <summary>
        /// キャラから外側へ押し出す炎オフセット
        /// </summary>
        public float FlameOutwardOffset => flameOutwardOffset;

        /// <summary>
        /// 自分側炎ティント
        /// </summary>
        public Color PlayerFlameTint => playerFlameTint;

        /// <summary>
        /// 敵側炎ティント
        /// </summary>
        public Color EnemyFlameTint => enemyFlameTint;

        /// <summary>
        /// 中央炎ティント
        /// </summary>
        public Color ClashFlameTint => clashFlameTint;

        /// <summary>
        /// 自分側ライト強度倍率
        /// </summary>
        public float PlayerLightIntensity => playerLightIntensity;

        /// <summary>
        /// 敵側ライト強度倍率
        /// </summary>
        public float EnemyLightIntensity => enemyLightIntensity;

        /// <summary>
        /// 中央ライト強度倍率
        /// </summary>
        public float ClashLightIntensity => clashLightIntensity;

        /// <summary>
        /// 再生に必要なプレハブが揃っているか
        /// </summary>
        public bool IsValid => playerFlamePrefab != null;

        /// <summary>
        /// Resourcesから既定設定を読み込む
        /// </summary>
        public static BattleMatchupFlameEffectSettings LoadDefault()
        {
            return Resources.Load<BattleMatchupFlameEffectSettings>(DefaultResourcePath);
        }
    }
}
