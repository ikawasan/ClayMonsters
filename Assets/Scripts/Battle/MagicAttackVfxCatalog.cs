using ClayEditor.Rigging;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 魔法攻撃の詠唱円と技別エフェクトprefab参照
    /// </summary>
    [CreateAssetMenu(fileName = "MagicAttackVfxCatalog", menuName = "ClayMonsters/MagicAttackVfxCatalog")]
    public sealed class MagicAttackVfxCatalog : ScriptableObject
    {
        private const string ResourcesPath = "Battle/MagicAttackVfxCatalog";

        [Header("共通")]
        [SerializeField] private GameObject castCirclePrefab;

        [Header("技別")]
        [SerializeField] private GameObject fireballPrefab;
        [SerializeField] private GameObject fireballExplosionPrefab;
        [SerializeField] private GameObject windSlasherPrefab;
        [SerializeField] private GameObject diamondDustPrefab;
        [SerializeField] private GameObject thunderShockPrefab;

        /// <summary>
        /// Resourcesからカタログを読み込む
        /// </summary>
        public static MagicAttackVfxCatalog Load()
        {
            return Resources.Load<MagicAttackVfxCatalog>(ResourcesPath);
        }

        /// <summary>
        /// 詠唱円prefab
        /// </summary>
        public GameObject CastCirclePrefab => castCirclePrefab;

        /// <summary>
        /// ファイアーボール着弾爆発prefab
        /// </summary>
        public GameObject FireballExplosionPrefab => fireballExplosionPrefab;

        /// <summary>
        /// 技別エフェクトprefabを返す
        /// </summary>
        /// <param name="motion">攻撃</param>
        public GameObject ResolveAttackPrefab(MotionType motion)
        {
            return motion switch
            {
                MotionType.Fireball => fireballPrefab,
                MotionType.WindSlasher => windSlasherPrefab,
                MotionType.DiamondDust => diamondDustPrefab,
                MotionType.ThunderShock => thunderShockPrefab,
                _ => null
            };
        }

        /// <summary>
        /// 必須参照を検証する
        /// </summary>
        public bool Validate(out string error)
        {
            if (castCirclePrefab == null)
            {
                error = "castCirclePrefabが未配線です";
                return false;
            }

            if (fireballPrefab == null
                || fireballExplosionPrefab == null
                || windSlasherPrefab == null
                || diamondDustPrefab == null
                || thunderShockPrefab == null)
            {
                error = "技別エフェクトprefabが未配線です";
                return false;
            }

            error = null;
            return true;
        }
    }
}
