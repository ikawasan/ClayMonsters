namespace Scene.TrainingScene.Domain
{
    /// <summary>
    /// 育成モードの定数とバランス設定
    /// </summary>
    public static class TrainingSettings
    {
        /// <summary>
        /// 育成日数(月〜金)
        /// </summary>
        public const int TotalDays = 5;

        /// <summary>
        /// 1日のターン数
        /// </summary>
        public static int TurnsPerDay => TrainingDailySchedule.TurnsPerDay;

        /// <summary>
        /// 互換用の旧週数定数
        /// </summary>
        public const int TotalWeeks = TotalDays;

        /// <summary>
        /// 攻撃スロット数
        /// </summary>
        public const int AttackSlotCount = SaveData.ModelAttackMotionUtility.SlotCount;

        /// <summary>
        /// 行動体力の最大値
        /// </summary>
        public const int MaxStamina = 100;

        /// <summary>
        /// 開始時の所持金
        /// </summary>
        public const int StartingMoney = 100;

        /// <summary>
        /// 訓練の標準体力消費(互換用)
        /// 実際の消費は主ステごとにTrainingFocusCatalogが返す
        /// </summary>
        public const int TrainStaminaCost = 20;

        /// <summary>
        /// 1ターンに提示する訓練主ステ候補数
        /// </summary>
        public const int OfferedTrainFocusCount = 3;

        /// <summary>
        /// 特訓の標準体力消費(互換用)
        /// 実際の消費は主ステごとにTrainingFocusCatalogが返す
        /// </summary>
        public const int SpecialTrainStaminaCost = 40;

        /// <summary>
        /// 特訓の体力消費倍率(通常訓練比)
        /// </summary>
        public const int SpecialTrainStaminaCostMultiplier = 2;

        /// <summary>
        /// 互換用の旧行動消費
        /// </summary>
        public const int StaminaCostPerAction = TrainStaminaCost;

        /// <summary>
        /// 訓練大成功率(百分率)
        /// </summary>
        public const float TrainGreatSuccessPercent = 20f;

        /// <summary>
        /// 特訓大成功率(百分率)
        /// </summary>
        public const float SpecialTrainGreatSuccessPercent = 15f;

        /// <summary>
        /// 訓練大成功時の上昇倍率
        /// </summary>
        public const float TrainGreatSuccessMultiplier = 1.8f;

        /// <summary>
        /// 特訓成功時の上昇倍率
        /// </summary>
        public const float SpecialTrainSuccessMultiplier = 1.75f;

        /// <summary>
        /// 特訓大成功時の上昇倍率
        /// </summary>
        public const float SpecialTrainGreatSuccessMultiplier = 2.5f;

        /// <summary>
        /// 訓練成功時にアイテムを拾う確率(百分率)
        /// </summary>
        public const float TrainLuckyItemPercent = 5f;

        /// <summary>
        /// 休憩の基本体力回復
        /// </summary>
        public const int RestStaminaRecovery = 40;

        /// <summary>
        /// 休憩大成功時の体力回復
        /// </summary>
        public const int RestGreatSuccessRecovery = 80;

        /// <summary>
        /// 休憩大成功率(百分率)
        /// </summary>
        public const float RestGreatSuccessPercent = 20f;

        /// <summary>
        /// 訓練失敗判定のしきい値(必要消費未満)
        /// </summary>
        public const int LowStaminaThreshold = TrainStaminaCost;

        /// <summary>
        /// 低体力時の基礎失敗率(百分率)
        /// </summary>
        public const float BaseFailurePercentAtLowStamina = 35f;

        /// <summary>
        /// しきい値を下回る体力1につき加算する失敗率(百分率)
        /// </summary>
        public const float FailurePercentPerStaminaBelowThreshold = 3f;

        /// <summary>
        /// 大会勝利時の体力回復量
        /// </summary>
        public const int TournamentVictoryStaminaRecovery = 20;

        /// <summary>
        /// 互換用の旧放課後勝利回復
        /// </summary>
        public const int AfterSchoolVictoryStaminaRecovery = TournamentVictoryStaminaRecovery;

        /// <summary>
        /// 放課後戦闘勝利の固定賞金
        /// </summary>
        public const int TournamentRewardBase = 200;

        /// <summary>
        /// 大会勝利の週ごとの追加賞金
        /// </summary>
        public const int TournamentRewardPerWeek = 50;

        /// <summary>
        /// 売店ページサイズ
        /// </summary>
        public const int ShopPageSize = 3;

        /// <summary>
        /// 栄養ドリンク価格
        /// </summary>
        public const int ShopStaminaDrinkPrice = 80;

        /// <summary>
        /// 栄養ドリンク回復量
        /// </summary>
        public const int ShopStaminaDrinkRecover = 50;

        /// <summary>
        /// 完全回復薬価格
        /// </summary>
        public const int ShopStaminaFullPrice = 180;

        /// <summary>
        /// ステ強化剤価格
        /// </summary>
        public const int ShopStatBoostPrice = 220;

        /// <summary>
        /// カクリツン価格
        /// </summary>
        public const int ShopTrainBoostPrice = 250;

        /// <summary>
        /// カクリツンの大成功率加算
        /// </summary>
        public const float ShopTrainBoostPercent = 25f;

        /// <summary>
        /// カクリツンの継続週数
        /// </summary>
        public const int ShopTrainBoostWeeks = 4;

        /// <summary>
        /// カクリツン改価格
        /// </summary>
        public const int ShopTrainBoostStrongPrice = 480;

        /// <summary>
        /// カクリツン改の大成功率加算
        /// </summary>
        public const float ShopTrainBoostStrongPercent = 50f;

        /// <summary>
        /// カクリツン改の継続週数
        /// </summary>
        public const int ShopTrainBoostStrongWeeks = 2;

        /// <summary>
        /// やる気回復アイテム価格
        /// </summary>
        public const int ShopMotivationBoostPrice = 150;

        /// <summary>
        /// やる気回復アイテムの上昇段階数
        /// </summary>
        public const int ShopMotivationBoostGain = 1;

        /// <summary>
        /// 開始時のやる気
        /// </summary>
        public const TrainingMotivation StartingMotivation = TrainingMotivation.Normal;

        /// <summary>
        /// やる気最低時の訓練上昇倍率
        /// </summary>
        public const float MotivationTrainMultiplierVeryLow = 0.6f;

        /// <summary>
        /// やる気低時の訓練上昇倍率
        /// </summary>
        public const float MotivationTrainMultiplierLow = 0.8f;

        /// <summary>
        /// やる気普通時の訓練上昇倍率
        /// </summary>
        public const float MotivationTrainMultiplierNormal = 1f;

        /// <summary>
        /// やる気高時の訓練上昇倍率
        /// </summary>
        public const float MotivationTrainMultiplierHigh = 1.3f;

        /// <summary>
        /// 行動成功後に技習得イベントが発生する確率(百分率)
        /// </summary>
        public const float LearnAttackEventTriggerPercent = 10f;

        /// <summary>
        /// 行動成功後に特訓イベントが発生する確率(百分率)
        /// </summary>
        public const float StatBoostEventTriggerPercent = 8f;

        /// <summary>
        /// 行動成功後に強敵急襲イベントが発生する確率(百分率)
        /// </summary>
        public const float AmbushEventTriggerPercent = 12f;

        /// <summary>
        /// 強敵急襲勝利時の基礎賞金
        /// </summary>
        public const int AmbushVictoryReward = 200;

        /// <summary>
        /// 強敵急襲勝利時のHP上昇
        /// </summary>
        public const int AmbushVictoryHpGain = 48;

        /// <summary>
        /// 強敵急襲勝利時の攻撃上昇
        /// </summary>
        public const int AmbushVictoryAttackGain = 16;

        /// <summary>
        /// 強敵急襲勝利時の防御上昇
        /// </summary>
        public const int AmbushVictoryDefenseGain = 16;

        /// <summary>
        /// 強敵急襲勝利時の速度上昇
        /// </summary>
        public const int AmbushVictorySpeedGain = 10;

        /// <summary>
        /// 強敵急襲勝利時の命中上昇
        /// </summary>
        public const int AmbushVictoryHitGain = 10;

        /// <summary>
        /// イベントのHP上昇最小
        /// </summary>
        public const int EventStatHpMin = 4;

        /// <summary>
        /// イベントのHP上昇最大
        /// </summary>
        public const int EventStatHpMax = 12;

        /// <summary>
        /// イベントの攻撃上昇最小
        /// </summary>
        public const int EventStatAttackMin = 2;

        /// <summary>
        /// イベントの攻撃上昇最大
        /// </summary>
        public const int EventStatAttackMax = 6;

        /// <summary>
        /// イベントの防御上昇最小
        /// </summary>
        public const int EventStatDefenseMin = 2;

        /// <summary>
        /// イベントの防御上昇最大
        /// </summary>
        public const int EventStatDefenseMax = 6;

        /// <summary>
        /// イベントの速度上昇最小
        /// </summary>
        public const int EventStatSpeedMin = 1;

        /// <summary>
        /// イベントの速度上昇最大
        /// </summary>
        public const int EventStatSpeedMax = 4;

        /// <summary>
        /// イベントの命中上昇最小
        /// </summary>
        public const int EventStatHitMin = 1;

        /// <summary>
        /// イベントの命中上昇最大
        /// </summary>
        public const int EventStatHitMax = 4;

        /// <summary>
        /// 互換用の行き先候補数
        /// </summary>
        public const int LocationChoiceCount = 3;

        /// <summary>
        /// 互換用の校長室倍率
        /// </summary>
        public const float PrincipalOfficeBonusMultiplier = 1.6f;

        /// <summary>
        /// 継承に必要な育成済みモンスター数
        /// </summary>
        public const int InheritanceParentCount = 2;

        /// <summary>
        /// 継承時に各親ステータスから加算する割合(百分率)
        /// </summary>
        public const int InheritanceStatPercentPerParent = 10;

        /// <summary>
        /// 継承演出の配置待ち秒数
        /// </summary>
        public const float InheritancePresentationPoseSeconds = 0.8f;

        /// <summary>
        /// 継承演出の発光秒数
        /// </summary>
        public const float InheritancePresentationGlowSeconds = 0.7f;

        /// <summary>
        /// 継承演出の上昇光秒数
        /// </summary>
        public const float InheritancePresentationLightRiseSeconds = 0.9f;

        /// <summary>
        /// 継承演出の高所での中央収束秒数
        /// </summary>
        public const float InheritancePresentationLightConvergeSeconds = 0.35f;

        /// <summary>
        /// 継承演出の下降光秒数
        /// </summary>
        public const float InheritancePresentationLightFallSeconds = 0.4f;

        /// <summary>
        /// 継承演出のタイトル表示秒数
        /// </summary>
        public const float InheritancePresentationTitleSeconds = 1.4f;

        /// <summary>
        /// 継承タイトル出現アニメ秒数
        /// </summary>
        public const float InheritancePresentationTitleAppearSeconds = 0.85f;

        /// <summary>
        /// 継承タイトル出現開始スケール
        /// </summary>
        public const float InheritancePresentationTitleAppearStartScale = 3.2f;

        /// <summary>
        /// 継承タイトル衝撃スケール
        /// </summary>
        public const float InheritancePresentationTitleImpactScale = 1.35f;

        /// <summary>
        /// 継承タイトル回転振れ幅(度)
        /// </summary>
        public const float InheritancePresentationTitleSpinDegrees = 18f;

        /// <summary>
        /// 継承演出の上昇光の高さ
        /// </summary>
        public const float InheritancePresentationLightRiseHeight = 7f;

        /// <summary>
        /// 継承元消去エフェクトの再生秒数
        /// </summary>
        public const float InheritancePresentationDisappearEffectSeconds = 1.2f;

        /// <summary>
        /// 継承先出現エフェクトの再生秒数
        /// </summary>
        public const float InheritancePresentationAppearEffectSeconds = 0.6f;

        /// <summary>
        /// 継承先出現エフェクトの再生速度倍率
        /// </summary>
        public const float InheritancePresentationAppearEffectSimulationSpeed = 2f;

        /// <summary>
        /// 下降完了前に出現エフェクトを始める先行秒数
        /// </summary>
        public const float InheritancePresentationAppearEffectLeadInSeconds = 0.2f;

        /// <summary>
        /// Magic shieldの基準身長
        /// </summary>
        public const float InheritancePresentationShieldReferenceHeight = 2f;

        /// <summary>
        /// Magic shieldの基準直径
        /// </summary>
        public const float InheritancePresentationShieldReferenceDiameter = 2f;

        /// <summary>
        /// Magic shieldのリング相対高さ
        /// </summary>
        public const float InheritancePresentationShieldRingLocalY = 1f;

        /// <summary>
        /// 着地後Crystalのサイズ倍率
        /// </summary>
        public const float InheritancePresentationCrystalSettleScaleMultiplier = 1.35f;

        /// <summary>
        /// 着地後Crystalの拡大秒数
        /// </summary>
        public const float InheritancePresentationCrystalSettleSeconds = 0.4f;

        /// <summary>
        /// 継承元が内側を向くヨー角度
        /// </summary>
        public const float InheritancePresentationParentInwardYawDegrees = 18f;
    }
}
