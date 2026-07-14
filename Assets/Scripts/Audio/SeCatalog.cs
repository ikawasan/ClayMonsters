namespace Audio
{
    /// <summary>
    /// SEのResourcesパスを解決する
    /// </summary>
    public static class SeCatalog
    {
        private const string ResourceRoot = "Audio/SE/";

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
                _ => string.Empty
            };
        }
    }
}
