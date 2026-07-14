using ClayEditor;
using ClayEditor.Rigging;
using SaveData;
using SaveData.Service;
using System.Collections.Generic;
using UnityEngine;

namespace UI.ClayEditor.View
{
    /// <summary>
    /// 編集中モデルからセーブプレビュー用のパラメータ・攻撃情報を収集する
    /// </summary>
    public static class ModelSavePreviewCollector
    {
        /// <summary>
        /// 現在の編集モデルから保存前プレビュー用データを収集する
        /// </summary>
        public static bool TryCollect(
            ClayAutoRigController autoRigController,
            SkeletonPartAnalyzer partAnalyzer,
            out ModelStatus status,
            out List<MotionType> attackMotions)
        {
            status = null;
            attackMotions = null;

            if (autoRigController == null || partAnalyzer == null)
            {
                return false;
            }

            autoRigController.RebuildSkeleton();

            SkinnedMeshRenderer renderer = autoRigController.MeshRenderer;
            Transform[] bones = autoRigController.Bones;
            if (renderer == null
                || bones == null
                || bones.Length == 0
                || renderer.sharedMesh == null
                || renderer.sharedMesh.vertexCount == 0)
            {
                return false;
            }

            attackMotions = ClayModelSaveService.PickRandomAttacks(partAnalyzer, bones, ClayModelSaveService.AttackMotionCount);
            status = ModelStatusCalculator.Calculate(renderer, partAnalyzer, bones);
            return true;
        }
    }
}
