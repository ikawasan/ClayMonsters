using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UI.ModelGallery.Data;
using UnityEngine;

namespace UI.ModelGallery.Interface
{
    /// <summary>
    /// 育成前モデル展示室の投稿閲覧ダウンロードを行うサービス契約
    /// </summary>
    public interface IModelGalleryService
    {
        /// <summary>
        /// 未育成スロットのモデルを展示室へ投稿する
        /// </summary>
        /// <param name="sourceSlotIndex">投稿元未育成スロット番号</param>
        /// <param name="title">展示タイトル</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功時はアイテムID失敗時はnull</returns>
        UniTask<string> PublishAsync(int sourceSlotIndex, string title, CancellationToken cancellationToken);

        /// <summary>
        /// 展示室の公開一覧を取得する
        /// </summary>
        /// <param name="sortMode">並び替えモード</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>モードに応じた一覧</returns>
        UniTask<IReadOnlyList<ModelGalleryItemSummary>> QueryAsync(
            ModelGalleryBrowseSortMode sortMode,
            CancellationToken cancellationToken);

        /// <summary>
        /// 展示室アイテムのプレビュー画像を読み込む
        /// 戻り値は呼び出し側でDestroyする
        /// </summary>
        /// <param name="itemId">展示室アイテムID</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>プレビュー画像なければnull</returns>
        UniTask<Texture2D> LoadPreviewAsync(string itemId, CancellationToken cancellationToken);

        /// <summary>
        /// お気に入り状態を切り替える
        /// </summary>
        /// <param name="itemId">展示室アイテムID</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>切替後にお気に入り中ならtrue失敗時はnull</returns>
        UniTask<bool?> ToggleFavoriteAsync(string itemId, CancellationToken cancellationToken);

        /// <summary>
        /// 展示室アイテムを未育成スロットへダウンロード保存する
        /// </summary>
        /// <param name="itemId">展示室アイテムID</param>
        /// <param name="destinationSlotIndex">保存先未育成スロット番号</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>保存に成功したか</returns>
        UniTask<bool> DownloadAsync(string itemId, int destinationSlotIndex, CancellationToken cancellationToken);
    }
}
