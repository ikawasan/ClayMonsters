using System.Collections.Generic;
using System.Text;
using Battle;
using ClayEditor.Rigging;
using Localization;
using SaveData;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// セーブプレビューとセーブデータ表示用のパラメータ・攻撃テキストを組み立てる
    /// </summary>
    public static class ModelSaveSummaryFormatter
    {
        /// <summary>
        /// セーブスロットの内容を詳細表示用テキストに整形する
        /// </summary>
        public static string FormatSlotDetail(ModelSaveSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.modelName))
            {
                return string.Empty;
            }

            BattleStatusBalance.Normalize(
                slot.status,
                out int hp,
                out int attack,
                out int defense,
                out int speed,
                out int hit);
            List<MotionType> attacks = CollectAttackMotions(slot.attackMotions);
            return FormatDetail(hp, attack, defense, speed, hit, attacks, false);
        }

        /// <summary>
        /// 保存前プレビューを詳細表示用テキストに整形する
        /// </summary>
        public static string FormatPreviewDetail(
            ModelStatus status,
            IReadOnlyList<MotionType> registeredAttackMotions)
        {
            BattleStatusBalance.Normalize(
                status,
                out int hp,
                out int attack,
                out int defense,
                out int speed,
                out int hit);
            List<MotionType> attacks = CollectAttackMotions(registeredAttackMotions);
            return FormatDetail(hp, attack, defense, speed, hit, attacks, true);
        }

        /// <summary>
        /// セーブスロットの内容をスロット一覧向けの1行テキストに整形する
        /// </summary>
        public static string FormatSlotCompact(ModelSaveSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.modelName))
            {
                return string.Empty;
            }

            BattleStatusBalance.Normalize(
                slot.status,
                out int hp,
                out int attack,
                out int defense,
                out int speed,
                out int hit);
            string attacks = FormatAttacksCompact(CollectAttackMotions(slot.attackMotions));
            return LocalizedText.GetOrFallback(
                GameTextKeys.SaveSummaryStatsCompact,
                "HP{hp} 攻撃{atk} 防御{def} 速度{spd} 命中{hit} / {attacks}",
                new Dictionary<string, object>
                {
                    { "hp", hp },
                    { "atk", attack },
                    { "def", defense },
                    { "spd", speed },
                    { "hit", hit },
                    { "attacks", attacks },
                });
        }

        /// <summary>
        /// セーブスロットの内容をスロット一覧向けのパラメータ行に整形する
        /// </summary>
        public static string FormatSlotListParameters(ModelSaveSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.modelName))
            {
                return string.Empty;
            }

            return FormatStatusParameters(slot.status);
        }

        /// <summary>
        /// ステータスをスロット一覧向けのパラメータ行に整形する
        /// </summary>
        public static string FormatStatusParameters(ModelStatus status)
        {
            BattleStatusBalance.Normalize(
                status,
                out int hp,
                out int attack,
                out int defense,
                out int speed,
                out int hit);
            return FormatStatsBlock(hp, attack, defense, speed, hit);
        }

        /// <summary>
        /// 育成中ステータスを戦闘上限内で整形する
        /// 作成時上限ではなくBattleStatusBalanceの上限を使う
        /// </summary>
        public static string FormatTrainingStatusParameters(ModelStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            return FormatStatusParameters(status);
        }

        /// <summary>
        /// ステータスを保存確認画面向けの縦並びパラメータ行に整形する
        /// </summary>
        public static string FormatConfirmStatusParameters(ModelStatus status)
        {
            return FormatStatusParameters(status);
        }

        /// <summary>
        /// 育成完了画面と同じ最終ステータス表示を返す
        /// </summary>
        public static string FormatTrainingFinalStatusParameters(ModelStatus status)
        {
            return FormatTrainingStatusParameters(status);
        }

        /// <summary>
        /// 育成完了画面と同じ範囲ラベルを返す
        /// </summary>
        public static string TrainingAttackRangeLabel =>
            LocalizedText.GetOrFallback(GameTextKeys.SaveSummaryRangeLabel, "範囲:");

        /// <summary>
        /// 育成完了画面と同じダメージ表示を返す
        /// </summary>
        public static string FormatTrainingAttackDamageText(MotionType motion)
        {
            return LocalizedText.GetOrFallback(GameTextKeys.SaveSummaryDamage, "ダメージ: ")
                + MotionPartRequirement.GetPowerDisplayValue(motion);
        }

        /// <summary>
        /// 育成完了画面と同じコスト表示を返す
        /// </summary>
        public static string FormatTrainingAttackCostText(MotionType motion)
        {
            return LocalizedText.GetOrFallback(GameTextKeys.SaveSummaryCostLabel, "コスト: ")
                + MotionPartRequirement.GetGutsCostDisplayValue(motion);
        }

        /// <summary>
        /// 育成完了画面と同じ数値情報表示を返す
        /// </summary>
        public static string FormatTrainingAttackStatsText(MotionType motion)
        {
            return FormatTrainingAttackDamageText(motion)
                + "\n"
                + FormatTrainingAttackCostText(motion);
        }

        /// <summary>
        /// セーブスロットの内容をスロット一覧向けの複数行テキストに整形する
        /// </summary>
        public static string FormatSlotListDetail(ModelSaveSlot slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.modelName))
            {
                return string.Empty;
            }

            string parameters = FormatSlotListParameters(slot);
            string attacks = FormatAttacksDetail(CollectAttackMotions(slot.attackMotions));
            return parameters + "\n" + attacks;
        }

        private static string FormatStatsBlock(int hp, int attack, int defense, int speed, int hit)
        {
            return LocalizedText.GetOrFallback(
                GameTextKeys.SaveSummaryStatsBlock,
                "HP {hp}\n攻撃 {atk}\n防御 {def}\n速度 {spd}\n命中 {hit}",
                new Dictionary<string, object>
                {
                    { "hp", hp },
                    { "atk", attack },
                    { "def", defense },
                    { "spd", speed },
                    { "hit", hit },
                });
        }

        private static string FormatDetail(
            int hp,
            int attack,
            int defense,
            int speed,
            int hit,
            IReadOnlyList<MotionType> attacks,
            bool isPreSavePreview)
        {
            var builder = new StringBuilder(256);
            builder.AppendLine(
                LocalizedText.GetOrFallback(GameTextKeys.SaveSummaryParams, "【パラメータ】"));
            builder.Append(
                LocalizedText.GetOrFallback(
                    GameTextKeys.SaveSummaryDetailStatLine,
                    "HP {hp} / 攻撃 {atk} / 防御 {def} / 速度 {spd} / 命中 {hit}",
                    new Dictionary<string, object>
                    {
                        { "hp", hp },
                        { "atk", attack },
                        { "def", defense },
                        { "spd", speed },
                        { "hit", hit },
                    }));

            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(
                isPreSavePreview
                    ? LocalizedText.GetOrFallback(
                        GameTextKeys.SaveSummaryRegisteredAttacks,
                        "【登録される攻撃】")
                    : LocalizedText.GetOrFallback(GameTextKeys.SaveSummaryAttacks, "【攻撃】"));
            builder.Append(FormatAttacksDetail(attacks));

            return builder.ToString().TrimEnd();
        }

        private static string FormatAttacksDetail(IReadOnlyList<MotionType> attacks)
        {
            if (attacks == null || attacks.Count == 0)
            {
                return LocalizedText.GetOrFallback(GameTextKeys.SaveSummaryNone, "なし");
            }

            var builder = new StringBuilder();
            for (int i = 0; i < attacks.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('、');
                }

                MotionType motion = attacks[i];
                builder.Append(MotionPartRequirement.FormatDisplayNameWithStrengthRank(motion));
                builder.Append(
                    LocalizedText.GetOrFallback(
                        GameTextKeys.SaveSummaryAttackMeta,
                        "({part}/{range}/威力{power}/コスト{cost})",
                        new Dictionary<string, object>
                        {
                            { "part", MotionPartRequirement.FormatTargetDestroyPartLabel(motion) },
                            { "range", MotionPartRequirement.FormatRangeLabel(motion) },
                            { "power", MotionPartRequirement.GetPowerDisplayValue(motion) },
                            { "cost", MotionPartRequirement.GetGutsCostDisplayValue(motion) },
                        }));
            }

            return builder.ToString();
        }

        private static string FormatAttacksCompact(IReadOnlyList<MotionType> attacks)
        {
            if (attacks == null || attacks.Count == 0)
            {
                return LocalizedText.GetOrFallback(GameTextKeys.SaveSummaryNoAttacks, "攻撃なし");
            }

            string firstAttack = MotionPartRequirement.FormatDisplayNameWithStrengthRank(attacks[0]);
            string firstTargetPart = MotionPartRequirement.FormatTargetDestroyPartLabel(attacks[0]);
            if (attacks.Count == 1)
            {
                return firstAttack + firstTargetPart;
            }

            return firstAttack
                + firstTargetPart
                + LocalizedText.GetOrFallback(
                    GameTextKeys.SaveSummaryAndMore,
                    " 他{count}",
                    "count",
                    attacks.Count - 1);
        }

        private static List<MotionType> CollectAttackMotions(IReadOnlyList<MotionType> motions)
        {
            var attacks = new List<MotionType>();
            if (motions == null)
            {
                return attacks;
            }

            for (int i = 0; i < motions.Count; i++)
            {
                MotionType motion = motions[i];
                if (ProceduralMotionCharacter.IsAttackMotion(motion) && !attacks.Contains(motion))
                {
                    attacks.Add(motion);
                }
            }

            return attacks;
        }
    }
}
