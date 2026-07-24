using SaveData;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 行き先ごとのステータス上昇量
    /// </summary>
    public readonly struct TrainingStatGain
    {
        public TrainingStatGain(int hp, int attack, int defense, int speed, int hit)
        {
            Hp = hp;
            Attack = attack;
            Defense = defense;
            Speed = speed;
            Hit = hit;
        }

        /// <summary>
        /// HP上昇量
        /// </summary>
        public int Hp { get; }

        /// <summary>
        /// 攻撃力上昇量
        /// </summary>
        public int Attack { get; }

        /// <summary>
        /// 防御力上昇量
        /// </summary>
        public int Defense { get; }

        /// <summary>
        /// 速度上昇量
        /// </summary>
        public int Speed { get; }

        /// <summary>
        /// 命中上昇量
        /// </summary>
        public int Hit { get; }

        /// <summary>
        /// 別の上昇量を加算した結果を返す
        /// </summary>
        public TrainingStatGain Add(TrainingStatGain other)
        {
            return new TrainingStatGain(
                Hp + other.Hp,
                Attack + other.Attack,
                Defense + other.Defense,
                Speed + other.Speed,
                Hit + other.Hit);
        }
    }

    /// <summary>
    /// 行き先の表示名と基礎ステータス上昇を解決する
    /// </summary>
    public static class TrainingLocationCatalog
    {
        /// <summary>
        /// 行き先の日本語名を返す
        /// </summary>
        /// <param name="location">行き先</param>
        public static string GetDisplayName(TrainingLocation location)
        {
            return location switch
            {
                TrainingLocation.ScienceLab => "理科室",
                TrainingLocation.HomeEcRoom => "家庭科室",
                TrainingLocation.CraftRoom => "図工室",
                TrainingLocation.Library => "図書室",
                TrainingLocation.MusicRoom => "音楽室",
                TrainingLocation.Gymnasium => "体育館",
                TrainingLocation.PrincipalOffice => "校長室",
                _ => string.Empty
            };
        }

        /// <summary>
        /// 行き先の基礎ステータス上昇量を返す
        /// </summary>
        /// <param name="location">行き先</param>
        public static TrainingStatGain GetBaseGain(TrainingLocation location)
        {
            return location switch
            {
                TrainingLocation.ScienceLab => new TrainingStatGain(0, 4, 1, 0, 1),
                TrainingLocation.HomeEcRoom => new TrainingStatGain(6, 0, 2, 0, 0),
                TrainingLocation.CraftRoom => new TrainingStatGain(0, 1, 4, 0, 0),
                TrainingLocation.Library => new TrainingStatGain(0, 0, 2, 1, 4),
                TrainingLocation.MusicRoom => new TrainingStatGain(0, 0, 0, 4, 2),
                TrainingLocation.Gymnasium => new TrainingStatGain(4, 3, 0, 2, 1),
                TrainingLocation.PrincipalOffice => new TrainingStatGain(3, 3, 3, 3, 3),
                _ => default
            };
        }

        /// <summary>
        /// 全行き先の配列を返す
        /// </summary>
        public static TrainingLocation[] AllLocations { get; } =
        {
            TrainingLocation.ScienceLab,
            TrainingLocation.HomeEcRoom,
            TrainingLocation.CraftRoom,
            TrainingLocation.Library,
            TrainingLocation.MusicRoom,
            TrainingLocation.Gymnasium,
            TrainingLocation.PrincipalOffice
        };
    }

    /// <summary>
    /// 時間割スロットの表示名と休憩判定を解決する
    /// </summary>
    public static class TrainingPeriodCatalog
    {
        /// <summary>
        /// 時間割の日本語名を返す
        /// </summary>
        /// <param name="period">時間割</param>
        public static string GetDisplayName(TrainingPeriod period)
        {
            return period switch
            {
                TrainingPeriod.FirstHour => "1時間目",
                TrainingPeriod.SecondHour => "2時間目",
                TrainingPeriod.MorningBreak => "中休み",
                TrainingPeriod.ThirdHour => "3時間目",
                TrainingPeriod.FourthHour => "4時間目",
                TrainingPeriod.LunchBreak => "昼休み",
                TrainingPeriod.FifthHour => "5時間目",
                TrainingPeriod.SixthHour => "6時間目",
                TrainingPeriod.AfterSchool => "放課後",
                _ => string.Empty
            };
        }

        /// <summary>
        /// 休憩時間かどうかを返す
        /// </summary>
        /// <param name="period">時間割</param>
        public static bool IsRestPeriod(TrainingPeriod period)
        {
            return period == TrainingPeriod.LunchBreak;
        }

        /// <summary>
        /// 売店が使える時間かどうかを返す
        /// </summary>
        /// <param name="period">時間割</param>
        public static bool IsShopPeriod(TrainingPeriod period)
        {
            return period == TrainingPeriod.LunchBreak;
        }

        /// <summary>
        /// 訓練コマンドを選ぶ時間かどうかを返す
        /// </summary>
        /// <param name="period">時間割</param>
        public static bool IsCommandPeriod(TrainingPeriod period)
        {
            return period == TrainingPeriod.FirstHour
                || period == TrainingPeriod.SecondHour
                || period == TrainingPeriod.ThirdHour
                || period == TrainingPeriod.FourthHour
                || period == TrainingPeriod.FifthHour
                || period == TrainingPeriod.SixthHour;
        }

        /// <summary>
        /// 行き先選択が必要な時間かどうかを返す
        /// </summary>
        /// <param name="period">時間割</param>
        public static bool RequiresLocationChoice(TrainingPeriod period)
        {
            return IsCommandPeriod(period);
        }

        /// <summary>
        /// 放課後の戦闘時間かどうかを返す
        /// </summary>
        /// <param name="period">時間割</param>
        public static bool IsBattlePeriod(TrainingPeriod period)
        {
            return period == TrainingPeriod.AfterSchool;
        }
    }

    /// <summary>
    /// 曜日の表示名を解決する
    /// </summary>
    public static class TrainingDayCatalog
    {
        /// <summary>
        /// 曜日の日本語名を返す
        /// </summary>
        /// <param name="day">曜日</param>
        public static string GetDisplayName(TrainingDayOfWeek day)
        {
            return day switch
            {
                TrainingDayOfWeek.Monday => "月曜日",
                TrainingDayOfWeek.Tuesday => "火曜日",
                TrainingDayOfWeek.Wednesday => "水曜日",
                TrainingDayOfWeek.Thursday => "木曜日",
                TrainingDayOfWeek.Friday => "金曜日",
                _ => string.Empty
            };
        }
    }
}
