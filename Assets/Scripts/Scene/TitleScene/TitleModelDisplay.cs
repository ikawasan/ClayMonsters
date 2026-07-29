using System.Collections.Generic;
using System.IO;
using System.Threading;
using Battle;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData;
using SaveData.Interface;
using UnityEngine;
using VContainer;

namespace Scene.TitleScene
{
    /// <summary>
    /// TitleシーンのModelSpawnRootへ保存済みモデルを1体表示する
    /// プレイヤー作成モンスターからランダムに選び未作成時は敵スロット0を使う
    /// </summary>
    public sealed class TitleModelDisplay : MonoBehaviour
    {
        private const int EnemyFallbackSlotIndex = 0;

        [SerializeField] private Transform spawnParent;
        [SerializeField] private LoadedModelConfigurator configurator;
        [SerializeField] private Transform fieldRoot;
        [SerializeField] private float fieldFloorLocalY = 0.605f;
        [SerializeField] private Transform groundReference;

        [Inject] private readonly IClayModelImporter importer;
        [Inject] private readonly IClayModelSaveService saveService;

        private Transform modelsRoot;
        private readonly List<GameObject> loadedModels = new List<GameObject>();

        /// <summary>
        /// モデル未表示時の注視点
        /// </summary>
        public Vector3 DefaultFocusCenter =>
            spawnParent != null ? spawnParent.position + Vector3.up * 0.85f : Vector3.zero;

        /// <summary>
        /// 保存済みモデルを読み込み表示する
        /// </summary>
        public async UniTask RefreshAsync(CancellationToken cancellationToken)
        {
            Clear();

            if (spawnParent == null || importer == null || saveService == null || configurator == null)
            {
                return;
            }

            if (!TryResolveSpawnSlot(out ModelSavePool pool, out int slotIndex))
            {
                return;
            }

            modelsRoot = new GameObject("TitleModels").transform;
            modelsRoot.SetParent(spawnParent, false);
            modelsRoot.localPosition = Vector3.zero;
            modelsRoot.localRotation = Quaternion.identity;

            GameObject imported = await LoadModelAsync(pool, slotIndex, modelsRoot, cancellationToken);
            if (imported == null)
            {
                Clear();
                return;
            }

            imported.transform.SetParent(modelsRoot, false);
            imported.transform.localRotation = Quaternion.identity;
            imported.transform.localPosition = Vector3.zero;

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            SnapModelToGround(imported.transform);

            loadedModels.Add(imported);
        }

        /// <summary>
        /// 表示中モデルの視覚中心を返す
        /// </summary>
        public bool TryGetVisualCenter(out Vector3 center)
        {
            if (!TryGetVisualBounds(out Bounds bounds))
            {
                center = spawnParent != null ? spawnParent.position : Vector3.zero;
                return false;
            }

            center = bounds.center;
            return true;
        }

        /// <summary>
        /// 表示中モデルのRenderer結合境界を返す
        /// </summary>
        public bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            if (loadedModels.Count == 0)
            {
                return false;
            }

            bool hasBounds = false;
            for (int i = 0; i < loadedModels.Count; i++)
            {
                GameObject model = loadedModels[i];
                if (model == null)
                {
                    continue;
                }

                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            return hasBounds;
        }

        /// <summary>
        /// 表示中モデルを地面へ再スナップする
        /// </summary>
        public void SnapAllModelsToGround()
        {
            for (int i = 0; i < loadedModels.Count; i++)
            {
                GameObject model = loadedModels[i];
                if (model == null)
                {
                    continue;
                }

                SnapModelToGround(model.transform);
            }
        }

        /// <summary>
        /// 表示中モデルを窓と反対方向へ向ける
        /// </summary>
        public void FaceAwayFromWindows(Transform windowsReference)
        {
            if (windowsReference == null)
            {
                return;
            }

            for (int i = 0; i < loadedModels.Count; i++)
            {
                GameObject model = loadedModels[i];
                if (model == null)
                {
                    continue;
                }

                Vector3 toWindows = windowsReference.position - model.transform.position;
                toWindows.y = 0f;
                if (toWindows.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                model.transform.rotation = Quaternion.LookRotation(-toWindows.normalized, Vector3.up);
            }
        }

        /// <summary>
        /// 表示中モデルを破棄する
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < loadedModels.Count; i++)
            {
                if (loadedModels[i] != null)
                {
                    Destroy(loadedModels[i]);
                }
            }

            loadedModels.Clear();

            if (modelsRoot != null)
            {
                Destroy(modelsRoot.gameObject);
                modelsRoot = null;
            }
        }

        private bool TryResolveSpawnSlot(out ModelSavePool pool, out int slotIndex)
        {
            List<int> playerSlots = CollectUsedPlayerSlots();
            if (playerSlots.Count > 0)
            {
                pool = ModelSavePool.Player;
                slotIndex = playerSlots[Random.Range(0, playerSlots.Count)];
                return true;
            }

            if (IsSlotLoadable(ModelSavePool.Enemy, EnemyFallbackSlotIndex))
            {
                pool = ModelSavePool.Enemy;
                slotIndex = EnemyFallbackSlotIndex;
                return true;
            }

            pool = default;
            slotIndex = -1;
            return false;
        }

        private bool IsSlotLoadable(ModelSavePool pool, int index)
        {
            ModelSaveSlot slot = saveService.GetSlot(pool, index);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                return false;
            }

            return ModelSaveStorage.Exists(slot.glbFileName);
        }

        private async UniTask<GameObject> LoadModelAsync(
            ModelSavePool pool,
            int slotIndex,
            Transform parent,
            CancellationToken cancellationToken)
        {
            ModelSaveSlot slot = saveService.GetSlot(pool, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                return null;
            }

            string filePath = ModelSaveStorage.ResolveReadPath(slot.glbFileName);
            if (!File.Exists(filePath))
            {
                return null;
            }

            GameObject imported = await importer.ImportFromGlbAsync(filePath, parent, cancellationToken);
            if (imported == null)
            {
                return null;
            }

            LoadedModelConfigurator.Result configured = configurator.Configure(imported);
            configured.Motion?.SetRootTranslationEnabled(false);
            configured.Motion?.Play(MotionType.None);
            return imported;
        }

        private void SnapModelToGround(Transform model)
        {
            float groundY = ResolveGroundY(model.position);
            BattleSpawnPlacement.SnapBottomToGroundY(model, groundY);
        }

        private float ResolveGroundY(Vector3 worldPosition)
        {
            if (groundReference != null)
            {
                return groundReference.position.y;
            }

            if (fieldRoot != null)
            {
                Vector3 local = fieldRoot.InverseTransformPoint(worldPosition);
                local.y = fieldFloorLocalY;
                return fieldRoot.TransformPoint(local).y;
            }

            return spawnParent != null ? spawnParent.position.y : worldPosition.y;
        }

        private List<int> CollectUsedPlayerSlots()
        {
            var result = new List<int>();
            for (int i = 0; i < ModelSavePoolSettings.GetSlotCount(ModelSavePool.Player); i++)
            {
                if (IsSlotLoadable(ModelSavePool.Player, i))
                {
                    result.Add(i);
                }
            }

            return result;
        }
    }
}
