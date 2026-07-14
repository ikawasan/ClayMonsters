using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// ロードしたモデル(SkinnedMeshRendererのみの状態)に、動作に必要なコンポーネントを
    /// 付与・初期化し、参照を設定する。
    /// - マテリアル適用(ClayEditと同じCustom/ClayMonster)
    /// - ProceduralMotionCharacter を付与してボーンで初期化＆再生開始(モデルが動くようになる)
    /// - 必要なら ModelPartLossController を付与して部位欠損を使える状態にする
    /// </summary>
    public class LoadedModelConfigurator : MonoBehaviour
    {
        [Header("マテリアル")]
        [Tooltip("ロードしたモデルへ適用するマテリアル(ClayEditで使うCustom/ClayMonster)")]
        [SerializeField] private Material clayMaterial;

        [Header("分類器")]
        [Tooltip("腕/脚などの部位判定に使う分類器。ClayEditと同じ設定にしておくこと")]
        [SerializeField] private SkeletonPartAnalyzer partAnalyzer;

        /// <summary>
        /// 部位分類器
        /// </summary>
        public SkeletonPartAnalyzer PartAnalyzer => partAnalyzer;

        [Header("モーション")]
        [Tooltip("動きの調整値。未指定ならテンプレートまたはScriptableObject既定値を使う")]
        [SerializeField] private ProceduralMotionSettings motionSettings;

        [Tooltip("動きの調整値とシーン固有オプションをコピーする元(任意)")]
        [SerializeField] private ProceduralMotionCharacter motionTemplate;

        [Tooltip("セットアップ後に自動再生するモーション")]
        [SerializeField] private MotionType autoPlayMotion = MotionType.Idle;

        [Header("部位欠損")]
        [Tooltip("ロード時に ModelPartLossController も付与する")]
        [SerializeField] private bool attachPartLoss = true;

        /// <summary>
        /// 設定結果。ゲーム側はここから各コンポーネントを参照して使う。
        /// </summary>
        public struct Result
        {
            public SkinnedMeshRenderer Renderer;
            public ProceduralMotionCharacter Motion;
            public ModelPartLossController PartLoss;
        }

        /// <summary>
        /// ロードしたモデルへ必要コンポーネントを付与・初期化する。
        /// </summary>
        /// <param name="importedRoot">ImportFromGlbAsync で生成したモデルのルート</param>
        public Result Configure(GameObject importedRoot)
        {
            var result = default(Result);
            if (importedRoot == null)
            {
                Debug.LogWarning("[LoadedModelConfigurator] importedRoot が null です");
                return result;
            }

            SkinnedMeshRenderer skinned = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned == null)
            {
                Debug.LogWarning("[LoadedModelConfigurator] SkinnedMeshRenderer が見つかりません");
                return result;
            }
            result.Renderer = skinned;

            // マテリアル適用
            ApplyMaterial(importedRoot);

            // モーション: コンポーネントを付与してボーンで初期化＆再生開始
            bool hasBones = skinned.bones != null && skinned.bones.Length > 0;
            if (hasBones)
            {
                ProceduralMotionCharacter motion = importedRoot.GetComponent<ProceduralMotionCharacter>();
                if (motion == null)
                {
                    motion = importedRoot.AddComponent<ProceduralMotionCharacter>();
                }

                if (motionTemplate != null)
                {
                    motion.CopyConfigurationFrom(motionTemplate);
                }

                if (motionSettings != null)
                {
                    motion.SetMotionSettings(motionSettings);
                }

                motion.Initialize(skinned.bones, partAnalyzer);
                motion.Play(autoPlayMotion);
                result.Motion = motion;
            }
            else
            {
                Debug.LogWarning("[LoadedModelConfigurator] ボーンが無いためモーションを初期化できません");
            }

            // 部位欠損: コンポーネントを付与してセットアップ
            if (attachPartLoss)
            {
                ModelPartLossController partLoss = importedRoot.GetComponent<ModelPartLossController>();
                if (partLoss == null)
                {
                    partLoss = importedRoot.AddComponent<ModelPartLossController>();
                }

                partLoss.Setup(importedRoot, partAnalyzer);
                result.PartLoss = partLoss;
            }

            return result;
        }

        // 配下のレンダラーへ ClayEditと同じマテリアルを適用する
        private void ApplyMaterial(GameObject root)
        {
            if (clayMaterial == null)
            {
                return;
            }

            var skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                skinnedRenderers[i].sharedMaterial = clayMaterial;
            }

            var meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                meshRenderers[i].sharedMaterial = clayMaterial;
            }
        }
    }
}