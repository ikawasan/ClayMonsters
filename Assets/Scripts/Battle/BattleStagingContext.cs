using Battle.Interface;
using Cysharp.Threading.Tasks;
using SaveData;
using System;
using System.Threading;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 戦闘演出に必要な参加者と配置情報
    /// </summary>
    public sealed class BattleStagingContext
    {
        /// <summary>
        /// プレイヤーユニット
        /// </summary>
        public BattleUnit Player { get; set; }

        /// <summary>
        /// 敵ユニット
        /// </summary>
        public BattleUnit Enemy { get; set; }

        /// <summary>
        /// プレイヤースポーン
        /// </summary>
        public Transform PlayerSpawn { get; set; }

        /// <summary>
        /// 敵スポーン
        /// </summary>
        public Transform EnemySpawn { get; set; }

        /// <summary>
        /// プレイヤーモデル
        /// </summary>
        public Transform PlayerModel { get; set; }

        /// <summary>
        /// 敵モデル
        /// </summary>
        public Transform EnemyModel { get; set; }

        /// <summary>
        /// 演出用フェードとキャンバス切替
        /// </summary>
        public IBattleCanvasTransition ScreenFade { get; set; }

        /// <summary>
        /// 本戦開始時の間合い
        /// </summary>
        public float InitialBattleDistance { get; set; }

        /// <summary>
        /// 本戦の最大間合い
        /// </summary>
        public float MaxBattleDistance { get; set; }

        /// <summary>
        /// 勝利後の戻るボタンUI
        /// </summary>
        public IBattleVictoryReturnView VictoryReturnView { get; set; }

        /// <summary>
        /// 勝利後のタイトル戻りと再戦ボタンUI
        /// </summary>
        public IBattleDualVictoryReturnView VictoryDualReturnView { get; set; }

        /// <summary>
        /// 勝利演出後に選ばれた遷移先
        /// </summary>
        public BattleVictoryReturnChoice VictoryReturnChoice { get; set; } = BattleVictoryReturnChoice.Title;

        /// <summary>
        /// トーナメント向けの勝利戻りボタンを使うか
        /// </summary>
        public bool UseTournamentVictoryButtons { get; set; }

        /// <summary>
        /// 両者の対戦開始ボタン押下完了を待つ
        /// </summary>
        public System.Func<CancellationToken, UniTask> WaitForMatchupStartAsync { get; set; }

        /// <summary>
        /// VS開始ボタンを待たずに紹介演出だけ再生する
        /// </summary>
        public bool AutoStartMatchup { get; set; }

        /// <summary>
        /// CPU戦のVS待ちで敵強さ選択UIを出すか
        /// </summary>
        public bool EnableEnemyStrengthSelect { get; set; }

        /// <summary>
        /// 現在の敵強さ段階
        /// </summary>
        public EnemyStrengthTier EnemyStrengthTier { get; set; } = EnemyStrengthTier.Normal;

        /// <summary>
        /// 強さ段階変更時に敵ユニットを再構築する
        /// </summary>
        public Func<EnemyStrengthTier, BattleUnit> RebuildEnemyWithStrengthTier { get; set; }

        /// <summary>
        /// 両者の中間地点を返す
        /// </summary>
        public Vector3 ResolveCenterPosition()
        {
            if (PlayerSpawn == null && EnemySpawn == null)
            {
                return Vector3.zero;
            }

            if (PlayerSpawn == null)
            {
                return EnemySpawn.position;
            }

            if (EnemySpawn == null)
            {
                return PlayerSpawn.position;
            }

            return (PlayerSpawn.position + EnemySpawn.position) * 0.5f;
        }
    }
}
