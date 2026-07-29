using ClayEditor.Rigging;
using SaveData;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 1回の行動結果
    /// </summary>
    public readonly struct TrainingActionResult
    {
        /// <summary>
        /// 行動結果を生成する
        /// </summary>
        public TrainingActionResult(
            TrainingCommandType command,
            TrainingFocus focus,
            TrainingLocation presentationLocation,
            bool succeeded,
            bool failedByLowStamina,
            bool isGreatSuccess,
            int staminaBefore,
            int staminaAfter,
            TrainingStatGain appliedGain,
            string foundItemId = null,
            int motivationGain = 0)
        {
            Command = command;
            Focus = focus;
            PresentationLocation = presentationLocation;
            Location = presentationLocation;
            Succeeded = succeeded;
            FailedByLowStamina = failedByLowStamina;
            IsRestAction = command == TrainingCommandType.Rest;
            IsGreatSuccess = isGreatSuccess;
            StaminaBefore = staminaBefore;
            StaminaAfter = staminaAfter;
            AppliedGain = appliedGain;
            FoundItemId = foundItemId;
            MotivationGain = Mathf.Max(0, motivationGain);
        }

        /// <summary>
        /// 実行したコマンド
        /// </summary>
        public TrainingCommandType Command { get; }

        /// <summary>
        /// 訓練系の主ステ
        /// </summary>
        public TrainingFocus Focus { get; }

        /// <summary>
        /// 背景用の行き先
        /// </summary>
        public TrainingLocation PresentationLocation { get; }

        /// <summary>
        /// 互換用の行き先
        /// </summary>
        public TrainingLocation Location { get; }

        /// <summary>
        /// 行動が成功したか
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 低体力による失敗か
        /// </summary>
        public bool FailedByLowStamina { get; }

        /// <summary>
        /// 休憩行動か
        /// </summary>
        public bool IsRestAction { get; }

        /// <summary>
        /// 大成功だったか
        /// </summary>
        public bool IsGreatSuccess { get; }

        /// <summary>
        /// 行動前体力
        /// </summary>
        public int StaminaBefore { get; }

        /// <summary>
        /// 行動後体力
        /// </summary>
        public int StaminaAfter { get; }

        /// <summary>
        /// 適用されたステータス上昇
        /// </summary>
        public TrainingStatGain AppliedGain { get; }

        /// <summary>
        /// 訓練中に拾ったアイテムID
        /// </summary>
        public string FoundItemId { get; }

        /// <summary>
        /// 休憩大成功などで上がるやる気段階数
        /// </summary>
        public int MotivationGain { get; }
    }

    /// <summary>
    /// 育成セッションの進行状態
    /// </summary>
    public sealed class TrainingSession
    {
        private readonly List<TrainingInventoryEntry> inventory =
            new List<TrainingInventoryEntry>();
        private readonly List<string> shopOfferItemIds = new List<string>();
        private TrainingFocus[] offeredFocuses;
        private int offeredFocusesTurnKey = int.MinValue;

        /// <summary>
        /// 育成対象スロット番号
        /// </summary>
        public int PlayerSlotIndex { get; }

        /// <summary>
        /// 現在曜日(月〜金)
        /// </summary>
        public TrainingDayOfWeek CurrentDay { get; private set; }

        /// <summary>
        /// 互換用の週番号表現(1始まりの曜日番号)
        /// </summary>
        public int CurrentWeek => (int)CurrentDay;

        /// <summary>
        /// 互換用のターン位置
        /// </summary>
        public int TurnIndexInDay { get; private set; }

        /// <summary>
        /// 行動体力
        /// </summary>
        public int Stamina { get; private set; }

        /// <summary>
        /// やる気
        /// </summary>
        public TrainingMotivation Motivation { get; private set; }

        /// <summary>
        /// 所持金
        /// </summary>
        public int Money { get; private set; }

        /// <summary>
        /// 現在のステータス
        /// </summary>
        public ModelStatus CurrentStatus { get; }

        /// <summary>
        /// 現在の攻撃構成
        /// </summary>
        public List<MotionType> AttackMotions { get; }

        /// <summary>
        /// 訓練大成功率の加算(百分率)
        /// </summary>
        public float TrainGreatSuccessBonusPercent { get; private set; }

        /// <summary>
        /// 訓練大成功ボーナスの残り週数
        /// </summary>
        public int TrainGreatSuccessBonusWeeks { get; private set; }

        /// <summary>
        /// 所持アイテム一覧
        /// </summary>
        public IReadOnlyList<TrainingInventoryEntry> Inventory => inventory;

        /// <summary>
        /// 売店に並んでいる商品ID
        /// </summary>
        public IReadOnlyList<string> ShopOfferItemIds => shopOfferItemIds;

        /// <summary>
        /// 育成完了済みか
        /// </summary>
        public bool IsCompleted { get; private set; }

        private float statusGainMultiplier = 1f;

        /// <summary>
        /// 育成完了状態へ遷移する
        /// </summary>
        public void MarkCompleted()
        {
            IsCompleted = true;
        }

        /// <summary>
        /// ステータス上昇全体に掛ける倍率を設定する
        /// </summary>
        /// <param name="multiplier">倍率</param>
        public void SetStatusGainMultiplier(float multiplier)
        {
            statusGainMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>
        /// セッションを生成する
        /// </summary>
        /// <param name="playerSlotIndex">対象スロット</param>
        /// <param name="baseStatus">開始時ステータス</param>
        /// <param name="attackMotions">開始時攻撃</param>
        public TrainingSession(
            int playerSlotIndex,
            ModelStatus baseStatus,
            IReadOnlyList<MotionType> attackMotions)
        {
            PlayerSlotIndex = playerSlotIndex;
            CurrentDay = TrainingDayOfWeek.Monday;
            TurnIndexInDay = 0;
            Stamina = TrainingSettings.MaxStamina;
            Motivation = TrainingSettings.StartingMotivation;
            Money = TrainingSettings.StartingMoney;
            CurrentStatus = ModelStatus.CloneOrDefault(baseStatus);
            AttackMotions = ModelAttackMotionUtility.Normalize(
                attackMotions,
                TrainingSettings.AttackSlotCount);
        }

        /// <summary>
        /// 保存データから育成セッションを復元する
        /// </summary>
        /// <param name="playerSlotIndex">対象スロット</param>
        /// <param name="progress">育成途中データ</param>
        /// <returns>復元したセッション</returns>
        public static TrainingSession Resume(int playerSlotIndex, TrainingSlotProgress progress)
        {
            var session = new TrainingSession(
                playerSlotIndex,
                progress.status,
                progress.attackMotions);
            session.CurrentDay = (TrainingDayOfWeek)Mathf.Clamp(
                progress.day,
                (int)TrainingDayOfWeek.Monday,
                TrainingSettings.TotalDays);
            session.TurnIndexInDay = Mathf.Clamp(
                progress.turnIndexInDay,
                0,
                TrainingDailySchedule.TurnsPerDay);
            session.Stamina = Mathf.Clamp(progress.stamina, 0, TrainingSettings.MaxStamina);
            session.Motivation = TrainingMotivationCatalog.Clamp(progress.motivation);
            session.Money = Mathf.Max(0, progress.money);
            session.TrainGreatSuccessBonusPercent =
                Mathf.Max(0f, progress.trainGreatSuccessBonusPercent);
            session.TrainGreatSuccessBonusWeeks =
                Mathf.Max(0, progress.trainGreatSuccessBonusWeeks);
            session.LoadInventory(progress.inventory);
            session.LoadShopOffer(progress.shopOfferItemIds);
            return session;
        }

        /// <summary>
        /// 売店陳列をランダムに入れ替える
        /// 放課後など陳列更新タイミングでのみ呼ぶ
        /// </summary>
        /// <param name="random">乱数</param>
        public void RefreshShopOffer(System.Random random)
        {
            shopOfferItemIds.Clear();
            List<string> rolled = TrainingShopCatalog.RollOfferItemIds(
                TrainingSettings.ShopPageSize,
                random);
            for (int i = 0; i < rolled.Count; i++)
            {
                shopOfferItemIds.Add(rolled[i]);
            }
        }

        /// <summary>
        /// 売店陳列が空なら抽選する
        /// </summary>
        /// <param name="random">乱数</param>
        public void EnsureShopOffer(System.Random random)
        {
            if (shopOfferItemIds.Count > 0)
            {
                return;
            }

            RefreshShopOffer(random);
        }

        /// <summary>
        /// 購入した商品を売り切れ枠にする
        /// 同じ来店中は空枠へ補充せず位置も詰めない
        /// </summary>
        /// <param name="itemId">商品ID</param>
        public bool TryRemoveShopOfferItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            for (int i = 0; i < shopOfferItemIds.Count; i++)
            {
                if (shopOfferItemIds[i] != itemId)
                {
                    continue;
                }

                shopOfferItemIds[i] = string.Empty;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 現在の売店陳列を返す
        /// 売り切れ枠は空IDの要素として位置を保つ
        /// </summary>
        public List<TrainingShopItem> GetShopOfferItems()
        {
            var items = new List<TrainingShopItem>(shopOfferItemIds.Count);
            for (int i = 0; i < shopOfferItemIds.Count; i++)
            {
                string itemId = shopOfferItemIds[i];
                if (string.IsNullOrEmpty(itemId)
                    || !TrainingShopCatalog.TryGetById(itemId, out TrainingShopItem item))
                {
                    items.Add(default);
                    continue;
                }

                items.Add(item);
            }

            return items;
        }

        /// <summary>
        /// 売店陳列を保存用に複製する
        /// </summary>
        public List<string> CloneShopOfferForSave()
        {
            return new List<string>(shopOfferItemIds);
        }

        private void LoadShopOffer(List<string> source)
        {
            shopOfferItemIds.Clear();
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count && shopOfferItemIds.Count < TrainingSettings.ShopPageSize; i++)
            {
                string itemId = source[i];
                if (string.IsNullOrEmpty(itemId)
                    || !TrainingShopCatalog.TryGetById(itemId, out _))
                {
                    // 売り切れや不正IDも枠位置を保つ
                    shopOfferItemIds.Add(string.Empty);
                    continue;
                }

                bool duplicate = false;
                for (int j = 0; j < shopOfferItemIds.Count; j++)
                {
                    if (shopOfferItemIds[j] == itemId)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    shopOfferItemIds.Add(string.Empty);
                    continue;
                }

                shopOfferItemIds.Add(itemId);
            }
        }

        /// <summary>
        /// アイテムを1個追加する
        /// </summary>
        /// <param name="itemId">商品ID</param>
        public void AddInventoryItem(string itemId)
        {
            AddInventoryItem(itemId, 1);
        }

        /// <summary>
        /// アイテムを指定個数追加する
        /// </summary>
        /// <param name="itemId">商品ID</param>
        /// <param name="count">追加個数</param>
        public void AddInventoryItem(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0)
            {
                return;
            }

            for (int i = 0; i < inventory.Count; i++)
            {
                TrainingInventoryEntry entry = inventory[i];
                if (entry == null || entry.itemId != itemId)
                {
                    continue;
                }

                entry.count = Mathf.Max(0, entry.count) + count;
                return;
            }

            inventory.Add(new TrainingInventoryEntry
            {
                itemId = itemId,
                count = count
            });
        }

        /// <summary>
        /// アイテムを1個消費する
        /// </summary>
        /// <param name="itemId">商品ID</param>
        public bool TryConsumeInventoryItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            for (int i = 0; i < inventory.Count; i++)
            {
                TrainingInventoryEntry entry = inventory[i];
                if (entry == null || entry.itemId != itemId || entry.count <= 0)
                {
                    continue;
                }

                entry.count--;
                if (entry.count <= 0)
                {
                    inventory.RemoveAt(i);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 所持アイテムの表示一覧を返す
        /// </summary>
        public List<TrainingInventoryEntryView> BuildInventoryViews()
        {
            var views = new List<TrainingInventoryEntryView>(inventory.Count);
            for (int i = 0; i < inventory.Count; i++)
            {
                TrainingInventoryEntry entry = inventory[i];
                if (entry == null
                    || entry.count <= 0
                    || !TrainingShopCatalog.TryGetById(entry.itemId, out TrainingShopItem item))
                {
                    continue;
                }

                views.Add(new TrainingInventoryEntryView(
                    item.Id,
                    item.DisplayName,
                    item.Description,
                    entry.count));
            }

            return views;
        }

        /// <summary>
        /// 所持アイテムを保存用に複製する
        /// </summary>
        public List<TrainingInventoryEntry> CloneInventoryForSave()
        {
            var clone = new List<TrainingInventoryEntry>(inventory.Count);
            for (int i = 0; i < inventory.Count; i++)
            {
                TrainingInventoryEntry entry = inventory[i];
                if (entry == null || string.IsNullOrEmpty(entry.itemId) || entry.count <= 0)
                {
                    continue;
                }

                clone.Add(new TrainingInventoryEntry
                {
                    itemId = entry.itemId,
                    count = entry.count
                });
            }

            return clone;
        }

        private void LoadInventory(List<TrainingInventoryEntry> source)
        {
            inventory.Clear();
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                TrainingInventoryEntry entry = source[i];
                if (entry == null
                    || string.IsNullOrEmpty(entry.itemId)
                    || entry.count <= 0
                    || !TrainingShopCatalog.TryGetById(entry.itemId, out _))
                {
                    continue;
                }

                inventory.Add(new TrainingInventoryEntry
                {
                    itemId = entry.itemId,
                    count = entry.count
                });
            }
        }

        /// <summary>
        /// 行動結果を適用する
        /// </summary>
        /// <param name="result">行動結果</param>
        public void ApplyAction(TrainingActionResult result)
        {
            Stamina = result.StaminaAfter;
            if (result.Succeeded)
            {
                ApplyGain(result.AppliedGain);
                if (!string.IsNullOrEmpty(result.FoundItemId))
                {
                    AddInventoryItem(result.FoundItemId);
                }

                if (result.MotivationGain > 0)
                {
                    RaiseMotivation(result.MotivationGain);
                }
            }
            else if (result.Command == TrainingCommandType.Train
                || result.Command == TrainingCommandType.SpecialTrain)
            {
                LowerMotivation(1);
            }

            CompletePeriod();
        }

        /// <summary>
        /// やる気を1段階以上下げる
        /// </summary>
        /// <param name="steps">下降段階数</param>
        public void LowerMotivation(int steps = 1)
        {
            if (steps <= 0)
            {
                return;
            }

            Motivation = TrainingMotivationCatalog.Clamp((int)Motivation - steps);
        }

        /// <summary>
        /// やる気を1段階以上上げる
        /// </summary>
        /// <param name="steps">上昇段階数</param>
        public void RaiseMotivation(int steps = 1)
        {
            if (steps <= 0)
            {
                return;
            }

            Motivation = TrainingMotivationCatalog.Clamp((int)Motivation + steps);
        }

        /// <summary>
        /// 所持金を加算する
        /// </summary>
        /// <param name="amount">加算額</param>
        public void AddMoney(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Money += amount;
        }

        /// <summary>
        /// 所持金を消費する
        /// </summary>
        /// <param name="amount">消費額</param>
        public bool TrySpendMoney(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (Money < amount)
            {
                return false;
            }

            Money -= amount;
            return true;
        }

        /// <summary>
        /// 体力を回復する
        /// </summary>
        /// <param name="amount">回復量</param>
        public void RecoverStamina(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Stamina = Mathf.Min(TrainingSettings.MaxStamina, Stamina + amount);
        }

        /// <summary>
        /// 訓練効率アップ効果を適用する
        /// </summary>
        /// <param name="bonusPercent">大成功率加算</param>
        /// <param name="weeks">継続週数</param>
        public void ApplyTrainEfficiencyBoost(float bonusPercent, int weeks)
        {
            if (bonusPercent <= 0f || weeks <= 0)
            {
                return;
            }

            if (bonusPercent >= TrainGreatSuccessBonusPercent)
            {
                TrainGreatSuccessBonusPercent = bonusPercent;
                TrainGreatSuccessBonusWeeks = weeks;
                return;
            }

            if (TrainGreatSuccessBonusWeeks <= 0)
            {
                TrainGreatSuccessBonusPercent = bonusPercent;
                TrainGreatSuccessBonusWeeks = weeks;
            }
        }

        /// <summary>
        /// 翌日へ進む
        /// </summary>
        public void AdvanceDay()
        {
            TurnIndexInDay = 0;
            if (TrainGreatSuccessBonusWeeks > 0)
            {
                TrainGreatSuccessBonusWeeks--;
                if (TrainGreatSuccessBonusWeeks <= 0)
                {
                    TrainGreatSuccessBonusPercent = 0f;
                }
            }

            if ((int)CurrentDay >= TrainingSettings.TotalDays)
            {
                IsCompleted = true;
                return;
            }

            CurrentDay = (TrainingDayOfWeek)((int)CurrentDay + 1);
        }

        /// <summary>
        /// 互換用の翌週進行
        /// </summary>
        public void AdvanceWeek()
        {
            AdvanceDay();
        }

        /// <summary>
        /// イベントのステータス上昇を適用する
        /// </summary>
        /// <param name="gain">上昇量</param>
        public void ApplyEventStatGain(TrainingStatGain gain)
        {
            ApplyGain(gain);
        }

        /// <summary>
        /// 指定スロットの攻撃を入れ替える
        /// </summary>
        /// <param name="slotIndex">攻撃スロット(0〜3)</param>
        /// <param name="newAttack">新しい攻撃</param>
        /// <returns>入れ替えに成功したか</returns>
        public bool TryReplaceAttack(int slotIndex, MotionType newAttack)
        {
            if (slotIndex < 0
                || slotIndex >= TrainingSettings.AttackSlotCount
                || slotIndex >= AttackMotions.Count
                || !ProceduralMotionCharacter.IsAttackMotion(newAttack))
            {
                return false;
            }

            AttackMotions[slotIndex] = newAttack;
            return true;
        }

        /// <summary>
        /// 空きスロットへ新しい攻撃を追加する
        /// </summary>
        /// <param name="newAttack">新しい攻撃</param>
        /// <returns>追加に成功したか</returns>
        public bool TryAddAttack(MotionType newAttack)
        {
            if (AttackMotions.Count >= TrainingSettings.AttackSlotCount
                || !ProceduralMotionCharacter.IsAttackMotion(newAttack)
                || AttackMotions.Contains(newAttack))
            {
                return false;
            }

            AttackMotions.Add(newAttack);
            return true;
        }

        /// <summary>
        /// 時間割スロットを1つ進める
        /// </summary>
        public void CompletePeriod()
        {
            TurnIndexInDay++;
        }

        /// <summary>
        /// 今ターンの訓練主ステ候補を返す
        /// ターンが変わっていれば再抽選する
        /// </summary>
        /// <param name="random">乱数</param>
        public IReadOnlyList<TrainingFocus> GetOrRollOfferedFocuses(System.Random random)
        {
            int turnKey = ((int)CurrentDay * 100) + TurnIndexInDay;
            if (offeredFocuses != null && offeredFocusesTurnKey == turnKey)
            {
                return offeredFocuses;
            }

            offeredFocuses = TrainingFocusCatalog.PickRandomFocuses(
                TrainingSettings.OfferedTrainFocusCount,
                random);
            offeredFocusesTurnKey = turnKey;
            return offeredFocuses;
        }

        /// <summary>
        /// 放課後勝利時の体力回復を適用する
        /// </summary>
        public void ApplyAfterSchoolVictoryRecovery()
        {
            Stamina = System.Math.Min(
                TrainingSettings.MaxStamina,
                Stamina + TrainingSettings.TournamentVictoryStaminaRecovery);
        }

        private void ApplyGain(TrainingStatGain gain)
        {
            TrainingStatGain applied = Mathf.Abs(statusGainMultiplier - 1f) < 0.0001f
                ? gain
                : TrainingFocusCatalog.ScaleGain(gain, statusGainMultiplier);
            CurrentStatus.hp += applied.Hp;
            CurrentStatus.attack += applied.Attack;
            CurrentStatus.defense += applied.Defense;
            CurrentStatus.speed += applied.Speed;
            CurrentStatus.hit += applied.Hit;
        }
    }
}
