namespace ClayMonstersPet;

/// <summary>
/// デスクトップペットの移動速度定数
/// </summary>
internal static class PetMovementSpeeds
{
    /// <summary>
    /// 通常移動の最大速度(px/s)
    /// </summary>
    public const float MaxWalkPixelsPerSec = 125f;

    /// <summary>
    /// バトル移動の最大速度(px/s)
    /// </summary>
    public const float MaxBattlePixelsPerSec = 1400f;

    /// <summary>
    /// 列追従の速度(px/s)
    /// </summary>
    public const float LineFollowPixelsPerSec = 120f;

    /// <summary>
    /// 列先頭の最小速度(px/s)
    /// </summary>
    public const float LineLeaderMinPixelsPerSec = 95f;

    /// <summary>
    /// 列先頭の最大速度(px/s)
    /// </summary>
    public const float LineLeaderMaxPixelsPerSec = 125f;

    /// <summary>
    /// ソロ散策の目標速度(px/s)
    /// </summary>
    public const float SoloWanderPixelsPerSec = 110f;

    /// <summary>
    /// ソロ散策の最短時間(秒)
    /// </summary>
    public const float SoloWanderMinDurationSeconds = 4.5f;

    /// <summary>
    /// ソロ散策の最長時間(秒)
    /// </summary>
    public const float SoloWanderMaxDurationSeconds = 12f;

    /// <summary>
    /// ボール追跡(ドラッグ追従)の最小速度(px/s)
    /// </summary>
    public const float ChaseBallLiveMinPixelsPerSec = 120f;

    /// <summary>
    /// ボール追跡(ドラッグ追従)の最大速度(px/s)
    /// </summary>
    public const float ChaseBallLiveMaxPixelsPerSec = 145f;

    /// <summary>
    /// ボール追跡(通常)の最小速度(px/s)
    /// </summary>
    public const float ChaseBallNormalMinPixelsPerSec = 85f;

    /// <summary>
    /// ボール追跡(通常)の最大速度(px/s)
    /// </summary>
    public const float ChaseBallNormalMaxPixelsPerSec = 105f;

    /// <summary>
    /// ボール前の突進速度(px/s)
    /// </summary>
    public const float ChargeBallPixelsPerSec = 155f;

    /// <summary>
    /// バトル開始位置への速度(px/s)
    /// </summary>
    public const float BattleSlotInPixelsPerSec = 700f;

    /// <summary>
    /// バトル構え位置への速度(px/s)
    /// </summary>
    public const float BattleWindupPixelsPerSec = 900f;

    /// <summary>
    /// バトル接近の速度(px/s)
    /// </summary>
    public const float BattleApproachPixelsPerSec = 1400f;

    /// <summary>
    /// バトルノックバックの速度(px/s)
    /// </summary>
    public const float BattleKnockbackPixelsPerSec = 1200f;

    /// <summary>
    /// ボールキックの速度(px/s)
    /// </summary>
    public const float BallKickPixelsPerSec = 240f;

    /// <summary>
    /// 移動距離から所要時間を返す
    /// </summary>
    public static float DurationFromTravel(
        float travelPixels,
        float speedPixelsPerSec,
        float minDurationSeconds,
        float maxDurationSeconds)
    {
        float speed = Math.Max(40f, speedPixelsPerSec);
        return Math.Clamp(travelPixels / speed, minDurationSeconds, maxDurationSeconds);
    }

    /// <summary>
    /// 状態に応じた最大速度で所要時間を下限補正する
    /// </summary>
    public static float EnforceMinimumDuration(
        float travelPixels,
        float requestedDurationSeconds,
        PetAiState state)
    {
        if (travelPixels <= 0.5f)
        {
            return Math.Max(0.18f, requestedDurationSeconds);
        }

        float maxSpeed = state == PetAiState.Battle
            ? MaxBattlePixelsPerSec
            : MaxWalkPixelsPerSec;
        float minDuration = travelPixels / maxSpeed;
        return Math.Max(0.18f, Math.Max(requestedDurationSeconds, minDuration));
    }
}
