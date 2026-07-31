using R3;

namespace SaveData.Interface
{
    /// <summary>
    /// 育成ゴールドとは別のゲーム内ポイント
    /// </summary>
    public interface IPointsService
    {
        /// <summary>
        /// 現在のポイント
        /// </summary>
        int Points { get; }

        /// <summary>
        /// ポイント変化の購読
        /// </summary>
        Observable<int> PointsObservable { get; }

        /// <summary>
        /// ポイントを加算して保存する
        /// </summary>
        /// <param name="amount">加算量(0以下は無視)</param>
        void AddPoints(int amount);

        /// <summary>
        /// 永続化データから再読込する
        /// </summary>
        void Reload();
    }
}
