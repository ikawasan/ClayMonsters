using SaveData;
using System;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 自動育成の放課後戦闘結果を簡易判定する
    /// </summary>
    public static class TrainingAutoBattleResolver
    {
        /// <summary>
        /// プレイヤー勝利かを返す
        /// </summary>
        /// <param name="playerStatus">プレイヤーステータス</param>
        /// <param name="enemyStatus">敵ステータス</param>
        /// <param name="random">乱数</param>
        public static bool TrySimulateVictory(
            ModelStatus playerStatus,
            ModelStatus enemyStatus,
            System.Random random)
        {
            if (playerStatus == null)
            {
                return false;
            }

            if (enemyStatus == null)
            {
                return true;
            }

            float playerPower = EvaluatePower(playerStatus);
            float enemyPower = EvaluatePower(enemyStatus);
            if (enemyPower <= 0f)
            {
                return true;
            }

            float ratio = playerPower / enemyPower;
            float winChance = 0.35f + ratio * 0.35f;
            winChance = Math.Clamp(winChance, 0.12f, 0.92f);
            return random.NextDouble() < winChance;
        }

        private static float EvaluatePower(ModelStatus status)
        {
            return status.hp
                + status.attack * 2.2f
                + status.defense * 1.4f
                + status.speed * 1.1f;
        }
    }
}
