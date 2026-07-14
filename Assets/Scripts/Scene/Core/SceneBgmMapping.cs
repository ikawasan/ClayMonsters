using Audio;
using Lighthouse.Scene;

namespace Scene.Core
{
    /// <summary>
    /// メインシーンIDと入場時BGMトラックの対応を解決する
    /// </summary>
    public static class SceneBgmMapping
    {
        /// <summary>
        /// シーン入場時に再生するBGMを取得する
        /// </summary>
        /// <param name="mainSceneId">メインシーンID</param>
        /// <param name="trackId">再生するBGM</param>
        /// <returns>入場BGMが定義されている場合true</returns>
        public static bool TryGetTrack(MainSceneId mainSceneId, out BgmTrackId trackId)
        {
            if (mainSceneId == ClayMonstersMainSceneId.Title)
            {
                trackId = BgmTrackId.Title;
                return true;
            }

            if (mainSceneId == ClayMonstersMainSceneId.ClayEdit)
            {
                trackId = BgmTrackId.ClayEdit;
                return true;
            }

            if (mainSceneId == ClayMonstersMainSceneId.Training)
            {
                trackId = BgmTrackId.Training;
                return true;
            }

            trackId = default;
            return false;
        }

        /// <summary>
        /// 遷移前に現在BGMをフェードアウトすべきか判定する
        /// </summary>
        /// <param name="nextMainSceneId">遷移先シーン</param>
        /// <param name="currentTrackId">再生中トラック</param>
        /// <returns>フェードアウトが必要ならtrue</returns>
        public static bool ShouldFadeOutForTransition(MainSceneId nextMainSceneId, BgmTrackId? currentTrackId)
        {
            if (!currentTrackId.HasValue)
            {
                return false;
            }

            if (!TryGetTrack(nextMainSceneId, out BgmTrackId nextTrackId))
            {
                return true;
            }

            return nextTrackId != currentTrackId.Value;
        }
    }
}
