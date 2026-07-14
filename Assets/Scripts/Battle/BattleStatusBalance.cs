using SaveData;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘用にセーブステータスを補正する
    /// </summary>
    public static class BattleStatusBalance
    {
        public const int MinHp = 400;
        public const int DefaultHp = ModelStatusDefaults.DefaultHp;
        public const int MinAttack = ModelStatusDefaults.MinAttack;
        public const int MaxAttack = ModelStatusDefaults.MaxAttack;
        public const int DefaultAttack = ModelStatusDefaults.DefaultAttack;
        public const int MinDefense = ModelStatusDefaults.MinDefense;
        public const int MaxDefense = ModelStatusDefaults.MaxDefense;
        public const int DefaultDefense = ModelStatusDefaults.DefaultDefense;
        public const int DefaultSpeed = ModelStatusDefaults.DefaultSpeed;
        public const int MinSpeed = 6;
        public const int MaxSpeed = 18;
        public const float MoveSpeedPerPoint = 0.04f;
        public const float MinMoveSpeed = 0.35f;
        public const float MinStepDistanceRatio = 0.45f;

        /// <summary>
        /// セーブデータのステータスを戦闘向けに補正する
        /// </summary>
        public static void Normalize(ModelStatus status, out int hp, out int attack, out int defense, out int speed)
        {
            ModelStatus source = status ?? new ModelStatus();

            int rawHp = source.hp > 0 ? source.hp : DefaultHp;
            hp = Mathf.Max(MinHp, rawHp);

            int rawAttack = source.attack > 0 ? source.attack : DefaultAttack;
            attack = Mathf.Clamp(rawAttack, MinAttack, MaxAttack);

            int rawDefense = source.defense > 0 ? source.defense : DefaultDefense;
            defense = Mathf.Clamp(rawDefense, MinDefense, MaxDefense);

            int rawSpeed = source.speed > 0 ? source.speed : DefaultSpeed;
            speed = Mathf.Clamp(rawSpeed, MinSpeed, MaxSpeed);
        }

        /// <summary>
        /// 速度ステータスから通常移動速度(単位/秒)を返す
        /// </summary>
        /// <param name="speed">速度ステータス</param>
        /// <param name="speedFactor">部位欠損などの速度倍率</param>
        public static float ComputeMoveSpeed(int speed, float speedFactor)
        {
            float baseSpeed = Mathf.Max(MinMoveSpeed, speed * MoveSpeedPerPoint);
            return baseSpeed * Mathf.Max(1f, speedFactor);
        }

        /// <summary>
        /// 速度ステータスからステップ移動距離を返す
        /// </summary>
        /// <param name="speed">速度ステータス</param>
        /// <param name="speedFactor">部位欠損などの速度倍率</param>
        /// <param name="referenceStepDistance">既定速度時のステップ距離</param>
        public static float ComputeStepDistance(int speed, float speedFactor, float referenceStepDistance)
        {
            float ratio = speed / (float)DefaultSpeed;
            float distance = referenceStepDistance * ratio * Mathf.Max(1f, speedFactor);
            float minimum = referenceStepDistance * MinStepDistanceRatio;
            return Mathf.Max(minimum, distance);
        }
    }
}
