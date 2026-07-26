using Battle;
using ClayEditor.Rigging;
using Cysharp.Threading.Tasks;
using SaveData;
using SaveData.Interface;
using Scene.BattleNpcScene;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using VContainer;

namespace Scene.TrainingScene
{
    /// <summary>
    /// 育成シーンで選択スロットのモデルを1体読み込み表示する
    /// </summary>
    public sealed class TrainingDisplay : MonoBehaviour
    {
        [SerializeField] private Transform spawnParent;
        [SerializeField] private Transform displayAnchor;
        [SerializeField] private LoadedModelConfigurator configurator;
        [SerializeField] private Transform fieldRoot;
        [SerializeField] private float fieldFloorLocalY = BattleClassroomFieldLayout.FieldFloorLocalY;
        [SerializeField] private Transform groundReference;

        [Inject] private readonly IClayModelImporter importer;
        [Inject] private readonly IClayModelSaveService saveService;

        private GameObject loadedModel;
        private ProceduralMotionCharacter motionCharacter;
        private readonly List<MotionType> usableAttacks = new List<MotionType>();
        private Vector3 savedDisplayPosition;
        private Quaternion savedDisplayRotation;
        private bool hasSavedDisplayTransform;
        private static readonly Quaternion TrainingDisplayFacingRotation = Quaternion.Euler(0f, 180f, 0f);

        /// <summary>
        /// ロードモデルのセットアップに使うConfigurator
        /// </summary>
        public LoadedModelConfigurator Configurator => configurator;

        /// <summary>
        /// 骨格で使用可能な攻撃
        /// </summary>
        public IReadOnlyList<MotionType> UsableAttacks => usableAttacks;

        private void Awake()
        {
            if (fieldRoot == null)
            {
                Debug.LogError(
                    "[TrainingDisplay] fieldRootが未配線です"
                    + " このシーンのFieldを割り当ててください",
                    this);
            }

            EnsureDisplayAnchor();
            EnsureImportParent();
        }

        /// <summary>
        /// 戦闘前の育成表示位置を保存する
        /// </summary>
        public void SaveDisplayTransform()
        {
            if (loadedModel == null)
            {
                hasSavedDisplayTransform = false;
                return;
            }

            savedDisplayPosition = loadedModel.transform.position;
            savedDisplayRotation = loadedModel.transform.rotation;
            hasSavedDisplayTransform = true;
        }

        /// <summary>
        /// 育成表示位置を保存して戦闘スポーンへ配置する
        /// 表示アンカーと戦闘スポーンの高さを分離する
        /// </summary>
        /// <param name="battleSpawn">戦闘用プレイヤースポーン</param>
        public void PrepareModelForBattle(Transform battleSpawn)
        {
            SaveDisplayTransform();
            if (loadedModel == null || battleSpawn == null)
            {
                return;
            }

            BattleSpawnPlacement.Apply(loadedModel.transform, battleSpawn);
            motionCharacter?.ClearBattlePositionConstraint();
            motionCharacter?.SetRootTranslationEnabled(false);
            motionCharacter?.OnLayoutPositionChanged();
        }

        /// <summary>
        /// 読み込み済みモーションキャラクター
        /// </summary>
        public ProceduralMotionCharacter MotionCharacter => motionCharacter;

        /// <summary>
        /// 表示中モデル
        /// </summary>
        public GameObject LoadedModel => loadedModel;

        /// <summary>
        /// 表示アンカー
        /// </summary>
        public Transform DisplayAnchor
        {
            get
            {
                EnsureDisplayAnchor();
                return displayAnchor;
            }
        }

        /// <summary>
        /// 表示中のプレイヤースロット番号 未表示時は-1
        /// </summary>
        public int LoadedSlotIndex { get; private set; } = -1;

        /// <summary>
        /// 指定スロットのモデルを表示中か
        /// </summary>
        /// <param name="slotIndex">プレイヤースロット番号</param>
        public bool IsDisplayingSlot(int slotIndex)
        {
            return LoadedSlotIndex == slotIndex && loadedModel != null;
        }

        /// <summary>
        /// カメラ注視点の基準となる表示中心を返す
        /// </summary>
        /// <param name="center">表示中心</param>
        /// <returns>常にtrue</returns>
        public bool TryGetDisplayFocusCenter(out Vector3 center)
        {
            if (TryGetVisualBounds(out Bounds bounds))
            {
                center = bounds.center;
                return true;
            }

            if (displayAnchor != null)
            {
                center = displayAnchor.position;
                return true;
            }

            center = Vector3.zero;
            return true;
        }

        /// <summary>
        /// 表示中モデルのRenderer結合境界を返す
        /// </summary>
        /// <param name="bounds">結合境界</param>
        /// <returns>取得できたらtrue</returns>
        public bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            if (loadedModel == null)
            {
                return false;
            }

            bool hasBounds = false;
            Renderer[] renderers = loadedModel.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        /// <summary>
        /// 育成済みセーブ用のメッシュとボーンルートを取得する
        /// </summary>
        /// <param name="renderer">スキンメッシュ</param>
        /// <param name="boneRoot">ボーン階層ルート</param>
        /// <returns>取得できたらtrue</returns>
        public bool TryGetSaveComponents(out SkinnedMeshRenderer renderer, out Transform boneRoot)
        {
            renderer = null;
            boneRoot = null;
            if (loadedModel == null)
            {
                return false;
            }

            renderer = loadedModel.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null)
            {
                return false;
            }

            boneRoot = ResolveBoneRootForExport(loadedModel.transform, renderer);
            return boneRoot != null;
        }

        /// <summary>
        /// 指定スロットのモデルを読み込み表示する
        /// </summary>
        /// <param name="slotIndex">プレイヤースロット番号</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask<bool> LoadSlotAsync(int slotIndex, CancellationToken cancellationToken)
        {
            Clear();

            if (spawnParent == null || importer == null || saveService == null || configurator == null)
            {
                return false;
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
            if (slot == null || string.IsNullOrEmpty(slot.glbFileName))
            {
                return false;
            }

            // 圧縮後はClayModel_SlotN.glb.gzになるためResolveReadPathで展開パスを解決する
            if (!ModelSaveStorage.Exists(slot.glbFileName))
            {
                Debug.LogError(
                    $"[TrainingDisplay] GLBが見つかりません: {slot.glbFileName}",
                    this);
                return false;
            }

            string filePath = ModelSaveStorage.ResolveReadPath(slot.glbFileName);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.LogError(
                    $"[TrainingDisplay] GLBの読込パスを解決できません: {slot.glbFileName}",
                    this);
                return false;
            }

            GameObject imported = await importer.ImportFromGlbAsync(filePath, spawnParent, cancellationToken);
            if (imported == null)
            {
                LoadedSlotIndex = -1;
                return false;
            }

            if (!await SetupDisplayedModelAsync(imported, slot, cancellationToken))
            {
                LoadedSlotIndex = -1;
                return false;
            }

            LoadedSlotIndex = slotIndex;
            return true;
        }

        /// <summary>
        /// 選択UIで読み込み済みのモデルを表示へ引き継ぐ
        /// </summary>
        /// <param name="model">読み込み済みモデル</param>
        /// <param name="slotIndex">プレイヤースロット番号</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask<bool> AdoptLoadedModelAsync(
            GameObject model,
            int slotIndex,
            CancellationToken cancellationToken)
        {
            if (model == null || saveService == null || configurator == null || spawnParent == null)
            {
                return false;
            }

            if (loadedModel != null && loadedModel != model)
            {
                Clear();
            }

            ModelSaveSlot slot = saveService.GetSlot(ModelSavePool.Player, slotIndex);
            if (slot == null)
            {
                return false;
            }

            if (!await SetupDisplayedModelAsync(model, slot, cancellationToken))
            {
                LoadedSlotIndex = -1;
                return false;
            }

            LoadedSlotIndex = slotIndex;
            return true;
        }

        /// <summary>
        /// 戦闘後に育成表示へモデルを戻す
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async UniTask RestoreAfterBattleAsync(CancellationToken cancellationToken)
        {
            if (loadedModel == null)
            {
                return;
            }

            Transform parent = ResolveDisplayParent();
            if (parent == null)
            {
                return;
            }

            loadedModel.SetActive(true);
            loadedModel.transform.localScale = Vector3.one;

            ModelPartLossController partLoss = loadedModel.GetComponent<ModelPartLossController>();
            partLoss?.RestoreAll();

            motionCharacter?.ResetForTrainingDisplay();

            if (hasSavedDisplayTransform)
            {
                loadedModel.transform.SetParent(parent, true);
                loadedModel.transform.SetPositionAndRotation(savedDisplayPosition, savedDisplayRotation);
            }
            else
            {
                loadedModel.transform.SetParent(parent, false);
                loadedModel.transform.localPosition = Vector3.zero;
                loadedModel.transform.localRotation = TrainingDisplayFacingRotation;
                loadedModel.transform.localScale = Vector3.one;
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                SnapModelToGround(loadedModel.transform);
            }
        }

        /// <summary>
        /// 表示中モデルを破棄する
        /// </summary>
        public void Clear()
        {
            motionCharacter = null;
            usableAttacks.Clear();
            LoadedSlotIndex = -1;
            if (loadedModel != null)
            {
                Destroy(loadedModel);
                loadedModel = null;
            }
        }

        /// <summary>
        /// 表示中モデルの足元を地面へ合わせる
        /// </summary>
        public void SnapDisplayedModelToGround()
        {
            if (loadedModel == null)
            {
                return;
            }

            SnapModelToGround(loadedModel.transform);
        }

        private async UniTask<bool> SetupDisplayedModelAsync(
            GameObject model,
            ModelSaveSlot slot,
            CancellationToken cancellationToken)
        {
            LoadedModelConfigurator.Result configured = configurator.Configure(model);
            motionCharacter = configured.Motion;
            RefreshUsableAttacks(configured.Renderer, slot.attackMotions);
            motionCharacter?.SetRootTranslationEnabled(false);
            motionCharacter?.Play(MotionType.Idle);

            Transform parent = ResolveDisplayParent();
            if (parent == null)
            {
                return false;
            }

            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = TrainingDisplayFacingRotation;
            model.transform.localScale = Vector3.one;

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            SnapModelToGround(model.transform);

            loadedModel = model;
            hasSavedDisplayTransform = false;
            return true;
        }

        private void EnsureDisplayAnchor()
        {
            if (displayAnchor != null)
            {
                return;
            }

            GameObject existing = GameObject.Find("TrainingModelDisplayPoint");
            if (existing != null)
            {
                displayAnchor = existing.transform;
                return;
            }

            var anchorObject = new GameObject("TrainingModelDisplayPoint");
            Transform layoutRoot = transform.parent != null ? transform.parent : transform;
            anchorObject.transform.SetParent(layoutRoot, false);
            anchorObject.transform.localPosition = Vector3.zero;
            anchorObject.transform.localRotation = Quaternion.identity;
            displayAnchor = anchorObject.transform;
        }

        private void EnsureImportParent()
        {
            EnsureDisplayAnchor();
            if (displayAnchor == null)
            {
                return;
            }

            if (spawnParent != null && !IsBattleSpawnTransform(spawnParent))
            {
                return;
            }

            Transform existing = displayAnchor.Find("TrainingModelImportRoot");
            if (existing == null)
            {
                var importRootObject = new GameObject("TrainingModelImportRoot");
                importRootObject.transform.SetParent(displayAnchor, false);
                importRootObject.transform.localPosition = Vector3.zero;
                importRootObject.transform.localRotation = Quaternion.identity;
                existing = importRootObject.transform;
            }

            spawnParent = existing;
        }

        private static bool IsBattleSpawnTransform(Transform target)
        {
            return target != null && target.name == "PlayerSpawnPoint";
        }

        private Transform ResolveDisplayParent()
        {
            EnsureDisplayAnchor();
            if (displayAnchor != null)
            {
                return displayAnchor;
            }

            return spawnParent;
        }

        private void SnapModelToGround(Transform modelTransform)
        {
            if (modelTransform == null)
            {
                return;
            }

            float groundY = ResolveDisplayGroundWorldY();
            BattleSpawnPlacement.SnapBottomToGroundY(modelTransform, groundY);
        }

        private float ResolveDisplayGroundWorldY()
        {
            if (groundReference != null)
            {
                return groundReference.position.y;
            }

            EnsureDisplayAnchor();
            if (displayAnchor != null)
            {
                return displayAnchor.position.y;
            }

            if (fieldRoot != null)
            {
                Vector3 local = fieldRoot.InverseTransformPoint(Vector3.zero);
                local.y = fieldFloorLocalY;
                return fieldRoot.TransformPoint(local).y;
            }

            return 0f;
        }

        private static Transform ResolveBoneRootForExport(Transform modelRoot, SkinnedMeshRenderer renderer)
        {
            if (modelRoot == null || renderer == null)
            {
                return null;
            }

            if (renderer.rootBone != null)
            {
                Transform bone = renderer.rootBone;
                while (bone.parent != null && bone.parent != modelRoot)
                {
                    bone = bone.parent;
                }

                if (bone != null && bone.parent == modelRoot)
                {
                    return bone;
                }
            }

            return modelRoot.childCount > 0 ? modelRoot : null;
        }

        private void RefreshUsableAttacks(SkinnedMeshRenderer renderer, IReadOnlyList<MotionType> fallbackAttacks)
        {
            usableAttacks.Clear();
            if (configurator != null
                && configurator.PartAnalyzer != null
                && renderer != null
                && renderer.bones != null
                && renderer.bones.Length > 0)
            {
                usableAttacks.AddRange(
                    AttackMotionSelector.CollectUsableAttacks(configurator.PartAnalyzer, renderer.bones));
            }

            if (usableAttacks.Count > 0 || fallbackAttacks == null)
            {
                return;
            }

            for (int i = 0; i < fallbackAttacks.Count; i++)
            {
                MotionType motion = fallbackAttacks[i];
                if (!usableAttacks.Contains(motion))
                {
                    usableAttacks.Add(motion);
                }
            }
        }
    }
}
