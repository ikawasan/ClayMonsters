using UnityEngine;

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
                return new TrainingShopPurchaseResult(false, "セッションがありません", item);
            }

            if (string.IsNullOrEmpty(item.Id))
            {
                return new TrainingShopPurchaseResult(false, "商品がありません", item);
            }

            if (session.Money < item.Price)
            {
                return new TrainingShopPurchaseResult(
                    false,
                    $"所持金が足りない({session.Money}G/{item.Price}G)",
                    item);
            }

            if (!session.TrySpendMoney(item.Price))
            {
                return new TrainingShopPurchaseResult(
                    false,
                    $"所持金が足りない({session.Money}G/{item.Price}G)",
                    item);
            }

            session.AddInventoryItem(item.Id);
            return new TrainingShopPurchaseResult(
                true,
                $"{item.DisplayName}を購入した\n所持へ追加\n残り{session.Money}G",
                item);
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
                return new TrainingItemUseResult(false, "セッションがありません", default);
            }

            if (!TrainingShopCatalog.TryGetById(itemId, out TrainingShopItem item))
            {
                return new TrainingItemUseResult(false, "アイテムが見つからない", default);
            }

            if (!session.TryConsumeInventoryItem(item.Id))
            {
                return new TrainingItemUseResult(
                    false,
                    $"{item.DisplayName}を持っていない",
                    item);
            }

            ApplyItemEffect(session, item);
            return new TrainingItemUseResult(
                true,
                $"{item.DisplayName}を使った",
                item);
        }

        /// <summary>
        /// 大会勝利時の賞金を返す
        /// </summary>
        /// <param name="week">現在週</param>
        public static int ResolveTournamentReward(int week)
        {
            int safeWeek = Mathf.Max(1, week);
            return TrainingSettings.TournamentRewardBase
                + safeWeek * TrainingSettings.TournamentRewardPerWeek;
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
            }
        }
    }
}
