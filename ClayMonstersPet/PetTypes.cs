namespace ClayMonstersPet;

internal enum PetFacing
{
    AnglePos45 = 0,
    AngleNeg45 = 1,
    Front = 2
}

internal enum PetAction
{
    Idle = 0,
    Walk = 1,
    Attack = 2
}

internal enum PetAiState
{
    Idle = 0,
    Walk = 1,
    Sleep = 2,
    Dragged = 3,
    LineFollow = 4,
    Sing = 5,
    Battle = 6,
    Play = 7
}

/// <summary>
/// 複数ペットのソロ行動が偏らないよう重みを返す
/// </summary>
internal interface IPetBehaviorAdvisor
{
    /// <summary>
    /// 指定行動の抽選重み倍率を返す
    /// </summary>
    float GetSoloWeight(PetForm self, PetAiState state);
}
