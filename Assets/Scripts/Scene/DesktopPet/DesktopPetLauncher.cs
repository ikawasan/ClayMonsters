using System.Collections.Generic;
using Audio.Interface;
using Extensions;
using SaveData;
using SaveData.Interface;
using Scene.DesktopPet.Interface;
using Scene.TitleScene;
using UnityEngine;
using VContainer;

namespace Scene.DesktopPet
{
    /// <summary>
    /// デスクトップペット起動の実装
    /// </summary>
    public sealed class DesktopPetLauncher : IDesktopPetLauncher
    {
        private const int MaxLaunchCount = 5;

        private readonly IBgmService bgmService;
        private readonly IPointsService pointsService;
        private readonly IClayModelSaveService saveService;
        private DesktopPetRuntime activeRuntime;

        /// <summary>
        /// 依存を受け取る
        /// </summary>
        [Inject]
        public DesktopPetLauncher(
            IBgmService bgmService,
            IPointsService pointsService,
            IClayModelSaveService saveService)
        {
            this.bgmService = bgmService;
            this.pointsService = pointsService;
            this.saveService = saveService;
        }

        /// <inheritdoc/>
        public void Launch(IReadOnlyList<int> playerSlotIndices, bool stayOnTop)
        {
            if (playerSlotIndices == null || playerSlotIndices.Count == 0)
            {
                Debug.LogError("[DesktopPetLauncher] スロットが選択されていません");
                return;
            }

            if (activeRuntime != null)
            {
                Debug.LogWarning("[DesktopPetLauncher] 既にデスクトップペットが起動中です");
                return;
            }

            List<int> validSlots = new List<int>(MaxLaunchCount);
            for (int i = 0; i < playerSlotIndices.Count && validSlots.Count < MaxLaunchCount; i++)
            {
                int slotIndex = playerSlotIndices[i];
                if (slotIndex < 0 || validSlots.Contains(slotIndex))
                {
                    continue;
                }

                validSlots.Add(slotIndex);
            }

            if (validSlots.Count == 0)
            {
                Debug.LogError("[DesktopPetLauncher] 有効なスロットがありません");
                return;
            }

            bgmService?.Stop();

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (TryHandoffReadyCacheAndQuit(validSlots, stayOnTop))
            {
                return;
            }
#endif

            DesktopPetLaunchRequest.SetPending(validSlots);

            GameObject host = new GameObject("DesktopPetHost");
            Object.DontDestroyOnLoad(host);
            activeRuntime = host.AddComponent<DesktopPetRuntime>();
            activeRuntime.Begin(
                validSlots,
                saveService,
                bgmService,
                pointsService,
                stayOnTop,
                OnRuntimeStopped);
            DesktopPetLaunchRequest.Clear();
        }

        private bool TryHandoffReadyCacheAndQuit(List<int> validSlots, bool stayOnTop)
        {
            List<string> directories = new List<string>(validSlots.Count);
            for (int i = 0; i < validSlots.Count; i++)
            {
                int slotIndex = validSlots[i];
                ModelSaveSlot slot = saveService != null
                    ? saveService.GetSlot(ModelSavePool.Player, slotIndex)
                    : null;
                string glbFileName = slot != null ? slot.glbFileName : null;
                if (string.IsNullOrEmpty(glbFileName))
                {
                    Debug.LogWarning(
                        "[DesktopPetLauncher] glbが空のため外部引き継ぎ不可 slot=" + slotIndex);
                    return false;
                }

                if (!DesktopPetSpriteCache.IsReady(slotIndex, glbFileName))
                {
                    Debug.LogWarning(
                        "[DesktopPetLauncher] スプライトキャッシュ不足 slot="
                        + slotIndex
                        + " glb="
                        + glbFileName
                        + " モデルを再保存してから起動してください");
                    return false;
                }

                directories.Add(DesktopPetSpriteCache.GetSlotDirectory(slotIndex));
            }

            if (directories.Count == 0 || !DesktopPetExternalProcess.TryStart(directories, stayOnTop))
            {
                Debug.LogWarning("[DesktopPetLauncher] 外部ビューアの起動に失敗しました");
                return false;
            }

            Debug.Log(
                "[DesktopPetLauncher] キャッシュ済みのため即外部ビューアへ引き継ぎます count="
                + directories.Count);
            ApplicationQuitGuard.RequestImmediateQuit();
            return true;
        }

        private void OnRuntimeStopped()
        {
            activeRuntime = null;
        }
    }
}
