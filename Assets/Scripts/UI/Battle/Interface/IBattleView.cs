using System.Collections.Generic;
using R3;

namespace UI.Battle.Interface
{
    /// <summary>
    /// 1つの技ボタンの表示情報(名前・使用可否・間合い・ガッツ・威力・破壊対象部位識別子)
    /// </summary>
    public readonly struct MoveDisplay
    {
        public MoveDisplay(
            string name,
            bool usable,
            float rangeMin,
            float rangeMax,
            float gutsCost,
            float power,
            MoveTargetPartId targetPartId,
            MoveTargetPartId requiredPartId)
        {
            Name = name;
            Usable = usable;
            RangeMin = rangeMin;
            RangeMax = rangeMax;
            GutsCost = gutsCost;
            Power = power;
            TargetPartId = targetPartId;
            RequiredPartId = requiredPartId;
        }

        public string Name { get; }
        public bool Usable { get; }
        public float RangeMin { get; }
        public float RangeMax { get; }
        public float GutsCost { get; }
        public float Power { get; }
        public MoveTargetPartId TargetPartId { get; }
        public MoveTargetPartId RequiredPartId { get; }
    }

    /// <summary>
    /// リアルタイム戦闘UIの契約表示更新と・技選択の通知を行う
    /// </summary>
    public interface IBattleView
    {
        /// <summary>
        /// 技ボタンが押された(技番号)
        /// </summary>
        Observable<int> OnMoveSelected { get; }

        /// <summary>
        /// 戦闘開始時にユニット名を初期化する
        /// </summary>
        void InitializeUnits(string playerName, string enemyName);

        /// <summary>
        /// 戦闘中のキーボード移動入力を優先するためUIのキーボードナビゲーションを無効化する
        /// </summary>
        void PrepareForBattleInput();

        /// <summary>
        /// HPを更新する
        /// </summary>
        void SetHp(bool isPlayer, int currentHp, int maxHp);

        /// <summary>
        /// ガッツを更新する(ゲージと現在値/最大値テキスト)
        /// </summary>
        void SetGuts(bool isPlayer, float guts, float maxGuts);

        /// <summary>
        /// 間合いを更新する
        /// </summary>
        void SetDistance(float distance, float maxDistance, string bandName);

        /// <summary>
        /// 戦闘ヒントを更新する
        /// </summary>
        void SetCombatHint(string hint);

        /// <summary>
        /// 残り時間を更新する
        /// </summary>
        void SetTimeRemaining(float seconds);

        /// <summary>
        /// プレイヤーの技ボタンの表示(名前・使用可否など)を更新する
        /// </summary>
        void SetPlayerMoves(IReadOnlyList<MoveDisplay> moves);

        /// <summary>
        /// 敵の技一覧の表示(威力・破壊部位・間合い・ガッツ・使用可否)を更新する
        /// </summary>
        void SetEnemyMoves(IReadOnlyList<MoveDisplay> moves);

        /// <summary>
        /// 攻撃開始時などに自他の攻撃名表示を更新する
        /// </summary>
        /// <param name="isPlayer">プレイヤー側ならtrue</param>
        /// <param name="attackName">表示する攻撃名</param>
        void SetAttackName(bool isPlayer, string attackName);

        /// <summary>
        /// リザルト表示前に戦闘操作UIを止めてレイアウト負荷を下げる
        /// </summary>
        void PrepareForResultDisplay();

        /// <summary>
        /// 決着表示(勝者名引き分けはnull)
        /// </summary>
        void ShowResult(string winnerName);
    }
}