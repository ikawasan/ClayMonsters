using ClayEditor.Rigging.Interface;
using ClayEditor.Rigging;
using GameData;
using R3;
using UnityEngine;
using VContainer;

namespace ClayEditor
{
    /// <summary>
    /// 造形メッシュからボーンを生成し、ウェイト計算とメッシュ適用まで行う制御役
    /// ボーン生成は重いので造形中には行わず、アニメーションモード遷移時とエクスポート時に
    /// 外部から RebuildSkeleton を呼んで生成する
    /// ボーン生成は IBoneSkeletonGenerator に委譲し、溶接後のトポロジーを
    /// ウェイト計算へ渡して二重溶接を防ぐ
    /// </summary>
    public class ClayAutoRigController : MonoBehaviour
    {
        [Inject] private readonly IBoneSkeletonGenerator skeletonGenerator;
        [Inject] private readonly ClayAutoRigger rigger;
        [Inject] private readonly ClayVoxelEngine engine;
        [Inject] private readonly SkeletonPartAnalyzer partAnalyzer;

        [Header("Auto Rig Settings")]
        [Tooltip("生成したボーンを配置する固定の親(シーン上のBoneRootオブジェクト)")]
        [SerializeField] private Transform boneRoot;

        [Tooltip("ウェイト計算やbindposeの基準となるメッシュルート(ClayEditorのTransformを指定)")]
        [SerializeField] private SkinnedMeshRenderer meshRenderer;

        [Tooltip("生成するボーン(ジョイント)数 / スライス数")]
        [SerializeField] private int jointCount = 5;

        [Header("Motion")]
        [Tooltip("生成ボーンを使う手続き的アニメーション")]
        [SerializeField] private ProceduralMotionCharacter motionCharacter;

        [SerializeField] private ClayBoneVisualizer boneVisualizer;

        private void Awake()
        {
            if (motionCharacter != null)
            {
                motionCharacter.SetRootTranslationEnabled(false);
            }
        }

        // 生成したボーン群(ルートを含む)
        private Transform[] bones = new Transform[0];
        private readonly Subject<Unit> skeletonRebuiltSubject = new Subject<Unit>();

        /// <summary>
        /// ボーン再生成が完了した通知
        /// </summary>
        public Observable<Unit> OnSkeletonRebuilt => skeletonRebuiltSubject;

        /// <summary>
        /// ボーンを配置する親
        /// </summary>
        public Transform BoneRoot => boneRoot;

        /// <summary>
        /// ウェイト計算の基準となるスキンメッシュ
        /// </summary>
        public SkinnedMeshRenderer MeshRenderer => meshRenderer;

        /// <summary>
        /// 生成したボーン群(ルートを含む)、未生成のときは空配列
        /// </summary>
        public Transform[] Bones => bones;

        /// <summary>
        /// 現在の造形メッシュからボーンを生成し、ウェイト計算とメッシュ適用を行う
        /// アニメーションモード遷移時とエクスポート時に外部から呼ぶ
        /// </summary>
        public void RebuildSkeleton()
        {
            if (boneRoot == null || meshRenderer == null)
            {
                Debug.LogWarning("[ClayAutoRigController] boneRoot / meshRenderer が未設定です");
                return;
            }

            if (!engine.IsBackendReady)
            {
                Debug.LogWarning("[ClayAutoRigController] ボクセルバックエンドが未初期化のためリグを生成できません");
                return;
            }

            engine.FlushShape();
            ClayVoxelEngine.MeshData data = engine.GenerateMeshData();

            // 頂点が無い(全削除など)場合はボーンをクリアして終了する
            if (data.vertices == null || data.vertices.Length == 0)
            {
                if (engine.HasMesh())
                {
                    Debug.LogWarning("[ClayAutoRigController] メッシュ生成に失敗しました CPUバックエンド利用時は造形確定後に再度お試しください");
                }

                ClearBones();
                return;
            }

            // メッシュ形状からボーンを生成する(溶接後のトポロジーを受け取る)
            SkeletonGenerationResult result = skeletonGenerator.Generate(data.vertices, meshRenderer.transform, boneRoot, jointCount);
            if (result.Bones == null || result.Bones.Length == 0)
            {
                ClearBones();
                return;
            }

            // 生成ボーンをリガーへ設定する
            rigger.SetBones(result.Bones);

            // ウェイトを計算してメッシュへ適用する(生成時のトポロジーを平滑化で共有)
            BoneWeight[] weights = rigger.Calculate(data.vertices, meshRenderer.transform, result.Topology);
            engine.ApplyToRenderer(data, weights, rigger.BindPoses, rigger.Bones);

            // 生成ボーンを保持する
            bones = rigger.Bones;

            // 手続き的アニメーションへ生成ボーンを渡す(静止状態で基準ポーズを記録、部位分類器も渡す)
            motionCharacter.Initialize(rigger.Bones, partAnalyzer);

            boneVisualizer.Rebuild();
            skeletonRebuiltSubject.OnNext(Unit.Default);
        }

        /// <summary>
        /// 生成済みボーンを破棄し、リガーとアニメーションの参照をクリアする
        /// </summary>
        public void ClearBones()
        {
            if (boneRoot != null)
            {
                for (int i = boneRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(boneRoot.GetChild(i).gameObject);
                }
            }

            bones = new Transform[0];
            rigger.SetBones(new Transform[0]);
            motionCharacter.Initialize(new Transform[0], partAnalyzer);
            boneVisualizer.Rebuild();
            skeletonRebuiltSubject.OnNext(Unit.Default);
        }

        /// <summary>
        /// スキン適用後に造形・ペイント編集へ戻すときボーンを破棄し表示メッシュを復元する
        /// </summary>
        /// <param name="mode">現在の編集モード</param>
        public void RestoreSculptEditState(EditModeType mode)
        {
            switch (mode)
            {
                case EditModeType.Animation:
                    RebuildSkeleton();
                    return;
                case EditModeType.Paint:
                    ClearBones();
                    engine.SwitchToSingleMesh();
                    return;
                default:
                    ClearBones();
                    engine.UpdateShapeFast(refreshColliders: true);
                    return;
            }
        }

        private void OnDestroy()
        {
            skeletonRebuiltSubject.Dispose();
        }
    }
}