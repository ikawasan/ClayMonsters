using R3;
using SaveData.Interface;
using UnityEngine;

namespace SaveData.Service
{
    /// <summary>
    /// 育成ゴールドとは別のゲーム内ポイントの読み書き
    /// </summary>
    public sealed class PointsService : IPointsService
    {
        private readonly ReactiveProperty<int> pointsProperty = new(0);

        /// <summary>
        /// 永続化データを読み込む
        /// </summary>
        public PointsService()
        {
            ReloadFromDisk();
        }

        /// <inheritdoc />
        public int Points => pointsProperty.Value;

        /// <inheritdoc />
        public Observable<int> PointsObservable => pointsProperty;

        /// <inheritdoc />
        public void AddPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (Points >= BattlePointsRules.MaxHeldPoints)
            {
                return;
            }

            int next = BattlePointsRules.ClampHeldPoints(Points + amount);
            if (next == Points)
            {
                return;
            }

            pointsProperty.Value = next;
            Persist();
            Debug.Log($"[PointsService] ポイント加算 amount={amount} total={Points}");
        }

        /// <inheritdoc />
        public void Reload()
        {
            ReloadFromDisk();
        }

        private void ReloadFromDisk()
        {
            SaveData saveData = SaveDataManager.Load();
            pointsProperty.Value = BattlePointsRules.ClampHeldPoints(saveData.Points);
        }

        private void Persist()
        {
            int current = Points;
            SaveDataManager.Update(data =>
            {
                data.Points = current;
            });
        }
    }
}
