using System.Text.Json.Nodes;

internal static class StatusCatalog
{
    public const int BalanceVersion = 15;

    private const float WeakProgress = 1f;
    private const float NormalProgress = 1.5f;
    private const float StrongProgress = 2f;
    private const float VeryStrongProgress = 2.5f;
    private const float StrongestProgress = 4.1f;

    private const int SoftTargetHp = 280;
    private const int SoftTargetAttack = 250;
    private const int SoftTargetDefense = 250;
    private const int SoftTargetSpeed = 160;
    private const int SoftTargetHit = 160;

    private const int MaxHp = 999;
    private const int MaxAttack = 999;
    private const int MaxDefense = 999;
    private const int MaxSpeed = 999;
    private const int MaxHit = 999;

    private const int ModelStatusCalculatorMinSpeed = ModelStatusDefaults.MinSpeed;
    private const int ModelStatusCalculatorMaxSpeed = ModelStatusDefaults.MaxSpeed;

    private const float PrimaryWeight = 1.38f;
    private const float SecondaryWeight = 1.22f;
    private const float MinorWeight = 0.78f;
    private const float FlatShapeThreshold = 0.10f;
    private const float DualSpecialtyGap = 0.14f;

    private static readonly StatWeights[] SeededArchetypes =
    {
        new(1.00f, 1.00f, 1.00f, 1.00f, 1.00f),
        new(1.40f, 0.72f, 1.35f, 0.70f, 0.83f),
        new(1.12f, 1.40f, 0.82f, 0.80f, 1.05f),
        new(0.70f, 1.45f, 0.68f, 1.10f, 1.18f),
        new(0.78f, 1.05f, 0.78f, 1.45f, 1.28f),
        new(1.28f, 0.70f, 1.45f, 0.68f, 0.90f),
        new(0.88f, 1.18f, 0.88f, 1.12f, 1.40f),
        new(1.22f, 1.28f, 1.15f, 0.72f, 0.78f),
        new(0.85f, 1.32f, 0.75f, 1.25f, 1.15f),
        new(1.35f, 0.95f, 1.20f, 0.75f, 0.82f),
    };

    public static void ApplyToSlot(JsonObject slot)
    {
        SlotData data = ReadSlot(slot);
        ModelStatus untrainedBase = ResolveUntrainedBase(data);
        ApplyGeneratedFromBase(data, NormalizeUntrainedBase(untrainedBase));
        WriteSlot(slot, data);
    }

    private static SlotData ReadSlot(JsonObject slot)
    {
        return new SlotData
        {
            ModelName = slot["modelName"]?.GetValue<string>(),
            GlbFileName = slot["glbFileName"]?.GetValue<string>(),
            Status = ReadStatus(slot["status"]),
            StatusWeak = ReadStatus(slot["statusWeak"]),
            StatusNormal = ReadStatus(slot["statusNormal"]),
            StatusStrong = ReadStatus(slot["statusStrong"]),
            StatusVeryStrong = ReadStatus(slot["statusVeryStrong"]),
            StatusStrongest = ReadStatus(slot["statusStrongest"]),
            HasEnemyStrengthStatuses = slot["hasEnemyStrengthStatuses"]?.GetValue<bool>() ?? false,
            EnemyStrengthBalanceVersion = slot["enemyStrengthBalanceVersion"]?.GetValue<int>() ?? 0
        };
    }

    private static void WriteSlot(JsonObject slot, SlotData data)
    {
        slot["status"] = ToJson(data.Status);
        slot["statusWeak"] = ToJson(data.StatusWeak);
        slot["statusNormal"] = ToJson(data.StatusNormal);
        slot["statusStrong"] = ToJson(data.StatusStrong);
        slot["statusVeryStrong"] = ToJson(data.StatusVeryStrong);
        slot["statusStrongest"] = ToJson(data.StatusStrongest);
        slot["hasEnemyStrengthStatuses"] = data.HasEnemyStrengthStatuses;
        slot["enemyStrengthBalanceVersion"] = data.EnemyStrengthBalanceVersion;
    }

    private static ModelStatus ReadStatus(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return new ModelStatus();
        }

        return new ModelStatus
        {
            Hp = obj["hp"]?.GetValue<int>() ?? 0,
            Attack = obj["attack"]?.GetValue<int>() ?? 0,
            Defense = obj["defense"]?.GetValue<int>() ?? 0,
            Speed = obj["speed"]?.GetValue<int>() ?? 0,
            Hit = obj["hit"]?.GetValue<int>() ?? 0
        };
    }

    private static JsonObject ToJson(ModelStatus status)
    {
        return new JsonObject
        {
            ["hp"] = status.Hp,
            ["attack"] = status.Attack,
            ["defense"] = status.Defense,
            ["speed"] = status.Speed,
            ["hit"] = status.Hit
        };
    }

    private static void ApplyGeneratedFromBase(SlotData slot, ModelStatus baseStatus)
    {
        ModelStatus source = NormalizeUntrainedBase(baseStatus);
        StatWeights weights = ResolvePersonalityWeights(slot, source);
        slot.StatusWeak = BuildTier(source, WeakProgress, weights);
        slot.StatusNormal = BuildTier(source, NormalProgress, weights);
        slot.StatusStrong = BuildTier(source, StrongProgress, weights);
        slot.StatusVeryStrong = BuildTier(source, VeryStrongProgress, weights);
        slot.StatusStrongest = BuildTier(source, StrongestProgress, weights);
        slot.Status = Clone(slot.StatusWeak);
        slot.HasEnemyStrengthStatuses = true;
        slot.EnemyStrengthBalanceVersion = BalanceVersion;
    }

    private static ModelStatus ResolveUntrainedBase(SlotData slot)
    {
        if (slot.HasEnemyStrengthStatuses
            && HasCompleteSet(slot)
            && slot.EnemyStrengthBalanceVersion < 2
            && HasAnyValue(slot.StatusNormal))
        {
            return Clone(slot.StatusNormal);
        }

        if (slot.HasEnemyStrengthStatuses && HasAnyValue(slot.StatusWeak))
        {
            return Clone(slot.StatusWeak);
        }

        return Clone(slot.Status);
    }

    private static ModelStatus NormalizeUntrainedBase(ModelStatus source)
    {
        ModelStatus status = Clone(source);
        status.Hp = NormalizeCreationStat(
            status.Hp,
            ModelStatusDefaults.MinHp,
            ModelStatusDefaults.MaxHp,
            ModelStatusDefaults.DefaultHp);
        status.Attack = NormalizeCreationStat(
            status.Attack,
            ModelStatusDefaults.MinAttack,
            ModelStatusDefaults.MaxAttack,
            ModelStatusDefaults.DefaultAttack);
        status.Defense = NormalizeCreationStat(
            status.Defense,
            ModelStatusDefaults.MinDefense,
            ModelStatusDefaults.MaxDefense,
            ModelStatusDefaults.DefaultDefense);
        status.Speed = NormalizeCreationStat(
            status.Speed,
            ModelStatusCalculatorMinSpeed,
            ModelStatusCalculatorMaxSpeed,
            ModelStatusDefaults.DefaultSpeed);
        status.Hit = NormalizeCreationStat(
            status.Hit,
            ModelStatusDefaults.MinHit,
            ModelStatusDefaults.MaxHit,
            ModelStatusDefaults.DefaultHit);
        return status;
    }

    private static int NormalizeCreationStat(int value, int min, int max, int defaultValue)
    {
        if (value <= 0)
        {
            return defaultValue;
        }

        return Math.Clamp(value, min, max);
    }

    private static StatWeights ResolvePersonalityWeights(SlotData slot, ModelStatus baseStatus)
    {
        float hp = Normalize01(baseStatus.Hp, ModelStatusDefaults.MinHp, ModelStatusDefaults.MaxHp);
        float attack = Normalize01(baseStatus.Attack, ModelStatusDefaults.MinAttack, ModelStatusDefaults.MaxAttack);
        float defense = Normalize01(baseStatus.Defense, ModelStatusDefaults.MinDefense, ModelStatusDefaults.MaxDefense);
        float speed = Normalize01(baseStatus.Speed, ModelStatusCalculatorMinSpeed, ModelStatusCalculatorMaxSpeed);
        float hit = Normalize01(baseStatus.Hit, ModelStatusDefaults.MinHit, ModelStatusDefaults.MaxHit);

        float[] scores = { hp, attack, defense, speed, hit };
        int primary = IndexOfMax(scores, exclude: -1);
        int secondary = IndexOfMax(scores, exclude: primary);
        float average = (hp + attack + defense + speed + hit) * 0.2f;
        float spread = scores[primary] - average;
        int seed = BuildPersonalitySeed(slot, baseStatus);

        if (spread < FlatShapeThreshold)
        {
            int archetypeIndex = Mod(seed, SeededArchetypes.Length);
            return SeededArchetypes[archetypeIndex].Normalized();
        }

        float[] weights = { MinorWeight, MinorWeight, MinorWeight, MinorWeight, MinorWeight };
        weights[primary] = PrimaryWeight;
        if (scores[primary] - scores[secondary] <= DualSpecialtyGap)
        {
            weights[secondary] = SecondaryWeight;
        }
        else if (Mod(seed, 3) == 0)
        {
            weights[secondary] = SecondaryWeight;
        }

        ApplySeedJitter(weights, seed);
        return new StatWeights(
            weights[0],
            weights[1],
            weights[2],
            weights[3],
            weights[4]).Normalized();
    }

    private static void ApplySeedJitter(float[] weights, int seed)
    {
        for (int i = 0; i < weights.Length; i++)
        {
            int unit = Mod(seed + (i * 37), 11) - 5;
            weights[i] *= 1f + (unit * 0.012f);
        }
    }

    private static int BuildPersonalitySeed(SlotData slot, ModelStatus baseStatus)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + StableStringHash(slot.ModelName);
            hash = (hash * 31) + StableStringHash(slot.GlbFileName);
            hash = (hash * 31) + baseStatus.Hp;
            hash = (hash * 31) + (baseStatus.Attack * 17);
            hash = (hash * 31) + (baseStatus.Defense * 23);
            hash = (hash * 31) + (baseStatus.Speed * 29);
            hash = (hash * 31) + (baseStatus.Hit * 31);
            return hash;
        }
    }

    private static int StableStringHash(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
            {
                hash = (hash * 31) + value[i];
            }

            return hash;
        }
    }

    private static float Normalize01(int value, int min, int max)
    {
        if (max <= min)
        {
            return 0f;
        }

        return Math.Clamp((value - min) / (float)(max - min), 0f, 1f);
    }

    private static int IndexOfMax(float[] scores, int exclude)
    {
        int best = exclude == 0 ? 1 : 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < scores.Length; i++)
        {
            if (i == exclude)
            {
                continue;
            }

            if (scores[i] > bestScore)
            {
                bestScore = scores[i];
                best = i;
            }
        }

        return best;
    }

    private static int Mod(int value, int modulo)
    {
        if (modulo <= 0)
        {
            return 0;
        }

        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static ModelStatus BuildTier(ModelStatus untrainedBase, float progress, StatWeights weights)
    {
        return Clamp(new ModelStatus
        {
            Hp = LerpTowardTarget(
                untrainedBase.Hp,
                ScaleSoftTarget(SoftTargetHp, weights.Hp),
                progress,
                MaxHp),
            Attack = LerpTowardTarget(
                untrainedBase.Attack,
                ScaleSoftTarget(SoftTargetAttack, weights.Attack),
                progress,
                MaxAttack),
            Defense = LerpTowardTarget(
                untrainedBase.Defense,
                ScaleSoftTarget(SoftTargetDefense, weights.Defense),
                progress,
                MaxDefense),
            Speed = LerpTowardTarget(
                untrainedBase.Speed,
                ScaleSoftTarget(SoftTargetSpeed, weights.Speed),
                progress,
                MaxSpeed),
            Hit = LerpTowardTarget(
                untrainedBase.Hit,
                ScaleSoftTarget(SoftTargetHit, weights.Hit),
                progress,
                MaxHit)
        });
    }

    private static int ScaleSoftTarget(int softTarget, float weight)
    {
        return Math.Max(1, (int)Math.Round(softTarget * Math.Max(0.1f, weight), MidpointRounding.AwayFromZero));
    }

    private static int LerpTowardTarget(int baseValue, int softTarget, float progress, int hardMax)
    {
        if (baseValue <= 0)
        {
            return baseValue;
        }

        int target = Math.Max(baseValue, softTarget);
        float lerped = baseValue + ((target - baseValue) * progress);
        return Math.Clamp((int)Math.Round(lerped, MidpointRounding.AwayFromZero), 1, hardMax);
    }

    private static bool HasCompleteSet(SlotData slot)
    {
        return HasAnyValue(slot.StatusWeak)
            && HasAnyValue(slot.StatusNormal)
            && HasAnyValue(slot.StatusStrong)
            && HasAnyValue(slot.StatusVeryStrong)
            && HasAnyValue(slot.StatusStrongest);
    }

    private static bool HasAnyValue(ModelStatus status)
    {
        return status.Hp > 0
            || status.Attack > 0
            || status.Defense > 0
            || status.Speed > 0
            || status.Hit > 0;
    }

    private static ModelStatus Clamp(ModelStatus source)
    {
        ModelStatus status = Clone(source);
        if (status.Hp > 0)
        {
            status.Hp = Math.Min(status.Hp, MaxHp);
        }

        if (status.Attack > 0)
        {
            status.Attack = Math.Min(status.Attack, MaxAttack);
        }

        if (status.Defense > 0)
        {
            status.Defense = Math.Min(status.Defense, MaxDefense);
        }

        if (status.Speed > 0)
        {
            status.Speed = Math.Min(status.Speed, MaxSpeed);
        }

        if (status.Hit > 0)
        {
            status.Hit = Math.Min(status.Hit, MaxHit);
        }

        return status;
    }

    private static ModelStatus Clone(ModelStatus source)
    {
        return new ModelStatus
        {
            Hp = source.Hp,
            Attack = source.Attack,
            Defense = source.Defense,
            Speed = source.Speed,
            Hit = source.Hit
        };
    }

    private sealed class SlotData
    {
        public string? ModelName { get; set; }
        public string? GlbFileName { get; set; }
        public ModelStatus Status { get; set; } = new();
        public ModelStatus StatusWeak { get; set; } = new();
        public ModelStatus StatusNormal { get; set; } = new();
        public ModelStatus StatusStrong { get; set; } = new();
        public ModelStatus StatusVeryStrong { get; set; } = new();
        public ModelStatus StatusStrongest { get; set; } = new();
        public bool HasEnemyStrengthStatuses { get; set; }
        public int EnemyStrengthBalanceVersion { get; set; }
    }

    private sealed class ModelStatus
    {
        public int Hp { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Speed { get; set; }
        public int Hit { get; set; }
    }

    private readonly struct StatWeights
    {
        public StatWeights(float hp, float attack, float defense, float speed, float hit)
        {
            Hp = hp;
            Attack = attack;
            Defense = defense;
            Speed = speed;
            Hit = hit;
        }

        public float Hp { get; }
        public float Attack { get; }
        public float Defense { get; }
        public float Speed { get; }
        public float Hit { get; }

        public StatWeights Normalized()
        {
            float mean = (Hp + Attack + Defense + Speed + Hit) * 0.2f;
            if (mean <= 0.0001f)
            {
                return new StatWeights(1f, 1f, 1f, 1f, 1f);
            }

            return new StatWeights(
                Hp / mean,
                Attack / mean,
                Defense / mean,
                Speed / mean,
                Hit / mean);
        }
    }
}

internal static class ModelStatusDefaults
{
    public const int MinHp = 30;
    public const int MaxHp = 100;
    public const int DefaultHp = 60;
    public const int MinAttack = 20;
    public const int MaxAttack = 100;
    public const int DefaultAttack = 50;
    public const int MinDefense = 20;
    public const int MaxDefense = 100;
    public const int DefaultDefense = 50;
    public const int MinSpeed = 20;
    public const int MaxSpeed = 100;
    public const int DefaultSpeed = 50;
    public const int MinHit = 20;
    public const int MaxHit = 100;
    public const int DefaultHit = 50;
}
