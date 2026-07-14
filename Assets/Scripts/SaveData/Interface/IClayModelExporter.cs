using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SaveData.Interface
{
    /// <summary>
    /// モデルをglbへエクスポート
    /// </summary>
    public interface IClayModelExporter
    {
        /// <summary>
        /// 実行中の SkinnedMeshRenderer とボーンルートからエクスポート用の独立階層を組み立ててglb出力
        /// </summary>
        /// <param name="runtimeRenderer">実行中のスキンメッシュ（マーチングキューブの結果）</param>
        /// <param name="boneRoot">ボーン階層のルート（複製してエクスポート階層に組み込む）</param>
        /// <param name="filePath">出力先のファイルパス（.glb）</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>成功したか</returns>
        UniTask<bool> ExportToGlbAsync(
            SkinnedMeshRenderer runtimeRenderer,
            Transform boneRoot,
            string filePath,
            CancellationToken cancellationToken);
    }
}