using System.Threading;
using ClayEditor;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData;
using SaveData.Interface;
using UI.ClayEditor.Interface;
using UnityEngine;
using VContainer;

namespace Scene.DesktopPet
{
    /// <summary>
    /// プレイヤーモデル保存時にデスクトップペット用スプライトを事前焼き出しする
    /// </summary>
    public sealed class DesktopPetSpritePreBakeService : IPlayerModelSaveSideEffect
    {
        private readonly ClayAutoRigController autoRigController;
        private readonly IClayModelImporter importer;
        private readonly IClayModelSaveService saveService;
        private readonly SkeletonPartAnalyzer partAnalyzer;

        /// <summary>
        /// 依存を受け取る
        /// </summary>
        [Inject]
        public DesktopPetSpritePreBakeService(
            IClayModelImporter importer,
            IClayModelSaveService saveService,
            SkeletonPartAnalyzer partAnalyzer,
            ClayAutoRigController autoRigController)
        {
            this.importer = importer;
            this.saveService = saveService;
            this.partAnalyzer = partAnalyzer;
            this.autoRigController = autoRigController;
        }

        /// <inheritdoc/>
        public async UniTask OnPlayerModelSavedAsync(int slotIndex, CancellationToken cancellationToken)
        {
            if (slotIndex < 0)
            {
                Debug.LogError("[DesktopPetSpritePreBakeService] 無効なスロットです");
                return;
            }

            if (importer == null || saveService == null || partAnalyzer == null)
            {
                Debug.LogError("[DesktopPetSpritePreBakeService] 依存が不足しているため事前焼き出しをスキップします");
                return;
            }

            Material clayMaterial = ResolveClayMaterial();
            if (clayMaterial == null)
            {
                Debug.LogError(
                    "[DesktopPetSpritePreBakeService] ClayMonsterマテリアルが取れないため事前焼き出しをスキップします");
                return;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
            string glbFileName = slot != null ? slot.glbFileName : null;
            if (string.IsNullOrEmpty(glbFileName))
            {
                Debug.LogError("[DesktopPetSpritePreBakeService] glbファイル名がありません slot=" + slotIndex);
                return;
            }

            Debug.Log("[DesktopPetSpritePreBakeService] 事前焼き出しを開始します slot=" + slotIndex);
            DesktopPetSpriteBaker baker = new DesktopPetSpriteBaker(
                importer,
                saveService,
                partAnalyzer,
                clayMaterial);
            DesktopPetSpriteSheet sheet = await baker.BakeAsync(slotIndex, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (sheet == null
                || sheet.GetFrameCount(DesktopPetFacing.AnglePos45, DesktopPetAction.Idle) <= 0
                || sheet.GetFrameCount(DesktopPetFacing.Front, DesktopPetAction.Idle) <= 0
                || sheet.GetFrameCount(DesktopPetFacing.Front, DesktopPetAction.Walk) <= 0
                || !DesktopPetSpriteBaker.HasVisibleContent(sheet))
            {
                Debug.LogError(
                    "[DesktopPetSpritePreBakeService] 事前焼き出しに失敗しました slot=" + slotIndex);
                return;
            }

            if (!DesktopPetSpriteCache.TrySave(slotIndex, glbFileName, sheet))
            {
                Debug.LogError(
                    "[DesktopPetSpritePreBakeService] キャッシュ保存に失敗しました slot=" + slotIndex);
                return;
            }

            Debug.Log("[DesktopPetSpritePreBakeService] 事前焼き出しが完了しました slot=" + slotIndex);
        }

        /// <inheritdoc/>
        public void OnPlayerModelDeleted(int slotIndex)
        {
            if (slotIndex < 0)
            {
                return;
            }

            DesktopPetSpriteCache.DeleteSlotCache(slotIndex);
        }

        private Material ResolveClayMaterial()
        {
            SkinnedMeshRenderer renderer = autoRigController != null
                ? autoRigController.MeshRenderer
                : null;
            if (renderer == null)
            {
                return null;
            }

            return renderer.sharedMaterial;
        }
    }
}
