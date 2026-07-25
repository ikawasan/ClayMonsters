using System;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 訓練主ステの表示と基礎上昇を解決する
    /// </summary>
    public static class TrainingFocusCatalog
    {
        /// <summary>
        /// 全主ステ一覧
        /// </summary>
        public static TrainingFocus[] AllFocuses { get; } =
        {
            TrainingFocus.Hp,
            TrainingFocus.Attack,
            TrainingFocus.Defense,
            TrainingFocus.Speed,
            TrainingFocus.Hit
        };

        /// <summary>
        /// 主ステ候補から指定数を重複なしでランダム抽選する
        /// </summary>
        /// <param name="count">抽選数</param>
        /// <param name="random">乱数</param>
        /// <returns>抽選結果</returns>
        public static TrainingFocus[] PickRandomFocuses(int count, System.Random random)
        {
            TrainingFocus[] source = AllFocuses;
            int take = Mathf.Clamp(count, 0, source.Length);
            if (take <= 0)
            {
                return Array.Empty<TrainingFocus>();
            }

            if (random == null)
            {
                random = new System.Random();
            }

            var pool = new TrainingFocus[source.Length];
            Array.Copy(source, pool, source.Length);
            for (int i = pool.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                TrainingFocus temp = pool[i];
                pool[i] = pool[swapIndex];
                pool[swapIndex] = temp;
            }

            var result = new TrainingFocus[take];
            Array.Copy(pool, result, take);
            return result;
        }

        /// <summary>
        /// 表示名を返す
        /// </summary>
        /// <param name="focus">主ステ</param>
        public static string GetDisplayName(TrainingFocus focus)
        {
            return focus switch
            {
                TrainingFocus.Hp => "HP訓練",
                TrainingFocus.Attack => "攻撃訓練",
                TrainingFocus.Defense => "防御訓練",
                TrainingFocus.Speed => "速さ訓練",
                TrainingFocus.Hit => "命中訓練",
                _ => focus.ToString()
            };
        }

        /// <summary>
        /// 背景用の行き先を返す
        /// </summary>
        /// <param name="focus">主ステ</param>
        public static TrainingLocation GetPresentationLocation(TrainingFocus focus)
        {
            return focus switch
            {
                TrainingFocus.Hp => TrainingLocation.HomeEcRoom,
                TrainingFocus.Attack => TrainingLocation.Gymnasium,
                TrainingFocus.Defense => TrainingLocation.CraftRoom,
                TrainingFocus.Speed => TrainingLocation.MusicRoom,
                TrainingFocus.Hit => TrainingLocation.Library,
                _ => TrainingLocation.ScienceLab
            };
        }

        /// <summary>
        /// 成功時の基礎上昇を返す
        /// </summary>
        /// <param name="focus">主ステ</param>
        public static TrainingStatGain GetBaseGain(TrainingFocus focus)
        {
            return focus switch
            {
                TrainingFocus.Hp => new TrainingStatGain(8, 0, 2, 4, 0),
                TrainingFocus.Attack => new TrainingStatGain(4, 8, 0, 2, 1),
                TrainingFocus.Defense => new TrainingStatGain(2, 4, 8, 0, 0),
                TrainingFocus.Speed => new TrainingStatGain(0, 2, 4, 8, 2),
                TrainingFocus.Hit => new TrainingStatGain(0, 2, 0, 2, 8),
                _ => default
            };
        }

        /// <summary>
        /// 上昇量へ倍率を適用する
        /// </summary>
        /// <param name="gain">元上昇</param>
        /// <param name="multiplier">倍率</param>
        public static TrainingStatGain ScaleGain(TrainingStatGain gain, float multiplier)
        {
            return new TrainingStatGain(
                Mathf.RoundToInt(gain.Hp * multiplier),
                Mathf.RoundToInt(gain.Attack * multiplier),
                Mathf.RoundToInt(gain.Defense * multiplier),
                Mathf.RoundToInt(gain.Speed * multiplier),
                Mathf.RoundToInt(gain.Hit * multiplier));
        }
    }
}
