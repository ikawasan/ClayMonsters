using UnityEngine;

namespace ClayEditor.Rigging
{
    /// <summary>
    /// エクスポート後の別シーンで、インポートしたモデルの SkinnedMeshRenderer から
    /// ボーンを取り出し、ProceduralMotionCharacter に渡して動かすための橋渡し
    /// glbはボーン階層(名前)を保持し、ProceduralMotionCharacterは名前ではなく
    /// 階層構造で役割を推定するため、そのまま走る、攻撃を適用できる
    /// インポートしたモデルにはClayEditと同じマテリアルを適用して見た目を再現する
    /// </summary>
    public class ImportedModelMotionBinder : MonoBehaviour
    {
        [Tooltip("動きを適用するモーションキャラクター")]
        [SerializeField] private ProceduralMotionCharacter motionCharacter;

        [Tooltip("バインド後に自動再生するモーション")]
        [SerializeField] private MotionType autoPlay = MotionType.Run;

        [Tooltip("インポートしたモデルへ適用するマテリアル(ClayEditで使うCustom/ClayMonster)")]
        [SerializeField] private Material clayMaterial;

        /// <summary>
        /// インポートしたモデルのルートからボーンを取り出してバインドし、再生を開始する
        /// </summary>
        /// <param name="importedRoot">インポートしたモデルのルートGameObject</param>
        public void Bind(GameObject importedRoot)
        {
            if (motionCharacter == null || importedRoot == null)
            {
                Debug.LogWarning("[ImportedModelMotionBinder] motionCharacter / importedRoot が未設定です");
                return;
            }

            // glTFastが置き換えた標準マテリアルではなく ClayEditと同じマテリアルを適用する
            ApplyClayMaterial(importedRoot);

            var skinned = importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned == null || skinned.bones == null || skinned.bones.Length == 0)
            {
                Debug.LogWarning("[ImportedModelMotionBinder] スキン付きメッシュが見つかりません");
                return;
            }

            motionCharacter.Initialize(skinned.bones);
            motionCharacter.Play(autoPlay);
        }

        // インポートしたモデル配下のレンダラへ ClayEditと同じマテリアルを適用する
        private void ApplyClayMaterial(GameObject importedRoot)
        {
            if (clayMaterial == null)
            {
                return;
            }

            var skinnedRenderers = importedRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                skinnedRenderers[i].sharedMaterial = clayMaterial;
            }

            var meshRenderers = importedRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                meshRenderers[i].sharedMaterial = clayMaterial;
            }
        }
    }
}