using System.Threading;
using UnityEngine;

namespace Scene.TrainingScene.Interface
{
    /// <summary>
    /// 訓練後の専用背景におけるモンスター徘徊
    /// </summary>
    public interface ITrainingMonsterRoamController
    {
        /// <summary>
        /// 指定範囲内で歩きと立ち止まりを繰り返す
        /// 非Roamからの初回開始時のみ範囲内のランダム位置へ配置する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        void StartRoam(CancellationToken cancellationToken);

        /// <summary>
        /// 徘徊を止め表示位置へ戻してIdleにする
        /// </summary>
        void StopRoam();

        /// <summary>
        /// 徘徊XZ範囲の中心と半サイズを返す
        /// </summary>
        /// <param name="center">範囲中心</param>
        /// <param name="halfExtents">XZ半サイズ</param>
        /// <returns>取得できたか</returns>
        bool TryGetRoamPlanarBounds(out Vector3 center, out Vector2 halfExtents);

        /// <summary>
        /// ワールド座標を徘徊範囲内へクランプする
        /// </summary>
        /// <param name="worldPoint">対象座標</param>
        /// <param name="keepY">維持するY</param>
        /// <returns>クランプ後の座標</returns>
        Vector3 ClampToRoamArea(Vector3 worldPoint, float keepY);

        /// <summary>
        /// 地面合わせの基準Yを返す
        /// </summary>
        /// <param name="nearPosition">近傍座標</param>
        /// <returns>地面Y</returns>
        float ResolveGroundY(Vector3 nearPosition);

        /// <summary>
        /// 指定Transformを追いかける
        /// </summary>
        /// <param name="target">追跡対象</param>
        void StartChase(Transform target);

        /// <summary>
        /// 追いかけを終了して通常徘徊へ戻す
        /// </summary>
        void StopChase();

        /// <summary>
        /// クリックで呼んで寄る反応の受付を切り替える
        /// </summary>
        /// <param name="suppressed">trueなら呼出反応を受け付けない</param>
        void SetClickReactionSuppressed(bool suppressed);
    }
}
