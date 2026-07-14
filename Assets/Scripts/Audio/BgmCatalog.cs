namespace Audio
{
    /// <summary>
    /// BGMのResourcesパスを解決する
    /// </summary>
    public static class BgmCatalog
    {
        private const string ResourceRoot = "Audio/BGM/";

        /// <summary>
        /// トラックIDからResourcesパスを取得する
        /// </summary>
        /// <param name="trackId">BGMトラック</param>
        /// <returns>Resources.Load用パス</returns>
        public static string GetResourcePath(BgmTrackId trackId)
        {
            return trackId switch
            {
                BgmTrackId.Title => ResourceRoot + "BGM_Title",
                BgmTrackId.ClayEdit => ResourceRoot + "BGM_ClayEdit",
                BgmTrackId.Training => ResourceRoot + "BGM_Training",
                BgmTrackId.BattleSelection => ResourceRoot + "BGM_BattleSelection",
                BgmTrackId.Battle => ResourceRoot + "BGM_Battle",
                _ => string.Empty
            };
        }
    }
}
