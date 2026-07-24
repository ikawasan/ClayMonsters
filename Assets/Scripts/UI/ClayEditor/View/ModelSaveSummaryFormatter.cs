using System.Collections.Generic;
using System.Text;
using Battle;
using ClayEditor.Rigging;
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
            return $"HP{hp} 攻撃{attack} 防御{defense} 速度{speed} 命中{hit} / {attacks}";
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
            return $"HP {hp}\n攻撃 {attack}\n防御 {defense}\n速度 {speed}\n命中 {hit}";
        }

        /// <summary>
        /// 育成中の実ステータスをそのまま整形する
        /// 作成時上限の丸めは掛けない
        /// </summary>
        public static string FormatTrainingStatusParameters(ModelStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            return $"HP {status.hp}\n攻撃 {status.attack}\n防御 {status.defense}\n速度 {status.speed}\n命中 {status.hit}";
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
        public const string TrainingAttackRangeLabel = "範囲:";

        /// <summary>
        /// 育成完了画面と同じダメージ表示を返す
        /// </summary>
        public static string FormatTrainingAttackDamageText(MotionType motion)
        {
            return "ダメージ: " + MotionPartRequirement.GetPowerDisplayValue(motion);
        }

        /// <summary>
        /// 育成完了画面と同じコスト表示を返す
        /// </summary>
        public static string FormatTrainingAttackCostText(MotionType motion)
        {
            return "コスト: " + MotionPartRequirement.GetGutsCostDisplayValue(motion);
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
            builder.AppendLine("【パラメータ】");
            builder.Append("HP ").Append(hp)
                .Append(" / 攻撃 ").Append(attack)
                .Append(" / 防御 ").Append(defense)
                .Append(" / 速度 ").Append(speed)
                .Append(" / 命中 ").Append(hit);

            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(isPreSavePreview ? "【登録される攻撃】" : "【攻撃】");
            builder.Append(FormatAttacksDetail(attacks));

            return builder.ToString().TrimEnd();
        }

        private static string FormatAttacksDetail(IReadOnlyList<MotionType> attacks)
        {
            if (attacks == null || attacks.Count == 0)
            {
                return "なし";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < attacks.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('、');
                }

                MotionType motion = attacks[i];
                builder.Append(MotionPartRequirement.GetDisplayName(motion));
                builder.Append('(').Append(MotionPartRequirement.FormatTargetDestroyPartLabel(motion));
                builder.Append('/').Append(MotionPartRequirement.FormatRangeLabel(motion));
                builder.Append("/威力").Append(MotionPartRequirement.GetPowerDisplayValue(motion));
                builder.Append("/コスト").Append(MotionPartRequirement.GetGutsCostDisplayValue(motion)).Append(')');
            }

            return builder.ToString();
        }

        private static string FormatAttacksCompact(IReadOnlyList<MotionType> attacks)
        {
            if (attacks == null || attacks.Count == 0)
            {
                return "攻撃なし";
            }

            string firstAttack = MotionPartRequirement.GetDisplayName(attacks[0]);
            string firstTargetPart = MotionPartRequirement.FormatTargetDestroyPartLabel(attacks[0]);
            if (attacks.Count == 1)
            {
                return firstAttack + firstTargetPart;
            }

            return firstAttack + firstTargetPart + " 他" + (attacks.Count - 1);
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
