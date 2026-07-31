using SaveData;
using System.Collections.Generic;
using UnityEngine;

namespace UI.SkillTree.View
{
    /// <summary>
    /// スキルツリーアイコンのResources読み込み
    /// </summary>
    public static class SkillTreeIconCatalog
    {
        private const string ResourceRoot = "UI/SkillTree/Icons/";
        private static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>
        /// ノードに対応するアイコンを返す
        /// </summary>
        /// <param name="nodeId">ノードID</param>
        public static Sprite Resolve(SkillTreeNodeId nodeId)
        {
            if (!SkillTreeCatalog.TryGet(nodeId, out SkillTreeNodeDefinition definition))
            {
                return null;
            }

            return ResolveEffect(definition.EffectType);
        }

        /// <summary>
        /// 効果種別に対応するアイコンを返す
        /// </summary>
        /// <param name="effectType">効果種別</param>
        public static Sprite ResolveEffect(SkillTreeEffectType effectType)
        {
            switch (effectType)
            {
                case SkillTreeEffectType.StartingHp:
                    return Load("Icon_StartingHp");
                case SkillTreeEffectType.StartingAttack:
                    return Load("Icon_StartingAttack");
                case SkillTreeEffectType.StartingDefense:
                    return Load("Icon_StartingDefense");
                case SkillTreeEffectType.StartingSpeed:
                    return Load("Icon_StartingSpeed");
                case SkillTreeEffectType.StartingHit:
                    return Load("Icon_StartingHit");
                case SkillTreeEffectType.StartingAll:
                    return Load("Icon_StartingAll");
                case SkillTreeEffectType.GreatSuccessPercent:
                    return Load("Icon_GreatSuccess");
                case SkillTreeEffectType.StartingMoney:
                    return Load("Icon_StartingMoney");
                case SkillTreeEffectType.TrainingMoneyPercent:
                    return Load("Icon_TrainingMoney");
                case SkillTreeEffectType.PointsGainPercent:
                    return Load("Icon_PointsGain");
                case SkillTreeEffectType.InheritancePercent:
                    return Load("Icon_Inheritance");
                default:
                    return Load("Icon_StartingHp");
            }
        }

        private static Sprite Load(string fileName)
        {
            if (Cache.TryGetValue(fileName, out Sprite cached) && cached != null)
            {
                return cached;
            }

            string path = ResourceRoot + fileName;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Sprite[] sprites = Resources.LoadAll<Sprite>(path);
                if (sprites != null && sprites.Length > 0)
                {
                    sprite = sprites[0];
                }
            }

            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            if (sprite == null)
            {
                Debug.LogError($"[SkillTreeIconCatalog] アイコン読み込み失敗 path={path}");
                return null;
            }

            Cache[fileName] = sprite;
            return sprite;
        }
    }
}
