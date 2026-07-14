using UnityEngine;

namespace ClayEditor.Rigging.Interface
{
    /// <summary>
    /// ボーン生成の結果。生成したボーン群と、生成過程で構築したメッシュトポロジー
    /// （溶接＋隣接）を保持する。トポロジーはウェイト平滑化で再利用し、二重溶接を防ぐ。
    /// </summary>
    public struct SkeletonGenerationResult
    {
        /// <summary>生成したボーン Transform 群（ルートから末端の順）。</summary>
        public Transform[] Bones;

        /// <summary>生成過程で構築した溶接トポロジー（無い場合は null）。</summary>
        public MeshTopology Topology;
    }

    /// <summary>
    /// メッシュ形状からスケルトン（ボーン階層）を生成する機能。
    /// 段階1は PCA による直線ボーンチェーン、段階2はカーブスケルトン抽出など、
    /// 実装を差し替えられるようインターフェースとして定義する。
    /// </summary>
    public interface IBoneSkeletonGenerator
    {
        /// <summary>
        /// メッシュ頂点からボーン階層を生成し、ボーンと（あれば）トポロジーを返す。
        /// </summary>
        /// <param name="localVertices">メッシュ頂点（meshRoot のローカル座標）。</param>
        /// <param name="meshRoot">頂点座標が属するルート Transform（ローカル→ワールド変換に用いる）。</param>
        /// <param name="boneParent">生成したボーンを配置する親 Transform（シーン上の固定 BoneRoot）。</param>
        /// <param name="jointCount">生成するジョイント数（または解像度）。</param>
        /// <returns>生成したボーンと、構築済みトポロジー。生成できない場合は空のボーン配列。</returns>
        SkeletonGenerationResult Generate(Vector3[] localVertices, Transform meshRoot, Transform boneParent, int jointCount);
    }
}