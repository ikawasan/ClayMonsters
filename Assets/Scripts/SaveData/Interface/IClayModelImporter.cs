using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace SaveData.Interface
{
    /// <summary>
    /// glb/glTF ファイルを読み込み、シーンへ生成する機能。
    /// </summary>
    public interface IClayModelImporter
    {
        /// <summary>
        /// 指定パスの glb を読み込み、シーンへ生成する。
        /// </summary>
        /// <param name="filePath">読み込む glb のファイルパス。</param>
        /// <param name="parent">生成先の親 Transform（null ならルートに生成）。</param>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>生成したモデルのルート GameObject。失敗時は null。</returns>
        UniTask<GameObject> ImportFromGlbAsync(string filePath, Transform parent, CancellationToken cancellationToken);
    }
}