using Battle;
using ClayEditor.Rigging;
using System;
using System.Collections.Generic;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 自動育成の行動方針
    /// </summary>
    public static class TrainingAutoPolicy
    {
        /// <summary>
        /// 授業時間のコマンドを選ぶ
        /// </summary>
        /// <param name="session">育成セッション</param>
        /// <param name="random">乱数</param>
        public static TrainingWeekChoice PickWeekChoice(TrainingSession session, System.Random random)
        {
            if (session == null)
            {
                return TrainingWeekChoice.Rest();
            }

            if (ShouldRest(session))
            {
                return TrainingWeekChoice.Rest();
            }

            TrainingFocus focus = PickBestFocus(session.GetOrRollOfferedFocuses(random));
            if (TrainingSchedule.IsSpecialTrainDay(session.CurrentDay)
                && session.Stamina >= TrainingFocusCatalog.GetSpecialTrainStaminaCost(focus))
            {
                return TrainingWeekChoice.FromFocus(
                    TrainingCommandType.SpecialTrain,
                    focus);
            }

            if (session.Stamina >= TrainingFocusCatalog.GetTrainStaminaCost(focus))
            {
                return TrainingWeekChoice.FromFocus(
                    TrainingCommandType.Train,
                    focus);
            }

            return TrainingWeekChoice.Rest();
        }

        /// <summary>
        /// 売店で買う商品を選ぶ
        /// 陳列中の商品から選ぶ
        /// </summary>
        /// <param name="session">育成セッション</param>
        public static bool TryPickShopItem(TrainingSession session, out TrainingShopItem item)
        {
            item = default;
            if (session == null)
            {
                return false;
            }

            List<TrainingShopItem> offer = session.GetShopOfferItems();
            if (offer.Count == 0)
            {
                return false;
            }

            if (session.Stamina <= TrainingSettings.TrainStaminaCost
                && TryFindInOffer(offer, TrainingShopItemType.StaminaRecover, out item))
            {
                return session.Money >= item.Price;
            }

            if (session.Stamina <= TrainingSettings.TrainStaminaCost
                && TryFindInOffer(offer, TrainingShopItemType.StaminaFullRecover, out item))
            {
                return session.Money >= item.Price;
            }

            if (session.Motivation < TrainingMotivation.Normal
                && TryFindInOffer(offer, TrainingShopItemType.MotivationBoost, out item))
            {
                return session.Money >= item.Price;
            }

            if (session.TrainGreatSuccessBonusWeeks <= 0
                && TryFindInOffer(offer, TrainingShopItemType.TrainEfficiency, out item))
            {
                return session.Money >= item.Price;
            }

            if (session.Money >= TrainingSettings.ShopStatBoostPrice
                && TryFindInOffer(offer, TrainingShopItemType.StatBoost, out item))
            {
                return session.Money >= item.Price;
            }

            for (int i = 0; i < offer.Count; i++)
            {
                if (session.Money >= offer[i].Price)
                {
                    item = offer[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 互換用の旧選択
        /// </summary>
        public static TrainingTurnChoice PickTurnChoice(
            TrainingSession session,
            IReadOnlyList<TrainingLocation> choices)
        {
            TrainingWeekChoice weekChoice = PickWeekChoice(session, null);
            if (weekChoice.Command == TrainingCommandType.Rest)
            {
                return TrainingTurnChoice.Rest();
            }

            TrainingLocation location =
                TrainingFocusCatalog.GetPresentationLocation(weekChoice.Focus);
            if (choices != null && choices.Count > 0)
            {
                location = choices[0];
                int bestScore = ScoreLocation(choices[0]);
                for (int i = 1; i < choices.Count; i++)
                {
                    int score = ScoreLocation(choices[i]);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        location = choices[i];
                    }
                }
            }

            return TrainingTurnChoice.FromLocation(location);
        }

        /// <summary>
        /// 習得技の入れ替え先スロットを選ぶ
        /// </summary>
        public static int PickAttackSwapSlot(TrainingSession session, MotionType learnedAttack)
        {
            if (session == null || session.AttackMotions == null || session.AttackMotions.Count == 0)
            {
                return -1;
            }

            int learnedPower = MotionPartRequirement.GetPowerDisplayValue(learnedAttack);
            int weakestSlot = -1;
            int weakestPower = int.MaxValue;
            for (int i = 0; i < session.AttackMotions.Count; i++)
            {
                int power = MotionPartRequirement.GetPowerDisplayValue(session.AttackMotions[i]);
                if (power >= weakestPower)
                {
                    continue;
                }

                weakestPower = power;
                weakestSlot = i;
            }

            if (weakestSlot < 0 || learnedPower <= weakestPower)
            {
                return -1;
            }

            return weakestSlot;
        }

        private static bool ShouldRest(TrainingSession session)
        {
            return session.Stamina < TrainingFocusCatalog.GetMinTrainStaminaCost();
        }

        private static bool TryFindInOffer(
            IReadOnlyList<TrainingShopItem> offer,
            TrainingShopItemType itemType,
            out TrainingShopItem item)
        {
            item = default;
            for (int i = 0; i < offer.Count; i++)
            {
                if (offer[i].ItemType != itemType)
                {
                    continue;
                }

                item = offer[i];
                return true;
            }

            return false;
        }

        private static TrainingFocus PickBestFocus(IReadOnlyList<TrainingFocus> focuses)
        {
            TrainingFocus best = TrainingFocus.Attack;
            int bestScore = int.MinValue;
            if (focuses == null || focuses.Count == 0)
            {
                focuses = TrainingFocusCatalog.AllFocuses;
            }

            for (int i = 0; i < focuses.Count; i++)
            {
                TrainingStatGain gain = TrainingFocusCatalog.GetBaseGain(focuses[i]);
                int score = gain.Hp + gain.Attack * 2 + gain.Defense + gain.Speed * 2 + gain.Hit * 2;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = focuses[i];
                }
            }

            return best;
        }

        private static int ScoreLocation(TrainingLocation location)
        {
            TrainingStatGain gain = TrainingLocationCatalog.GetBaseGain(location);
            return gain.Hp + gain.Attack * 2 + gain.Defense + gain.Speed * 2 + gain.Hit * 2;
        }
    }
}
