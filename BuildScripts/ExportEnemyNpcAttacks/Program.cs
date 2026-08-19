using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

const int AttackDrawVersion = 3;
const int NpcStoredDrawSalt = 0xC38B14;
const int SlotCount = 4;

string projectRoot = args.Length > 0
    ? args[0]
    : Directory.GetCurrentDirectory();
string catalogPath = Path.Combine(
    projectRoot,
    "Assets",
    "StreamingAssets",
    "ModelSave",
    "EnemyModelSave.json.gz");

if (!File.Exists(catalogPath))
{
    Console.Error.WriteLine($"catalog not found: {catalogPath}");
    return 1;
}

string json = ReadGzipText(catalogPath);
JsonNode? root = JsonNode.Parse(json);
JsonArray? slots = root?["slots"]?.AsArray();
if (slots == null)
{
    Console.Error.WriteLine("failed to parse enemy catalog slots");
    return 1;
}

int used = 0;
int attackUpdated = 0;
int statusUpdated = 0;
for (int i = 0; i < slots.Count; i++)
{
    if (slots[i] is not JsonObject slot)
    {
        continue;
    }

    if (slot["isUsed"]?.GetValue<bool>() != true)
    {
        continue;
    }

    used++;
    StatusCatalog.ApplyToSlot(slot);
    statusUpdated++;

    List<int> baseAttacks = ReadIntArray(slot["attackMotions"]);
    slot["hasEnemyStrengthAttacks"] = true;
    slot["enemyStrengthAttackVersion"] = AttackDrawVersion;
    slot["attackMotionsWeak"] = ToJsonArray(DrawTier(EnemyStrengthTier.Weak, baseAttacks, i));
    slot["attackMotionsNormal"] = ToJsonArray(DrawTier(EnemyStrengthTier.Normal, baseAttacks, i));
    slot["attackMotionsStrong"] = ToJsonArray(DrawTier(EnemyStrengthTier.Strong, baseAttacks, i));
    slot["attackMotionsVeryStrong"] = ToJsonArray(DrawTier(EnemyStrengthTier.VeryStrong, baseAttacks, i));
    slot["attackMotionsStrongest"] = ToJsonArray(DrawTier(EnemyStrengthTier.Strongest, baseAttacks, i));
    attackUpdated++;
}

string outputJson = root!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
WriteGzipText(catalogPath, outputJson);

Console.WriteLine(
    $"exported used={used} statusUpdated={statusUpdated} attacksUpdated={attackUpdated}"
        + $" balanceVersion={StatusCatalog.BalanceVersion}"
        + $" attackVersion={AttackDrawVersion}"
        + $" path={catalogPath}");
if (slots[0] is JsonObject sample)
{
    Console.WriteLine($"sample0 weak={sample["statusWeak"]}");
    Console.WriteLine($"sample0 very={sample["statusVeryStrong"]}");
    Console.WriteLine($"sample0 strongest={sample["statusStrongest"]}");
    Console.WriteLine($"sample0 attacksVery={sample["attackMotionsVeryStrong"]}");
}

return 0;

static JsonArray ToJsonArray(IReadOnlyList<int> values)
{
    JsonArray array = new();
    for (int i = 0; i < values.Count; i++)
    {
        array.Add(values[i]);
    }

    return array;
}

static List<int> ReadIntArray(JsonNode? node)
{
    List<int> values = new();
    if (node is not JsonArray array)
    {
        return values;
    }

    foreach (JsonNode? item in array)
    {
        if (item == null)
        {
            continue;
        }

        values.Add(item.GetValue<int>());
    }

    return values;
}

static string ReadGzipText(string path)
{
    using FileStream fileStream = File.OpenRead(path);
    using GZipStream gzip = new(fileStream, CompressionMode.Decompress);
    using StreamReader reader = new(gzip, Encoding.UTF8);
    return reader.ReadToEnd();
}

static void WriteGzipText(string path, string text)
{
    using FileStream fileStream = File.Create(path);
    using GZipStream gzip = new(fileStream, CompressionLevel.Optimal);
    using StreamWriter writer = new(gzip, new UTF8Encoding(false));
    writer.Write(text);
}

static List<int> DrawTier(EnemyStrengthTier tier, List<int> savedAttacks, int slotIndex)
{
    HashSet<BonePart> parts = InferAvailableParts(savedAttacks);
    List<MotionType> pool = CollectAttacksForAvailableParts(parts);
    return Resolve(tier, pool, slotIndex, SlotCount, NpcStoredDrawSalt)
        .Select(motion => (int)motion)
        .ToList();
}

static HashSet<BonePart> InferAvailableParts(List<int> savedAttacks)
{
    HashSet<BonePart> parts = new() { BonePart.Body };
    foreach (int raw in savedAttacks)
    {
        MotionType motion = (MotionType)raw;
        if (!IsAttackMotion(motion))
        {
            continue;
        }

        parts.Add(GetRequiredPart(motion));
    }

    return parts;
}

static List<MotionType> CollectAttacksForAvailableParts(HashSet<BonePart> availableParts)
{
    availableParts.Add(BonePart.Body);
    List<MotionType> attacks = new();
    foreach (MotionType motion in Enum.GetValues<MotionType>())
    {
        if (!IsAttackMotion(motion))
        {
            continue;
        }

        if (availableParts.Contains(GetRequiredPart(motion)))
        {
            attacks.Add(motion);
        }
    }

    return attacks;
}

static List<MotionType> Resolve(
    EnemyStrengthTier tier,
    List<MotionType> usableAttacks,
    int slotIndex,
    int slotCount,
    int drawSalt)
{
    int count = Math.Max(0, slotCount);
    List<MotionType> result = new(count);
    if (count == 0)
    {
        return result;
    }

    List<MotionType> pool = BuildUsablePool(usableAttacks);
    if (pool.Count == 0)
    {
        return result;
    }

    Random random = new(BuildSeed(slotIndex, tier, drawSalt));
    int[] rankTargets = BuildRankTargets(tier, count, random);
    HashSet<BonePart> usedParts = new();

    for (int i = 0; i < count; i++)
    {
        MotionType? picked = TryPick(pool, result, usedParts, rankTargets[i], true, random)
            ?? TryPick(pool, result, usedParts, rankTargets[i], false, random)
            ?? TryPickAny(pool, result, random);
        if (!picked.HasValue)
        {
            break;
        }

        result.Add(picked.Value);
        usedParts.Add(GetRequiredPart(picked.Value));
    }

    return result;
}

static List<MotionType> BuildUsablePool(List<MotionType> usableAttacks)
{
    List<MotionType> pool = new();
    foreach (MotionType motion in usableAttacks)
    {
        if (!IsAttackMotion(motion) || pool.Contains(motion))
        {
            continue;
        }

        pool.Add(motion);
    }

    return pool;
}

static int BuildSeed(int slotIndex, EnemyStrengthTier tier, int drawSalt)
{
    unchecked
    {
        return (slotIndex + 1) * 397 ^ ((int)tier + 1) * 7919 ^ drawSalt;
    }
}

static int[] BuildRankTargets(EnemyStrengthTier tier, int count, Random random)
{
    int[] targets = new int[count];
    for (int i = 0; i < count; i++)
    {
        targets[i] = tier switch
        {
            EnemyStrengthTier.Weak => 1,
            EnemyStrengthTier.Normal => i < count / 2 ? 1 : 2,
            EnemyStrengthTier.Strong => i < count / 2 ? 2 : 3,
            EnemyStrengthTier.VeryStrong => random.Next(1, 4),
            EnemyStrengthTier.Strongest => random.Next(2, 4),
            _ => 1
        };
    }

    if (tier == EnemyStrengthTier.Weak && count > 0 && random.Next(0, 100) < 12)
    {
        targets[random.Next(0, count)] = 2;
    }

    if (tier == EnemyStrengthTier.Normal && count > 0 && random.Next(0, 100) < 18)
    {
        targets[random.Next(0, count)] = 3;
    }

    ShuffleInPlace(targets, random);
    return targets;
}

static MotionType? TryPick(
    List<MotionType> pool,
    List<MotionType> used,
    HashSet<BonePart> usedParts,
    int preferredRank,
    bool preferUniquePart,
    Random random)
{
    List<MotionType> exact = new();
    List<MotionType> near = new();
    foreach (MotionType motion in pool)
    {
        if (used.Contains(motion))
        {
            continue;
        }

        BonePart part = GetRequiredPart(motion);
        if (preferUniquePart
            && usedParts.Contains(part)
            && HasUnusedPartCandidate(pool, used, usedParts))
        {
            continue;
        }

        int rank = GetStrengthRank(motion);
        if (rank == preferredRank)
        {
            exact.Add(motion);
        }
        else if (Math.Abs(rank - preferredRank) == 1)
        {
            near.Add(motion);
        }
    }

    if (exact.Count > 0)
    {
        return exact[random.Next(exact.Count)];
    }

    if (near.Count > 0)
    {
        return near[random.Next(near.Count)];
    }

    return null;
}

static MotionType? TryPickAny(List<MotionType> pool, List<MotionType> used, Random random)
{
    List<MotionType> candidates = new();
    foreach (MotionType motion in pool)
    {
        if (!used.Contains(motion))
        {
            candidates.Add(motion);
        }
    }

    return candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : null;
}

static bool HasUnusedPartCandidate(
    List<MotionType> pool,
    List<MotionType> used,
    HashSet<BonePart> usedParts)
{
    foreach (MotionType motion in pool)
    {
        if (used.Contains(motion))
        {
            continue;
        }

        if (!usedParts.Contains(GetRequiredPart(motion)))
        {
            return true;
        }
    }

    return false;
}

static void ShuffleInPlace(int[] values, Random random)
{
    for (int i = values.Length - 1; i > 0; i--)
    {
        int j = random.Next(0, i + 1);
        (values[i], values[j]) = (values[j], values[i]);
    }
}

static int GetStrengthRank(MotionType motion) => motion switch
{
    MotionType.Slap or MotionType.Punch or MotionType.LowSweep or MotionType.Tackle
        or MotionType.HipCheck or MotionType.Chop or MotionType.DoubleSlap or MotionType.Peck => 1,
    MotionType.Kick or MotionType.Headbutt or MotionType.ShoulderRam or MotionType.Knee
        or MotionType.Elbow or MotionType.Bite or MotionType.Uppercut or MotionType.TailWhip
        or MotionType.DoubleKick or MotionType.HornAttack or MotionType.TailSlam or MotionType.Rollout => 2,
    MotionType.GroundPound or MotionType.Stomp or MotionType.BellyFlop or MotionType.BodySlam
        or MotionType.SpinTackle or MotionType.Fireball or MotionType.WindSlasher
        or MotionType.DiamondDust or MotionType.ThunderShock or MotionType.HammerArm
        or MotionType.DropKick => 3,
    _ => 1
};

static BonePart GetRequiredPart(MotionType motion) => motion switch
{
    MotionType.Punch or MotionType.Elbow or MotionType.Uppercut or MotionType.Slap
        or MotionType.Chop or MotionType.DoubleSlap or MotionType.HammerArm => BonePart.Arm,
    MotionType.Kick or MotionType.Stomp or MotionType.Knee or MotionType.LowSweep
        or MotionType.DoubleKick or MotionType.DropKick => BonePart.Leg,
    MotionType.TailWhip or MotionType.TailSlam => BonePart.Back,
    MotionType.Headbutt or MotionType.Bite or MotionType.Peck or MotionType.HornAttack => BonePart.Front,
    _ => BonePart.Body
};

static bool IsAttackMotion(MotionType type) =>
    type is MotionType.Tackle or MotionType.Punch or MotionType.Kick or MotionType.SpinTackle
        or MotionType.TailWhip or MotionType.Headbutt or MotionType.Elbow or MotionType.Stomp
        or MotionType.BodySlam or MotionType.Uppercut or MotionType.Knee or MotionType.ShoulderRam
        or MotionType.BellyFlop or MotionType.HipCheck or MotionType.GroundPound or MotionType.Slap
        or MotionType.LowSweep or MotionType.Bite or MotionType.Fireball or MotionType.WindSlasher
        or MotionType.DiamondDust or MotionType.ThunderShock or MotionType.Chop or MotionType.DoubleSlap
        or MotionType.HammerArm or MotionType.DoubleKick or MotionType.DropKick or MotionType.Peck
        or MotionType.HornAttack or MotionType.TailSlam or MotionType.Rollout;

enum EnemyStrengthTier
{
    Weak = 0,
    Normal = 1,
    Strong = 2,
    VeryStrong = 3,
    Strongest = 4
}

enum BonePart
{
    Body = 0,
    Leg = 1,
    Arm = 2,
    Front = 3,
    Back = 4
}

enum MotionType
{
    None = 0,
    Idle = 1,
    Run = 2,
    LegRun = 3,
    Tackle = 4,
    Punch = 5,
    Kick = 6,
    SpinTackle = 7,
    TailWhip = 8,
    StepForward = 9,
    StepBackward = 10,
    Hit = 11,
    AttackCharge = 12,
    Headbutt = 13,
    Elbow = 14,
    Stomp = 15,
    BodySlam = 16,
    Uppercut = 17,
    Knee = 18,
    ShoulderRam = 19,
    BellyFlop = 20,
    HipCheck = 21,
    GroundPound = 22,
    Slap = 23,
    LowSweep = 24,
    Bite = 25,
    Fireball = 26,
    WindSlasher = 27,
    DiamondDust = 28,
    ThunderShock = 29,
    Chop = 30,
    DoubleSlap = 31,
    HammerArm = 32,
    DoubleKick = 33,
    DropKick = 34,
    Peck = 35,
    HornAttack = 36,
    TailSlam = 37,
    Rollout = 38
}
