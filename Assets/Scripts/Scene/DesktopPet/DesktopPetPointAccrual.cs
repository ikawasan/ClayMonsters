using System;
using System.Globalization;
using System.IO;
using SaveData;
using SaveData.Interface;
using UnityEngine;

namespace Scene.DesktopPet
{
    /// <summary>
    /// デスクトップペット稼働時間に応じたポイント付与
    /// 1時間あたり10ポイントを実時間で精算する
    /// </summary>
    public static class DesktopPetPointAccrual
    {
        private const string SessionFileName = "points_session.txt";
        private const int PointsPerHour = 10;
        private static readonly long HourTicks = TimeSpan.TicksPerHour;

        /// <summary>
        /// ペットセッションを開始する
        /// </summary>
        public static void BeginSession()
        {
            SettlePending();
            if (TryReadState(out bool active, out _, out _) && active)
            {
                return;
            }

            WriteState(
                active: true,
                lastSettledUtcTicks: DateTime.UtcNow.Ticks,
                endedUtcTicks: 0);
            Debug.Log("[DesktopPetPointAccrual] ポイント付与セッションを開始しました");
        }

        /// <summary>
        /// 未精算分をポイントへ反映する
        /// </summary>
        /// <param name="pointsService">注入済みならメモリ上のポイントも同期する</param>
        /// <returns>今回付与したポイント</returns>
        public static int SettlePending(IPointsService pointsService = null)
        {
            if (!TryReadState(out bool active, out long lastSettledUtcTicks, out long endedUtcTicks))
            {
                return 0;
            }

            long endTicks = ResolveEndTicks(active, endedUtcTicks);
            if (endTicks <= lastSettledUtcTicks)
            {
                return 0;
            }

            long elapsedTicks = endTicks - lastSettledUtcTicks;
            int hours = (int)(elapsedTicks / HourTicks);
            if (hours <= 0)
            {
                return 0;
            }

            int amount = hours * PointsPerHour;
            GrantPoints(amount, pointsService);
            long settledTicks = lastSettledUtcTicks + (hours * HourTicks);
            WriteState(
                active: active && endedUtcTicks <= 0,
                lastSettledUtcTicks: settledTicks,
                endedUtcTicks: endedUtcTicks);
            Debug.Log(
                "[DesktopPetPointAccrual] ポイント精算 hours="
                + hours
                + " amount="
                + amount);
            return amount;
        }

        /// <summary>
        /// ペットセッションを終了し未精算分を反映する
        /// </summary>
        /// <param name="pointsService">注入済みならメモリ上のポイントも同期する</param>
        public static void EndSession(IPointsService pointsService = null)
        {
            if (!TryReadState(out bool active, out long lastSettledUtcTicks, out long endedUtcTicks))
            {
                return;
            }

            if (active && endedUtcTicks <= 0)
            {
                endedUtcTicks = DateTime.UtcNow.Ticks;
                WriteState(
                    active: false,
                    lastSettledUtcTicks: lastSettledUtcTicks,
                    endedUtcTicks: endedUtcTicks);
            }
            else if (active)
            {
                WriteState(
                    active: false,
                    lastSettledUtcTicks: lastSettledUtcTicks,
                    endedUtcTicks: endedUtcTicks);
            }

            SettlePending(pointsService);
            Debug.Log("[DesktopPetPointAccrual] ポイント付与セッションを終了しました");
        }

        private static long ResolveEndTicks(bool active, long endedUtcTicks)
        {
            if (endedUtcTicks > 0)
            {
                return endedUtcTicks;
            }

            if (active)
            {
                return DateTime.UtcNow.Ticks;
            }

            return 0;
        }

        private static void GrantPoints(int amount, IPointsService pointsService)
        {
            if (amount <= 0)
            {
                return;
            }

            if (pointsService != null)
            {
                pointsService.AddPoints(amount);
                return;
            }

            SaveDataManager.Update(data =>
            {
                data.Points = BattlePointsRules.ClampHeldPoints(data.Points + amount);
            });
        }

        private static string SessionFilePath =>
            Path.Combine(DesktopPetSpriteCache.RootDirectory, SessionFileName);

        private static bool TryReadState(
            out bool active,
            out long lastSettledUtcTicks,
            out long endedUtcTicks)
        {
            active = false;
            lastSettledUtcTicks = 0;
            endedUtcTicks = 0;
            string path = SessionFilePath;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    int separator = line.IndexOf('=');
                    if (separator <= 0 || separator >= line.Length - 1)
                    {
                        continue;
                    }

                    string key = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();
                    if (string.Equals(key, "active", StringComparison.OrdinalIgnoreCase))
                    {
                        active = value == "1"
                            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (string.Equals(key, "lastSettledUtcTicks", StringComparison.OrdinalIgnoreCase))
                    {
                        long.TryParse(
                            value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out lastSettledUtcTicks);
                    }
                    else if (string.Equals(key, "endedUtcTicks", StringComparison.OrdinalIgnoreCase))
                    {
                        long.TryParse(
                            value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out endedUtcTicks);
                    }
                }

                return lastSettledUtcTicks > 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[DesktopPetPointAccrual] セッション読込失敗: " + exception.Message);
                return false;
            }
        }

        private static void WriteState(bool active, long lastSettledUtcTicks, long endedUtcTicks)
        {
            try
            {
                Directory.CreateDirectory(DesktopPetSpriteCache.RootDirectory);
                string content =
                    "active=" + (active ? "1" : "0") + "\n"
                    + "lastSettledUtcTicks="
                    + lastSettledUtcTicks.ToString(CultureInfo.InvariantCulture)
                    + "\n"
                    + "endedUtcTicks="
                    + endedUtcTicks.ToString(CultureInfo.InvariantCulture)
                    + "\n";
                File.WriteAllText(SessionFilePath, content);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[DesktopPetPointAccrual] セッション書込失敗: " + exception.Message);
            }
        }
    }
}
