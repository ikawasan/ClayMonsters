using Battle;
using Battle.Interface;
using Battle.Interface;
using Battle.View;
using Camera.View;
using ClayEditor.Rigging;
using SaveData;
using SaveData.Interface;
using Scene.BattleNpcScene.Interface;
using Scene.BattleNpcScene.View;
using UI.Battle.View;
using UnityEngine;

namespace Scene.BattleNpcScene
{
    /// <summary>
    /// BattleFlow.ContextをBattleNpc向けに組み立てる
    /// </summary>
    public static class BattleFlowContextBuilder
    {
        /// <summary>
        /// BattleNpc用Contextを生成する
        /// </summary>
        public static BattleFlow.Context Build(
            BattleFlowRunner runner,
            IClayModelSaveService saveService,
            IBattleCanvasTransition presentationTransition,
            int enemySlotIndex)
        {
            if (runner == null)
            {
                return null;
            }

            return new BattleFlow.Context
            {
                PlayerSpawn = runner.PlayerSpawn,
                EnemySpawn = runner.EnemySpawn,
                BattleUiCanvas = runner.BattleUiCanvas,
                EnemySlotIndex = RandomEnemySlotResolver.Resolve(saveService, enemySlotIndex),
                BattleCamera = runner.BattleCamera,
                CameraProfile = runner.CameraProfile,
                PresentationTransition = presentationTransition,
                HitEffect = runner.HitEffect,
                DamagePopup = runner.DamagePopup,
                FinishPresentation = runner.Staging != null ? runner.Staging.FinishPresentation : null,
                PartBreakPresentation = runner.Staging != null ? runner.Staging.PartBreakPresentation : null,
                VictoryReturnView = runner.VictoryReturnView,
                TipsView = runner.TipsView,
                LevelDesignSettings = runner.LevelDesignSettings,
                AutoStartMatchup = false,
            };
        }
    }
}
