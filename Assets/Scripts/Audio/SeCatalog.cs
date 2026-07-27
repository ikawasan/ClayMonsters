namespace Audio
{
    /// <summary>
    /// SEのResourcesパスを解決する
    /// </summary>
    public static class SeCatalog
    {
        private const string ResourceRoot = "Audio/SE/";
        private const string MagicRoot = ResourceRoot + "Magic/";

        /// <summary>
        /// トラックIDからResourcesパスを取得する
        /// </summary>
        /// <param name="trackId">SEトラック</param>
        /// <returns>Resources.Load用パス</returns>
        public static string GetResourcePath(SeTrackId trackId)
        {
            return trackId switch
            {
                SeTrackId.AttackHit => ResourceRoot + "AttackHit",
                SeTrackId.AttackMiss => ResourceRoot + "AttackMiss",
                SeTrackId.PartsBreak => ResourceRoot + "PartsBreak",
                SeTrackId.PowerCharge => ResourceRoot + "PowerCharge",
                SeTrackId.MagicCircle => MagicRoot + "MagicCircle",
                SeTrackId.MagicFireball => MagicRoot + "Fireball",
                SeTrackId.MagicFireballHit => MagicRoot + "FireballHit",
                SeTrackId.MagicWindSlasher => MagicRoot + "WindSlasher",
                SeTrackId.MagicDiamondDust => MagicRoot + "DirmondDust",
                SeTrackId.MagicThunderShock => MagicRoot + "ThunderShock",
                _ => string.Empty
            };
        }
    }
}
