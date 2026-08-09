using Localization;
using System.Collections.Generic;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 売店の商品カタログ
    /// </summary>
    public static class TrainingShopCatalog
    {
        /// <summary>
        /// 全商品一覧
        /// </summary>
        public static IReadOnlyList<TrainingShopItem> AllItems { get; } = new[]
        {
            new TrainingShopItem(
                "stamina_drink",
                "栄養ドリンク",
                $"体力+{TrainingSettings.ShopStaminaDrinkRecover}",
                TrainingSettings.ShopStaminaDrinkPrice,
                TrainingShopItemType.StaminaRecover,
                default,
                TrainingSettings.ShopStaminaDrinkRecover,
                0f,
                0),
            new TrainingShopItem(
                "stamina_full",
                "完全回復薬",
                "体力を全回復",
                TrainingSettings.ShopStaminaFullPrice,
                TrainingShopItemType.StaminaFullRecover,
                default,
                TrainingSettings.MaxStamina,
                0f,
                0),
            new TrainingShopItem(
                "stat_hp",
                "HP強化剤",
                "HP+8",
                TrainingSettings.ShopStatBoostPrice,
                TrainingShopItemType.StatBoost,
                new TrainingStatGain(8, 0, 0, 0, 0),
                0,
                0f,
                0),
            new TrainingShopItem(
                "stat_attack",
                "攻撃強化剤",
                "攻撃+6",
                TrainingSettings.ShopStatBoostPrice,
                TrainingShopItemType.StatBoost,
                new TrainingStatGain(0, 6, 0, 0, 0),
                0,
                0f,
                0),
            new TrainingShopItem(
                "stat_defense",
                "防御強化剤",
                "防御+6",
                TrainingSettings.ShopStatBoostPrice,
                TrainingShopItemType.StatBoost,
                new TrainingStatGain(0, 0, 6, 0, 0),
                0,
                0f,
                0),
            new TrainingShopItem(
                "stat_speed",
                "速さ強化剤",
                "速度+6",
                TrainingSettings.ShopStatBoostPrice,
                TrainingShopItemType.StatBoost,
                new TrainingStatGain(0, 0, 0, 6, 0),
                0,
                0f,
                0),
            new TrainingShopItem(
                "stat_hit",
                "命中強化剤",
                "命中+6",
                TrainingSettings.ShopStatBoostPrice,
                TrainingShopItemType.StatBoost,
                new TrainingStatGain(0, 0, 0, 0, 6),
                0,
                0f,
                0),
            new TrainingShopItem(
                "train_boost",
                "カクリツン",
                $"大成功率+{TrainingSettings.ShopTrainBoostPercent:0}%を"
                    + $"{TrainingSettings.ShopTrainBoostTurns}ターン",
                TrainingSettings.ShopTrainBoostPrice,
                TrainingShopItemType.TrainEfficiency,
                default,
                0,
                TrainingSettings.ShopTrainBoostPercent,
                TrainingSettings.ShopTrainBoostTurns),
            new TrainingShopItem(
                "train_boost_strong",
                "カクリツン改",
                $"大成功率+{TrainingSettings.ShopTrainBoostStrongPercent:0}%を"
                    + $"{TrainingSettings.ShopTrainBoostStrongTurns}ターン",
                TrainingSettings.ShopTrainBoostStrongPrice,
                TrainingShopItemType.TrainEfficiency,
                default,
                0,
                TrainingSettings.ShopTrainBoostStrongPercent,
                TrainingSettings.ShopTrainBoostStrongTurns),
            new TrainingShopItem(
                "motivation_tennis",
                "テニスボール",
                $"やる気が{TrainingSettings.ShopTennisBallGain}段階上がる",
                TrainingSettings.ShopTennisBallPrice,
                TrainingShopItemType.MotivationBoost,
                default,
                0,
                0f,
                0,
                TrainingSettings.ShopTennisBallGain),
            new TrainingShopItem(
                "motivation_boost",
                "サッカーボール",
                $"やる気が{TrainingSettings.ShopSoccerBallGain}段階上がる",
                TrainingSettings.ShopSoccerBallPrice,
                TrainingShopItemType.MotivationBoost,
                default,
                0,
                0f,
                0,
                TrainingSettings.ShopSoccerBallGain)
        };

        /// <summary>
        /// 商品のローカライズ表示名を返す
        /// </summary>
        /// <param name="item">商品</param>
        public static string GetLocalizedName(TrainingShopItem item)
        {
            return LocalizedText.GetOrFallback(
                GameTextKeys.TrainingShopName(item.Id),
                item.DisplayName);
        }

        /// <summary>
        /// 商品のローカライズ説明を返す
        /// </summary>
        /// <param name="item">商品</param>
        public static string GetLocalizedDescription(TrainingShopItem item)
        {
            if (item.GreatSuccessBonusPercent > 0f && item.GreatSuccessBonusWeeks > 0)
            {
                return LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingShopDesc(item.Id),
                    item.Description,
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "percent", Mathf.RoundToInt(item.GreatSuccessBonusPercent) },
                        { "turns", item.GreatSuccessBonusWeeks },
                    });
            }

            return LocalizedText.GetOrFallback(
                GameTextKeys.TrainingShopDesc(item.Id),
                item.Description);
        }

        /// <summary>
        /// 1ページ分の商品を返す
        /// </summary>
        /// <param name="pageIndex">ページ番号(0始まり)</param>
        /// <param name="pageSize">ページサイズ</param>
        public static List<TrainingShopItem> GetPage(int pageIndex, int pageSize)
        {
            var page = new List<TrainingShopItem>(pageSize);
            if (pageSize <= 0 || AllItems.Count == 0)
            {
                return page;
            }

            int start = pageIndex * pageSize;
            if (start < 0 || start >= AllItems.Count)
            {
                return page;
            }

            int end = System.Math.Min(start + pageSize, AllItems.Count);
            for (int i = start; i < end; i++)
            {
                page.Add(AllItems[i]);
            }

            return page;
        }

        /// <summary>
        /// 総ページ数を返す
        /// </summary>
        /// <param name="pageSize">ページサイズ</param>
        public static int GetPageCount(int pageSize)
        {
            if (pageSize <= 0)
            {
                return 0;
            }

            return (AllItems.Count + pageSize - 1) / pageSize;
        }

        /// <summary>
        /// 商品IDから定義を取得する
        /// </summary>
        /// <param name="itemId">商品ID</param>
        /// <param name="item">商品</param>
        public static bool TryGetById(string itemId, out TrainingShopItem item)
        {
            item = default;
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            for (int i = 0; i < AllItems.Count; i++)
            {
                if (AllItems[i].Id != itemId)
                {
                    continue;
                }

                item = AllItems[i];
                return true;
            }

            return false;
        }

        /// <summary>
        /// 売店陳列用に重複なしで商品を抽選する
        /// </summary>
        /// <param name="count">陳列数</param>
        /// <param name="random">乱数</param>
        public static List<string> RollOfferItemIds(int count, System.Random random)
        {
            int offerCount = System.Math.Max(0, count);
            var offer = new List<string>(offerCount);
            if (random == null || AllItems.Count == 0 || offerCount == 0)
            {
                return offer;
            }

            var pool = new List<TrainingShopItem>(AllItems.Count);
            for (int i = 0; i < AllItems.Count; i++)
            {
                pool.Add(AllItems[i]);
            }

            int take = System.Math.Min(offerCount, pool.Count);
            for (int i = 0; i < take; i++)
            {
                int pick = random.Next(i, pool.Count);
                TrainingShopItem swapped = pool[i];
                pool[i] = pool[pick];
                pool[pick] = swapped;
                offer.Add(pool[i].Id);
            }

            return offer;
        }

        /// <summary>
        /// 商品ID一覧を商品定義へ解決する
        /// </summary>
        /// <param name="itemIds">商品ID</param>
        public static List<TrainingShopItem> ResolveItems(IReadOnlyList<string> itemIds)
        {
            var items = new List<TrainingShopItem>();
            if (itemIds == null)
            {
                return items;
            }

            for (int i = 0; i < itemIds.Count; i++)
            {
                if (!TryGetById(itemIds[i], out TrainingShopItem item))
                {
                    continue;
                }

                items.Add(item);
            }

            return items;
        }
    }
}
