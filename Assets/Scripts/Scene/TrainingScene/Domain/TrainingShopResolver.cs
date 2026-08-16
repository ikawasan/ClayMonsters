using Localization;
using System;
using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 売店購入の解決結果
    /// </summary>
    public readonly struct TrainingShopPurchaseResult
    {
        /// <summary>
        /// 購入結果を生成する
        /// </summary>
        public TrainingShopPurchaseResult(
            bool succeeded,
            string message,
            TrainingShopItem item)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Item = item;
        }

        /// <summary>
        /// 購入できたか
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 結果メッセージ
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 対象商品
        /// </summary>
        public TrainingShopItem Item { get; }
    }

    /// <summary>
    /// 所持アイテム使用の解決結果
    /// </summary>
    public readonly struct TrainingItemUseResult
    {
        /// <summary>
        /// 使用結果を生成する
        /// </summary>
        public TrainingItemUseResult(
            bool succeeded,
            string message,
            TrainingShopItem item)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            Item = item;
        }

        /// <summary>
        /// 使用できたか
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 結果メッセージ
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 対象商品
        /// </summary>
        public TrainingShopItem Item { get; }
    }

    /// <summary>
    /// 売店陳列入れ替えの解決結果
    /// </summary>
    public readonly struct TrainingShopRefreshResult
    {
        /// <summary>
        /// 入れ替え結果を生成する
        /// </summary>
        public TrainingShopRefreshResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// 入れ替えできたか
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 結果メッセージ
        /// </summary>
        public string Message { get; }
    }

    /// <summary>
    /// 売店の購入と所持アイテム使用
    /// </summary>
    public static class TrainingShopResolver
    {
        /// <summary>
        /// 商品を購入して所持へ追加する
        /// </summary>
        /// <param name="session">育成セッション</param>
        /// <param name="item">商品</param>
        public static TrainingShopPurchaseResult TryPurchase(
            TrainingSession session,
            TrainingShopItem item)
        {
            if (session == null)
            {
                return new TrainingShopPurchaseResult(
                    false,
                    LocalizedText.GetOrFallback(GameTextKeys.TrainingShopNoSession, "セッションがありません"),
                    item);
            }

            if (string.IsNullOrEmpty(item.Id))
            {
                return new TrainingShopPurchaseResult(
                    false,
                    LocalizedText.GetOrFallback(GameTextKeys.TrainingShopNoItem, "商品がありません"),
                    item);
            }

            if (session.Money < item.Price)
            {
                return new TrainingShopPurchaseResult(
                    false,
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingShopNotEnoughMoney,
                        "所持金が足りない({have}G/{price}G)",
                        new Dictionary<string, object>
                        {
                            { "have", session.Money },
                            { "price", item.Price },
                        }),
                    item);
            }

            if (!session.TrySpendMoney(item.Price))
            {
                return new TrainingShopPurchaseResult(
                    false,
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingShopNotEnoughMoney,
                        "所持金が足りない({have}G/{price}G)",
                        new Dictionary<string, object>
                        {
                            { "have", session.Money },
                            { "price", item.Price },
                        }),
                    item);
            }

            session.AddInventoryItem(item.Id);
            // 購入した枠は売り切れにし同来店中は補充しない
            session.TryRemoveShopOfferItem(item.Id);
            return new TrainingShopPurchaseResult(
                true,
                LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingShopPurchased,
                    "{name}を購入した\n所持へ追加\n残り{money}G",
                    new Dictionary<string, object>
                    {
                        { "name", TrainingShopCatalog.GetLocalizedName(item) },
                        { "money", session.Money },
                    }),
                item);
        }

        /// <summary>
        /// 所持金を払って売店陳列を入れ替える
        /// </summary>
        /// <param name="session">育成セッション</param>
        /// <param name="random">乱数</param>
        public static TrainingShopRefreshResult TryRefreshOffer(
            TrainingSession session,
            Random random)
        {
            if (session == null)
            {
                return new TrainingShopRefreshResult(
                    false,
                    LocalizedText.GetOrFallback(GameTextKeys.TrainingShopNoSession, "セッションがありません"));
            }

            int price = TrainingSettings.ShopRefreshPrice;
            if (session.Money < price)
            {
                return new TrainingShopRefreshResult(
                    false,
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingShopNotEnoughMoney,
                        "所持金が足りない({have}G/{price}G)",
                        new Dictionary<string, object>
                        {
                            { "have", session.Money },
                            { "price", price },
                        }));
            }

            if (!session.TrySpendMoney(price))
            {
                return new TrainingShopRefreshResult(
                    false,
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingShopNotEnoughMoney,
                        "所持金が足りない({have}G/{price}G)",
                        new Dictionary<string, object>
                        {
                            { "have", session.Money },
                            { "price", price },
                        }));
            }

            session.RefreshShopOffer(random);
            return new TrainingShopRefreshResult(
                true,
                LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingShopRefreshed,
                    "商品を更新した\n残り{money}G",
                    new Dictionary<string, object>
                    {
                        { "money", session.Money },
                    }));
        }

        /// <summary>
        /// 所持アイテムを1個使用する
        /// </summary>
        /// <param name="session">育成セッション</param>
        /// <param name="itemId">商品ID</param>
        public static TrainingItemUseResult TryUseItem(
            TrainingSession session,
            string itemId)
        {
            if (session == null)
            {
                return new TrainingItemUseResult(
                    false,
                    LocalizedText.GetOrFallback(GameTextKeys.TrainingShopNoSession, "セッションがありません"),
                    default);
            }

            if (!TrainingShopCatalog.TryGetById(itemId, out TrainingShopItem item))
            {
                return new TrainingItemUseResult(
                    false,
                    LocalizedText.GetOrFallback(GameTextKeys.TrainingShopItemNotFound, "アイテムが見つからない"),
                    default);
            }

            if (!session.TryConsumeInventoryItem(item.Id))
            {
                return new TrainingItemUseResult(
                    false,
                    LocalizedText.GetOrFallback(
                        GameTextKeys.TrainingShopNotOwned,
                        "{name}を持っていない",
                        new Dictionary<string, object>
                        {
                            { "name", TrainingShopCatalog.GetLocalizedName(item) },
                        }),
                    item);
            }

            ApplyItemEffect(session, item);
            string message = item.ItemType == TrainingShopItemType.MotivationBoost
                ? LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingShopUsedMotivated,
                    "{name}を使った\nやる気が上昇した",
                    new Dictionary<string, object>
                    {
                        { "name", TrainingShopCatalog.GetLocalizedName(item) },
                    })
                : LocalizedText.GetOrFallback(
                    GameTextKeys.TrainingShopUsed,
                    "{name}を使った",
                    new Dictionary<string, object>
                    {
                        { "name", TrainingShopCatalog.GetLocalizedName(item) },
                    });
            return new TrainingItemUseResult(
                true,
                message,
                item);
        }

        /// <summary>
        /// 放課後戦闘勝利時の賞金を返す
        /// 最終日は賞金なしそれ以外は固定額
        /// </summary>
        /// <param name="week">現在週</param>
        public static int ResolveTournamentReward(int week)
        {
            if (week >= TrainingSettings.TotalDays)
            {
                return 0;
            }

            return TrainingSettings.TournamentRewardBase;
        }

        private static void ApplyItemEffect(TrainingSession session, TrainingShopItem item)
        {
            switch (item.ItemType)
            {
                case TrainingShopItemType.StaminaRecover:
                case TrainingShopItemType.StaminaFullRecover:
                    session.RecoverStamina(item.StaminaRecover);
                    break;
                case TrainingShopItemType.StatBoost:
                    session.ApplyEventStatGain(item.StatGain);
                    break;
                case TrainingShopItemType.TrainEfficiency:
                    session.ApplyTrainEfficiencyBoost(
                        item.GreatSuccessBonusPercent,
                        item.GreatSuccessBonusWeeks);
                    break;
                case TrainingShopItemType.MotivationBoost:
                    session.RaiseMotivation(item.MotivationGain);
                    break;
            }
        }
    }
}
